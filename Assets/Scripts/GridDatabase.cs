using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "GridDatabase", menuName = "棋盘/创建ID存储器")]
public class GridDatabase : ScriptableObject {
    
    private Dictionary<int, GridNode> cache = new Dictionary<int, GridNode>();

    public void RefreshCache() {
        cache.Clear();
        GridNode[] allNodes = GameObject.FindObjectsOfType<GridNode>();
        foreach (var node in allNodes) {
            if (node.gridId != -1) {
                if (!cache.ContainsKey(node.gridId)) {
                    cache.Add(node.gridId, node);
                }
            }
        }
        Debug.Log($"【系统】数据库刷新完成，当前场景共有 {cache.Count} 个地块。");
    }

    public GridNode GetGridById(int id) {
        if (cache.Count == 0) RefreshCache();
        
        if (cache.TryGetValue(id, out GridNode node)) {
            return node;
        }
        return null;
    }

    public List<GridNode> GetAllNodes() {
        if (cache.Count == 0) RefreshCache();
        return new List<GridNode>(cache.Values);
    }
}