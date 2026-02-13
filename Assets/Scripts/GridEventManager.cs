using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening; 

public class GridEventManager : MonoBehaviour {
    public static GridEventManager Instance;
    
    [Header("落地表现配置")]
    public AudioClip houseLandingSFX;     
    public float shakeDuration = 0.4f;    
    public float shakeStrength = 0.6f;    

    private bool isWaitingShop = false;   

    void Awake() { 
        Instance = this; 
    }

    public IEnumerator HandleGridEvent(PlayerController player, GridNode node, System.Action onComplete) {
        // --- 1. 处理空地逻辑 ---
        if (node.type == GridType.Empty) {
            if (node.owner == null) {
                if (player.money >= node.purchasePrice) {
                    bool decisionMade = false;
                    UIManager.Instance.UpdateStatus($"空地待售：是否花费 ${node.purchasePrice} 购买？");
                    
                    UIManager.Instance.ShowActionButton("购买土地", () => {
                        player.ChangeMoney(-node.purchasePrice);
                        node.owner = player;
                        
                        if (node.buildingAnchor != null && GameDataManager.Instance.housePrefab != null) {
                            StartCoroutine(PlayEnhancedHouseDropAnimation(node));
                        }

                        node.GetComponent<Renderer>().material.color = new Color(0.6f, 1f, 0.6f); 
                        decisionMade = true;
                    });

                    // 注意：这里建议在UIManager里再做一个“跳过”按钮，点击后也设 decisionMade = true
                    while (!decisionMade) {
                        yield return null; 
                    }
                }
            }
            else if (node.owner != player) {
                int rent = node.rentPrice;
                UIManager.Instance.UpdateStatus($"踏入玩家 {node.owner.playerId} 的领地，支付租金 ${rent}");
                player.ChangeMoney(-rent);
                node.owner.ChangeMoney(rent);
                yield return new WaitForSeconds(1.5f);
            }
        }
        
        // --- 2. 处理商店逻辑 (保持阻塞直到关闭) ---
        else if (node.type == GridType.Shop) {
            if (ShopManager.Instance != null) {
                isWaitingShop = true;
                ShopManager.Instance.OpenShop();
                
                while (isWaitingShop) {
                    yield return null;
                }
            }
        }

        onComplete?.Invoke();
    }

    public void NotifyShopClosed() {
        isWaitingShop = false;
    }

    public IEnumerator PlayEnhancedHouseDropAnimation(GridNode node) {
        if (node.buildingAnchor == null || GameDataManager.Instance.housePrefab == null) yield break;

        Vector3 targetPos = node.buildingAnchor.position;
        Vector3 startPos = targetPos + Vector3.up * GameDataManager.Instance.dropHeight;

        GameObject house = Instantiate(GameDataManager.Instance.housePrefab, startPos, node.buildingAnchor.rotation);
        house.transform.SetParent(node.buildingAnchor);
        node.currentBuilding = house;

        Vector3 originalScale = house.transform.localScale;

        float duration = 0.5f; 
        house.transform.DOMove(targetPos, duration).SetEase(Ease.InQuad);
        house.transform.DOScale(new Vector3(originalScale.x * 0.8f, originalScale.y * 1.4f, originalScale.z * 0.8f), duration * 0.8f);

        yield return new WaitForSeconds(duration);

        house.transform.position = targetPos;

        if (CameraController.Instance != null) {
            CameraController.Instance.ApplyShake(shakeDuration, shakeStrength);
        }

        if (houseLandingSFX != null) {
            AudioSource.PlayClipAtPoint(houseLandingSFX, targetPos);
        }

        if (GameDataManager.Instance.smokeEffectPrefab != null) {
            GameObject smoke = Instantiate(GameDataManager.Instance.smokeEffectPrefab, targetPos, Quaternion.identity);
            Destroy(smoke, 3.0f);
        }

        Sequence s = DOTween.Sequence();
        s.Append(house.transform.DOScale(new Vector3(originalScale.x * 1.3f, originalScale.y * 0.7f, originalScale.z * 1.3f), 0.1f));
        s.Append(house.transform.DOScale(originalScale, 0.2f).SetEase(Ease.OutBack));
    }
}