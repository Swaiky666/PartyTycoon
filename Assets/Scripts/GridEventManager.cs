using UnityEngine;
using System.Collections;

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
                        node.GetComponent<Renderer>().material.color = new Color(0.6f, 1f, 0.6f); 
                        decisionMade = true;
                    });

                    while (!decisionMade) {
                        // 逻辑：你可以通过ESC键跳过购买，或者在UIManager添加取消按钮
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
}