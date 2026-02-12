using UnityEngine;
using System.Collections;
using DG.Tweening; // 确保已安装并导入 DOTween

public class GridEventManager : MonoBehaviour {
    public static GridEventManager Instance;
    
    [Header("落地表现配置")]
    public AudioClip houseLandingSFX;     // 落地音效
    public float shakeDuration = 0.4f;    // 震动持续时间
    public float shakeStrength = 0.6f;    // 震动强度

    void Awake() { Instance = this; }

    public IEnumerator HandleGridEvent(PlayerController player, GridNode node, System.Action onComplete) {
        if (node.type == GridType.Empty) {
            if (node.owner == null) {
                if (player.money >= node.purchasePrice) {
                    bool decisionMade = false;
                    UIManager.Instance.UpdateStatus($"空地待售：是否花费 ${node.purchasePrice} 购买？");
                    
                    UIManager.Instance.ShowActionButton("购买土地", () => {
                        player.ChangeMoney(-node.purchasePrice);
                        node.owner = player;
                        
                        // 触发增强版房屋生成动画
                        if (node.buildingAnchor != null && GameDataManager.Instance.housePrefab != null) {
                            StartCoroutine(PlayEnhancedHouseDropAnimation(node));
                        }

                        node.GetComponent<Renderer>().material.color = new Color(0.6f, 1f, 0.6f); 
                        decisionMade = true;
                    });

                    while (!decisionMade) {
                        if (Input.GetKeyDown(KeyCode.Escape)) decisionMade = true;
                        yield return null;
                    }
                    UIManager.Instance.HideActionButton();
                }
            } else if (node.owner != player) {
                UIManager.Instance.UpdateStatus($"过路费：支付 ${node.rentPrice} 给玩家 {node.owner.playerId}");
                player.ChangeMoney(-node.rentPrice);
                node.owner.ChangeMoney(node.rentPrice);
                yield return new WaitForSeconds(2.0f);
            }
        }
        
        onComplete?.Invoke();
    }

    private IEnumerator PlayEnhancedHouseDropAnimation(GridNode node) {
        // 1. 基础参数准备
        float height = GameDataManager.Instance.dropHeight;
        Vector3 targetPos = node.buildingAnchor.position;
        Vector3 spawnPos = targetPos + Vector3.up * height;

        // 2. 自动旋转对齐：计算从锚点指向当前地块中心的向量
        Vector3 directionToGrid = (node.transform.position - targetPos).normalized;
        directionToGrid.y = 0; // 锁定水平方向
        Quaternion targetRotation = Quaternion.LookRotation(directionToGrid);
        
        // 四舍五入到 90 度的倍数，确保房屋对齐规整
        Vector3 angles = targetRotation.eulerAngles;
        angles.y = Mathf.Round(angles.y / 90f) * 90f;
        Quaternion finalRotation = Quaternion.Euler(angles);

        // 3. 实例化与初始化缩放
        GameObject house = Instantiate(GameDataManager.Instance.housePrefab, spawnPos, finalRotation);
        node.currentBuilding = house;
        Vector3 originalScale = house.transform.localScale;

        // 4. 下落动画：结合位移与纵向拉伸 (Stretch)
        float duration = 0.5f; 
        house.transform.DOMove(targetPos, duration).SetEase(Ease.InQuad);
        // 下落过程中变得细长
        house.transform.DOScale(new Vector3(originalScale.x * 0.8f, originalScale.y * 1.4f, originalScale.z * 0.8f), duration * 0.8f);

        yield return new WaitForSeconds(duration);

        // 5. 落地瞬间表现
        house.transform.position = targetPos;

        // (A) 调用相机的位移补偿震动接口
        if (CameraController.Instance != null) {
            CameraController.Instance.ApplyShake(shakeDuration, shakeStrength);
        }

        // (B) 落地音效
        if (houseLandingSFX != null) {
            AudioSource.PlayClipAtPoint(houseLandingSFX, targetPos);
        }

        // (C) 落地烟雾粒子
        if (GameDataManager.Instance.smokeEffectPrefab != null) {
            GameObject smoke = Instantiate(GameDataManager.Instance.smokeEffectPrefab, targetPos, Quaternion.identity);
            Destroy(smoke, 3.0f);
        }

        // 6. 落地弹性序列：压缩 (Squash) -> 恢复
        Sequence s = DOTween.Sequence();
        // 瞬间压扁
        s.Append(house.transform.DOScale(new Vector3(originalScale.x * 1.3f, originalScale.y * 0.6f, originalScale.z * 1.3f), 0.1f)); 
        // 略微反弹拉长
        s.Append(house.transform.DOScale(new Vector3(originalScale.x * 0.9f, originalScale.y * 1.1f, originalScale.z * 0.9f), 0.1f)); 
        // 最终恢复原状
        s.Append(house.transform.DOScale(originalScale, 0.1f)); 
    }
}