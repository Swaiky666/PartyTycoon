using UnityEngine;
using System.Collections.Generic;

public class CardRangeFinder : MonoBehaviour {
    public static CardRangeFinder Instance;
    
    private List<GridNode> highlightedNodes = new List<GridNode>();
    private CardBase currentCard;

    void Awake() { Instance = this; }

    public void ShowRange(CardBase card) {
        currentCard = card;
        ClearHighlight();
        
        PlayerController p = TurnManager.Instance.GetCurrentPlayer();
        // 获取符合条件的格子
        highlightedNodes = CalculateRange(p.currentGrid, card.rangeStraight, card.rangeAdjacent);
        
        // 标红显示
        foreach (var node in highlightedNodes) {
            node.GetComponent<Renderer>().material.color = Color.red;
            // 为格子增加点击确认逻辑（暂时简化，可复用 ArrowClicker 思想）
            node.gameObject.AddComponent<GridClicker>().Setup(node, ConfirmUse);
        }
        
        UIManager.Instance.UpdateStatus("点击红色地块使用道具");
    }

    private List<GridNode> CalculateRange(GridNode center, int straightLimit, int adjacentLimit) {
        List<GridNode> results = new List<GridNode>();
        
        // BFS 算法寻找“相邻路径”范围
        Queue<(GridNode node, int dist)> queue = new Queue<(GridNode, int)>();
        queue.Enqueue((center, 0));
        HashSet<GridNode> visited = new HashSet<GridNode>();
        visited.Add(center);

        while (queue.Count > 0) {
            var current = queue.Dequeue();
            if (current.dist > 0) results.Add(current.node); // 不包含自己站的格子

            if (current.dist < adjacentLimit) {
                foreach (var next in current.node.connections) {
                    if (next != null && !visited.Contains(next)) {
                        visited.Add(next);
                        queue.Enqueue((next, current.dist + 1));
                    }
                }
            }
        }
        
        // 直线逻辑已经在 BFS 中涵盖（因为直线也是相邻的一种），
        // 如果你的直线范围大于相邻范围，则需要在此处额外写四个方向的循环扩展。
        
        return results;
    }

    void ConfirmUse(GridNode target) {
        if (currentCard.UseCard(TurnManager.Instance.GetCurrentPlayer(), target)) {
            ClearHighlight();
            TurnManager.Instance.CompleteCardAction();
        }
    }

    public void ClearHighlight() {
        foreach (var node in highlightedNodes) {
            if (node == null) continue;
            // 还原颜色（此处应根据地块原本逻辑还原，暂时设为白色）
            node.GetComponent<Renderer>().material.color = Color.white;
            Destroy(node.GetComponent<GridClicker>());
        }
        highlightedNodes.Clear();
    }
}