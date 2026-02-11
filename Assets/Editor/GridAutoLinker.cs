#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class GridAutoLinker : EditorWindow {
    private float gridStep = 2.0f;
    private float slotOffset = 0.5f;
    private GridDatabase db;

    [MenuItem("工具/棋盘全自动配置工具")]
    public static void ShowWindow() {
        GridAutoLinker window = GetWindow<GridAutoLinker>("全自动工具");
        window.AutoFindDatabase();
    }

    private void OnEnable() { AutoFindDatabase(); }

    private void AutoFindDatabase() {
        string[] guids = AssetDatabase.FindAssets("t:GridDatabase");
        if (guids.Length > 0) {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            db = AssetDatabase.LoadAssetAtPath<GridDatabase>(path);
        }
    }

    private void OnGUI() {
        GUILayout.Label("1. 基础配置", EditorStyles.boldLabel);
        db = (GridDatabase)EditorGUILayout.ObjectField("当前 ID 存储器", db, typeof(GridDatabase), false);
        gridStep = EditorGUILayout.FloatField("网格尺寸", gridStep);

        EditorGUILayout.Space();
        GUILayout.Label("2. 核心规划 (需选中地板)", EditorStyles.boldLabel);

        GUI.color = Color.cyan;
        if (GUILayout.Button("【初始化】先排地板 -> 再挤开选框")) ResetAndReplan();
        GUI.color = Color.white;

        EditorGUILayout.Space();
        GUILayout.Label("3. 路径与站位", EditorStyles.boldLabel);
        if (GUILayout.Button("自动连接路径 (Link)")) LinkNodes();
        if (GUILayout.Button("配置角色站位 (Slots)")) ManageSlots();

        EditorGUILayout.Space();
        GUILayout.Label("4. 数据库同步", EditorStyles.boldLabel);
        if (GUILayout.Button("同步所有地块到数据库")) SyncAllToDatabase();
    }

    private void ResetAndReplan() {
        GameObject[] selected = Selection.gameObjects;
        if (selected.Length == 0) return;

        Undo.RecordObjects(selected, "Reset and Replan");

        HashSet<Vector2Int> occupiedSlots = new HashSet<Vector2Int>();
        System.Array.Sort(selected, (a, b) => a.transform.position.sqrMagnitude.CompareTo(b.transform.position.sqrMagnitude));

        List<GridNode> nodesToProcess = new List<GridNode>();

        foreach (var obj in selected) {
            GridNode node = obj.GetComponent<GridNode>();
            if (!node) continue;
            nodesToProcess.Add(node);

            Undo.RecordObject(obj.transform, "Snap Tile");
            Vector2Int tileGridPos = GetGridPos(obj.transform.position);

            if (occupiedSlots.Contains(tileGridPos)) {
                tileGridPos = FindEmptySlot(tileGridPos, occupiedSlots);
            }
            
            occupiedSlots.Add(tileGridPos);
            obj.transform.position = GridToWorld(tileGridPos);
        }

        foreach (var node in nodesToProcess) {
            if (node.buildingAnchor == null) continue;

            Undo.RecordObject(node.buildingAnchor, "Snap Anchor");
            Vector2Int tileGridPos = GetGridPos(node.transform.position);
            Vector2Int anchorGridPos = FindBestNeighborSlot(tileGridPos, occupiedSlots);
            occupiedSlots.Add(anchorGridPos);

            node.buildingAnchor.position = GridToWorld(anchorGridPos);
            
            Vector3 lp = node.buildingAnchor.localPosition;
            lp.y = 0.2f; 
            node.buildingAnchor.localPosition = lp;
        }

        Debug.Log("【工具】初始化完成：已重新排布地块和选框。");
    }

    private Vector2Int GetGridPos(Vector3 pos) => new Vector2Int(Mathf.RoundToInt(pos.x / gridStep), Mathf.RoundToInt(pos.z / gridStep));
    private Vector3 GridToWorld(Vector2Int gPos) => new Vector3(gPos.x * gridStep, 0, gPos.y * gridStep);

    private Vector2Int FindBestNeighborSlot(Vector2Int center, HashSet<Vector2Int> occupied) {
        Vector2Int[] neighbors = {
            center + new Vector2Int(0, 1),
            center + new Vector2Int(0, -1),
            center + new Vector2Int(1, 0),
            center + new Vector2Int(-1, 0)
        };

        foreach (var pos in neighbors) {
            if (!occupied.Contains(pos)) return pos;
        }
        return FindEmptySlot(center, occupied);
    }

    private Vector2Int FindEmptySlot(Vector2Int start, HashSet<Vector2Int> occupied) {
        int r = 1;
        while (r < 100) {
            for (int x = -r; x <= r; x++) {
                for (int y = -r; y <= r; y++) {
                    if (Mathf.Abs(x) != r && Mathf.Abs(y) != r) continue;
                    Vector2Int test = start + new Vector2Int(x, y);
                    if (!occupied.Contains(test)) return test;
                }
            }
            r++;
        }
        return start;
    }

    private void LinkNodes() {
        GridNode[] allNodes = GameObject.FindObjectsOfType<GridNode>();
        foreach (var nA in allNodes) {
            Undo.RecordObject(nA, "Link Nodes");
            System.Array.Clear(nA.connections, 0, nA.connections.Length);
            foreach (var nB in allNodes) {
                if (nA == nB) continue;
                Vector3 diff = nB.transform.position - nA.transform.position;
                if (diff.magnitude > gridStep * 1.1f) continue;
                
                // 自动连接上下左右 4 个方向
                int dir = -1;
                if (Mathf.Abs(diff.x) > Mathf.Abs(diff.z)) {
                    dir = diff.x > 0 ? 1 : 3;
                } else {
                    dir = diff.z > 0 ? 0 : 2;
                }
                
                if (dir != -1) nA.connections[dir] = nB;
            }
            EditorUtility.SetDirty(nA);
        }
        Debug.Log("【工具】路径自动连接完成。");
    }

    private void ManageSlots() {
        foreach (var obj in Selection.gameObjects) {
            GridNode node = obj.GetComponent<GridNode>();
            if (!node) continue;
            Undo.RecordObject(node, "Manage Slots");
            for (int i = 0; i < 6; i++) {
                string sName = "Slot_" + (i + 1);
                Transform s = obj.transform.Find(sName);
                if (!s) {
                    s = new GameObject(sName).transform;
                    s.SetParent(obj.transform);
                }
                s.localPosition = new Vector3(((i % 3) - 1f) * slotOffset, 0.1f, ((i / 3) == 0 ? 0.3f : -0.3f));
                node.slotPoints[i] = s;
            }
            EditorUtility.SetDirty(node);
        }
        Debug.Log("【工具】角色站位 Slot 配置完成。");
    }

    // --- 核心修复：不再操作 db.allGrids 列表 ---
    private void SyncAllToDatabase() {
        if (db == null) {
            Debug.LogError("【工具】未指定 GridDatabase，同步失败！");
            return;
        }

        GridNode[] allNodes = GameObject.FindObjectsOfType<GridNode>();
        
        // 1. 给场景中的地块分配唯一 ID
        for (int i = 0; i < allNodes.Length; i++) {
            Undo.RecordObject(allNodes[i], "Assign Grid ID");
            allNodes[i].gridId = i;
            EditorUtility.SetDirty(allNodes[i]);
        }

        // 2. 调用数据库的刷新方法，让它通过 FindObjectsOfType 自动建立内存映射
        db.RefreshCache();

        // 3. 标记数据库已变动（虽然现在是动态缓存，但养成 SetDirty 习惯防止 SO 某些持久化字段未保存）
        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();

        Debug.Log($"【工具】同步成功：已为 {allNodes.Length} 个地块分配 ID 并刷新数据库映射。");
    }
}
#endif