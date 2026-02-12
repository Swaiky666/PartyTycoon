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
        
        GUI.color = Color.yellow;
        if (GUILayout.Button("【重置】删除并重新生成 BuildingAnchor")) ForceRebuildAnchors();
        GUI.color = Color.white;

        EditorGUILayout.Space();
        GUILayout.Label("3. 路径与站位", EditorStyles.boldLabel);
        if (GUILayout.Button("自动连接路径 (Link)")) LinkNodes();
        if (GUILayout.Button("配置角色站位 (Slots)")) ManageSlots();

        EditorGUILayout.Space();
        GUILayout.Label("4. 数据库同步", EditorStyles.boldLabel);
        if (GUILayout.Button("同步所有地块到数据库")) SyncAllToDatabase();
    }

    private void ForceRebuildAnchors() {
        GameObject[] selected = Selection.gameObjects;
        if (selected.Length == 0) return;

        HashSet<Vector2Int> occupiedSlots = new HashSet<Vector2Int>();
        GridNode[] allNodes = GameObject.FindObjectsOfType<GridNode>();
        foreach (var n in allNodes) occupiedSlots.Add(GetGridPos(n.transform.position));

        foreach (GameObject obj in selected) {
            GridNode node = obj.GetComponent<GridNode>();
            if (node == null) continue;

            Transform oldAnchor = obj.transform.Find("BuildingAnchor");
            if (oldAnchor != null) Undo.DestroyObjectImmediate(oldAnchor.gameObject);

            Vector2Int tileGridPos = GetGridPos(obj.transform.position);
            Vector2Int anchorGridPos = FindBestNeighborSlot(tileGridPos, occupiedSlots);
            occupiedSlots.Add(anchorGridPos);

            GameObject newAnchor = new GameObject("BuildingAnchor");
            Undo.RegisterCreatedObjectUndo(newAnchor, "Create Anchor");
            newAnchor.transform.SetParent(obj.transform);
            newAnchor.transform.position = GridToWorld(anchorGridPos);
            
            // --- 核心修正：朝向当前地板 ---
            // 1. 设置高度偏移
            Vector3 lp = newAnchor.transform.localPosition;
            lp.y = 0.2f; 
            newAnchor.transform.localPosition = lp;

            // 2. 让锚点注视地块中心 (LookAt)
            // 我们通过 LookAt 地块的世界坐标，然后旋转 180 度（因为模型正面通常是 Z 轴正方向）
            newAnchor.transform.LookAt(obj.transform.position);
            // 如果你的模型导入时正面反了，可以根据需要调整下面这一行代码：
            // newAnchor.transform.Rotate(0, 180, 0); 

            Undo.RecordObject(node, "Link New Anchor");
            node.buildingAnchor = newAnchor.transform;
            EditorUtility.SetDirty(node);
        }
        Debug.Log("【工具】建筑选框已重置，且朝向已自动对准地块。");
    }

    private void ResetAndReplan() {
        // ... 原有的地块排布逻辑不变 ...
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
            if (occupiedSlots.Contains(tileGridPos)) tileGridPos = FindEmptySlot(tileGridPos, occupiedSlots);
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
            
            // 在排布逻辑里也加入注视
            node.buildingAnchor.LookAt(node.transform.position);
            Vector3 lp = node.buildingAnchor.localPosition;
            lp.y = 0.2f; 
            node.buildingAnchor.localPosition = lp;
        }
        Debug.Log("【工具】初始化完成：已重新排布地块和选框（含自动对齐朝向）。");
    }

    private Vector2Int GetGridPos(Vector3 pos) => new Vector2Int(Mathf.RoundToInt(pos.x / gridStep), Mathf.RoundToInt(pos.z / gridStep));
    private Vector3 GridToWorld(Vector2Int gPos) => new Vector3(gPos.x * gridStep, 0, gPos.y * gridStep);

    private Vector2Int FindBestNeighborSlot(Vector2Int center, HashSet<Vector2Int> occupied) {
        Vector2Int[] neighbors = { center + new Vector2Int(0, 1), center + new Vector2Int(0, -1), center + new Vector2Int(1, 0), center + new Vector2Int(-1, 0) };
        foreach (var pos in neighbors) if (!occupied.Contains(pos)) return pos;
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
                int dir = -1;
                if (Mathf.Abs(diff.x) > Mathf.Abs(diff.z)) dir = diff.x > 0 ? 1 : 3;
                else dir = diff.z > 0 ? 0 : 2;
                if (dir != -1) nA.connections[dir] = nB;
            }
            EditorUtility.SetDirty(nA);
        }
    }

    private void ManageSlots() {
        foreach (var obj in Selection.gameObjects) {
            GridNode node = obj.GetComponent<GridNode>();
            if (!node) continue;
            Undo.RecordObject(node, "Manage Slots");
            for (int i = 0; i < 6; i++) {
                string sName = "Slot_" + (i + 1);
                Transform s = obj.transform.Find(sName);
                if (!s) { s = new GameObject(sName).transform; s.SetParent(obj.transform); }
                s.localPosition = new Vector3(((i % 3) - 1f) * slotOffset, 0.1f, ((i / 3) == 0 ? 0.3f : -0.3f));
                node.slotPoints[i] = s;
            }
            EditorUtility.SetDirty(node);
        }
    }

    private void SyncAllToDatabase() {
        if (db == null) return;
        GridNode[] allNodes = GameObject.FindObjectsOfType<GridNode>();
        for (int i = 0; i < allNodes.Length; i++) {
            Undo.RecordObject(allNodes[i], "Assign Grid ID");
            allNodes[i].gridId = i;
            EditorUtility.SetDirty(allNodes[i]);
        }
        db.RefreshCache();
        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();
    }
}
#endif