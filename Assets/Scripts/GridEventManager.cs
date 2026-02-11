using UnityEngine;
using System.Collections;
using DG.Tweening; 

public class GridEventManager : MonoBehaviour {
    public static GridEventManager Instance;
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
                        
                        // 玩家购买：播放掉落动画
                        if (node.buildingAnchor != null && GameDataManager.Instance.housePrefab != null) {
                            StartCoroutine(PlayHouseDropAnimation(node));
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

    private IEnumerator PlayHouseDropAnimation(GridNode node) {
        float height = GameDataManager.Instance.dropHeight;
        Vector3 targetPos = node.buildingAnchor.position;
        Vector3 spawnPos = targetPos + Vector3.up * height;

        GameObject house = Instantiate(GameDataManager.Instance.housePrefab, spawnPos, node.buildingAnchor.rotation);
        node.currentBuilding = house;

        float elapsed = 0f;
        float duration = 0.8f; 

        while (elapsed < duration) {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            // 二次方加速模拟重力
            float easeT = t * t; 
            house.transform.position = Vector3.Lerp(spawnPos, targetPos, easeT);
            yield return null;
        }

        house.transform.position = targetPos;

        // 触地烟雾粒子
        if (GameDataManager.Instance.smokeEffectPrefab != null) {
            GameObject smoke = Instantiate(GameDataManager.Instance.smokeEffectPrefab, targetPos, Quaternion.identity);
            Destroy(smoke, 3.0f);
        }

        // 触地视觉反馈：DOTween 震动
        house.transform.DOShakePosition(0.2f, 0.3f);
    }
}