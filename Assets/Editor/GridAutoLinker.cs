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

        // 全局占用字典：记录哪些坐标被占了 (Vector2Int -> 是否是地板)
        // 我们用这个来确保“一个萝卜一个坑”
        HashSet<Vector2Int> occupiedSlots = new HashSet<Vector2Int>();

        // --- 第一步：先规划地板位置 ---
        // 排序：按当前位置排序，尽量保持玩家预想的布局
        System.Array.Sort(selected, (a, b) => a.transform.position.sqrMagnitude.CompareTo(b.transform.position.sqrMagnitude));

        List<GridNode> nodesToProcess = new List<GridNode>();

        foreach (var obj in selected) {
            GridNode node = obj.GetComponent<GridNode>();
            if (!node) continue;
            nodesToProcess.Add(node);

            Undo.RecordObject(obj.transform, "Snap Tile");

            // 计算地板理想网格位
            Vector2Int tileGridPos = GetGridPos(obj.transform.position);

            // 如果该位被占了，地板之间互相挤开
            if (occupiedSlots.Contains(tileGridPos)) {
                tileGridPos = FindEmptySlot(tileGridPos, occupiedSlots);
            }
            
            // 记录地板占用
            occupiedSlots.Add(tileGridPos);
            obj.transform.position = GridToWorld(tileGridPos);
        }

        // --- 第二步：在地板周围生成/规划蓝色选框 ---
        foreach (var node in nodesToProcess) {
            if (node.buildingAnchor == null) continue;

            Undo.RecordObject(node.buildingAnchor, "Snap Anchor");

            // 获取当前地板的网格坐标
            Vector2Int tileGridPos = GetGridPos(node.transform.position);

            // 寻找周围可以放选框的邻居位 (上下左右)
            Vector2Int anchorGridPos = FindBestNeighborSlot(tileGridPos, occupiedSlots);

            // 记录选框占用，防止下一个选框叠上来
            occupiedSlots.Add(anchorGridPos);

            // 设置选框世界坐标
            node.buildingAnchor.position = GridToWorld(anchorGridPos);
            
            // 高度微调（略高于地板防止闪烁）
            Vector3 lp = node.buildingAnchor.localPosition;
            lp.y = 0.2f; 
            node.buildingAnchor.localPosition = lp;
        }

        Debug.Log("【初始化完成】已优先排布地板，并在空余位置挤开了蓝色选框。");
    }

    // 坐标转换辅助
    private Vector2Int GetGridPos(Vector3 pos) => new Vector2Int(Mathf.RoundToInt(pos.x / gridStep), Mathf.RoundToInt(pos.z / gridStep));
    private Vector3 GridToWorld(Vector2Int gPos) => new Vector3(gPos.x * gridStep, 0, gPos.y * gridStep);

    // 寻找邻居空位逻辑：优先上下左右，如果都满了，就找更远一点的
    private Vector2Int FindBestNeighborSlot(Vector2Int center, HashSet<Vector2Int> occupied) {
        // 优先顺序：前、后、左、右
        Vector2Int[] neighbors = {
            center + new Vector2Int(0, 1),
            center + new Vector2Int(0, -1),
            center + new Vector2Int(1, 0),
            center + new Vector2Int(-1, 0)
        };

        foreach (var pos in neighbors) {
            if (!occupied.Contains(pos)) return pos;
        }

        // 如果上下左右都满了，调用更广范围的搜索
        return FindEmptySlot(center, occupied);
    }

    // 螺旋算法：寻找最近的完全空白格
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

    // --- 以下保持 Link 和 Slots 逻辑不变 ---
    private void LinkNodes() {
        GridNode[] allNodes = GameObject.FindObjectsOfType<GridNode>();
        foreach (var nA in allNodes) {
            Undo.RecordObject(nA, "Link Nodes");
            System.Array.Clear(nA.connections, 0, nA.connections.Length);
            foreach (var nB in allNodes) {
                if (nA == nB) continue;
                Vector3 diff = nB.transform.position - nA.transform.position;
                if (diff.magnitude > gridStep * 1.1f) continue;
                int dir = Mathf.Abs(diff.x) > Mathf.Abs(diff.z) ? (diff.x > 0 ? 1 : 3) : (diff.z > 0 ? 0 : 2);
                nA.connections[dir] = nB;
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
                if (!s) {
                    s = new GameObject(sName).transform;
                    s.SetParent(obj.transform);
                }
                s.localPosition = new Vector3(((i % 3) - 1f) * slotOffset, 0.1f, ((i / 3) == 0 ? 0.3f : -0.3f));
                node.slotPoints[i] = s;
            }
            EditorUtility.SetDirty(node);
        }
    }

    private void SyncAllToDatabase() {
        if (db == null) return;
        Undo.RecordObject(db, "Sync Database");
        db.allGrids.Clear();
        GridNode[] allNodes = GameObject.FindObjectsOfType<GridNode>();
        int currentId = 0;
        foreach (var n in allNodes) {
            n.gridId = currentId;
            db.allGrids.Add(new GridDatabase.GridEntry { id = currentId, node = n });
            EditorUtility.SetDirty(n);
            currentId++;
        }
        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();
    }
}