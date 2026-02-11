using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "GridDatabase", menuName = "棋盘/创建ID存储器")]
public class GridDatabase : ScriptableObject {
    
    // 关键修正：必须添加这个特性，否则 Inspector 永远显示 Type Mismatch
    [System.Serializable] 
    public class GridEntry {
        public int id;
        public GridNode node;
    }

    public List<GridEntry> allGrids = new List<GridEntry>();

    public GridNode GetGridById(int id) {
        var entry = allGrids.Find(g => g.id == id);
        return entry != null ? entry.node : null;
    }

    public List<GridNode> GetAllNodes() {
        List<GridNode> nodes = new List<GridNode>();
        foreach (var entry in allGrids) {
            if (entry != null && entry.node != null) nodes.Add(entry.node);
        }
        return nodes;
    }

    public void CleanUp() {
        allGrids.RemoveAll(g => g == null || g.node == null);
        #if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
        #endif
    }
}