using UnityEngine;
using System.Collections.Generic;

public class CardRangeFinder : MonoBehaviour {
    public static CardRangeFinder Instance;
    
    [Header("描边表现设置")]
    public Color outlineColor = Color.yellow;
    public float outlineWidth = 7f;
    public Outline.Mode outlineMode = Outline.Mode.OutlineAll;

    private List<GridNode> highlightedNodes = new List<GridNode>();
    private CardBase currentCard;

    void Awake() { Instance = this; }

    public void ShowRange(CardBase card) {
        currentCard = card;
        ClearHighlight(); // 先清理，确保状态干净
        
        PlayerController p = TurnManager.Instance.GetCurrentPlayer();
        // 计算 BFS 范围
        highlightedNodes = CalculateRange(p.currentGrid, card.rangeStraight, card.rangeAdjacent);
        
        Debug.Log($"【卡牌系统】正在为 {highlightedNodes.Count} 个地块激活交互能力...");

        foreach (var node in highlightedNodes) {
            // 1. 动态添加 BoxCollider (如果原本没有)
            // 必须有 Collider，OnMouseDown 才能生效
            BoxCollider col = node.gameObject.GetComponent<BoxCollider>();
            if (col == null) {
                col = node.gameObject.AddComponent<BoxCollider>();
                // 这里的 Size 可以根据你的地块模型大小微调，通常 1,1,1 或匹配模型
                col.size = new Vector3(2f, 0.5f, 2f); 
                col.isTrigger = true; // 设为 Trigger 防止产生物理碰撞力
            }

            // 2. 动态添加 QuickOutline 描边
            Outline outline = node.gameObject.GetComponent<Outline>();
            if (outline == null) {
                outline = node.gameObject.AddComponent<Outline>();
            }
            outline.OutlineColor = outlineColor;
            outline.OutlineWidth = outlineWidth;
            outline.OutlineMode = outlineMode;
            outline.enabled = true;

            // 3. 动态添加 GridClicker 点击检测
            GridClicker clicker = node.gameObject.GetComponent<GridClicker>();
            if (clicker == null) {
                clicker = node.gameObject.AddComponent<GridClicker>();
            }
            clicker.Setup(node, ConfirmUse);
        }
    }

    private List<GridNode> CalculateRange(GridNode center, int straightLimit, int adjacentLimit) {
        List<GridNode> results = new List<GridNode>();
        Queue<(GridNode node, int dist)> queue = new Queue<(GridNode, int)>();
        queue.Enqueue((center, 0));
        HashSet<GridNode> visited = new HashSet<GridNode>();
        visited.Add(center);

        while (queue.Count > 0) {
            var current = queue.Dequeue();
            if (current.dist > 0) results.Add(current.node); 

            if (current.dist < adjacentLimit) {
                foreach (var next in current.node.connections) {
                    if (next != null && !visited.Contains(next)) {
                        visited.Add(next);
                        queue.Enqueue((next, current.dist + 1));
                    }
                }
            }
        }
        return results;
    }

    void ConfirmUse(GridNode target) {
        Debug.Log($"【卡牌系统】点击确认！目标地块: {target.gridId}");
        PlayerController currentPlayer = TurnManager.Instance.GetCurrentPlayer();

        if (currentCard.UseCard(currentPlayer, target)) {
            currentPlayer.cards.Remove(currentCard);
            ClearHighlight(); // 使用成功，卸载所有组件
            TurnManager.Instance.CompleteCardAction();
        }
    }

    public void ClearHighlight() {
        Debug.Log("【卡牌系统】清理地块交互组件...");
        foreach (var node in highlightedNodes) {
            if (node == null) continue;

            // 1. 移除描边
            Outline outline = node.gameObject.GetComponent<Outline>();
            if (outline != null) Destroy(outline);

            // 2. 移除点击器
            GridClicker clicker = node.gameObject.GetComponent<GridClicker>();
            if (clicker != null) Destroy(clicker);

            // 3. 移除碰撞体 (核心修复)
            // 这样平时地块就不具备射线检测能力，性能最好且不会误触
            BoxCollider col = node.gameObject.GetComponent<BoxCollider>();
            if (col != null) {
                Destroy(col);
            }
        }
        highlightedNodes.Clear();
    }
}