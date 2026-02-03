using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "GridDatabase", menuName = "棋盘/创建ID存储器")]
public class GridDatabase : ScriptableObject {
    [System.Serializable]
    public class GridEntry {
        public int id;
        public GridNode node;
    }

    public List<GridEntry> allGrids = new List<GridEntry>();

    // 根据 ID 获取地块引用
    public GridNode GetGridById(int id) {
        return allGrids.Find(g => g.id == id)?.node;
    }

    // 清理无效引用（防止删除物体后残留空引用）
    public void CleanUp() {
        allGrids.RemoveAll(g => g.node == null);
    }
}