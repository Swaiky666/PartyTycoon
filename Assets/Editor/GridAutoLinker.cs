using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

public class GridAutoLinker : EditorWindow {
    private float gridStep = 2.0f;
    private float slotOffset = 0.5f;
    private GridDatabase db;

    [MenuItem("工具/棋盘全自动配置工具")]
    public static void ShowWindow() {
        GridAutoLinker window = GetWindow<GridAutoLinker>("全自动工具");
        window.AutoFindDatabase(); // 窗口打开时自动寻找
    }

    private void OnEnable() {
        AutoFindDatabase(); // 脚本加载时自动寻找
    }

    private void OnGUI() {
        GUILayout.Label("1. 基础配置 (自动同步中)", EditorStyles.boldLabel);
        
        // 如果还是没找到，提供手动选择框以防万一
        db = (GridDatabase)EditorGUILayout.ObjectField("当前 ID 存储器", db, typeof(GridDatabase), false);
        
        if (db == null) {
            if (GUILayout.Button("手动搜索或重新创建数据库")) {
                AutoFindDatabase();
            }
            EditorGUILayout.HelpBox("正在尝试定位 GridDatabase...", MessageType.Info);
        }

        gridStep = EditorGUILayout.FloatField("砖块对齐尺寸", gridStep);
        slotOffset = EditorGUILayout.FloatField("站位点间隔", slotOffset);

        EditorGUILayout.Space();
        GUILayout.Label("2. 执行流程", EditorStyles.boldLabel);

        if (db != null) {
            if (GUILayout.Button("第一步：对齐位置 (Snap)")) SnapNodes();
            if (GUILayout.Button("第二步：建立双向连线 (Link)")) LinkNodes();
            if (GUILayout.Button("第三步：管理站位 Slot")) ManageSlots();
            
            GUI.color = Color.green;
            if (GUILayout.Button("第四步：智能分配 ID")) ManageIds();
            GUI.color = Color.white;
        }
    }

    // 核心自动寻找/创建逻辑
    private void AutoFindDatabase() {
        if (db != null) return;

        // 1. 在工程中搜索类型为 GridDatabase 的资源
        string[] guids = AssetDatabase.FindAssets("t:GridDatabase");
        
        if (guids.Length > 0) {
            // 2. 如果找到了，加载第一个
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            db = AssetDatabase.LoadAssetAtPath<GridDatabase>(path);
            Debug.Log($"[工具] 已自动关联现有的数据库: {path}");
        } else {
            // 3. 如果没找到，自动创建一个
            CreateNewDatabase();
        }
    }

    private void CreateNewDatabase() {
        GridDatabase asset = ScriptableObject.CreateInstance<GridDatabase>();
        string path = "Assets/GridDatabase.asset";
        
        // 确保文件名唯一，不覆盖其他资源
        path = AssetDatabase.GenerateUniqueAssetPath(path);
        
        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();
        db = asset;
        Debug.Log($"[工具] 未检测到数据库，已自动创建: {path}");
    }

    // --- 以下功能逻辑保持不变 ---

    private void ManageIds() {
        if (db == null) return;
        GameObject[] selected = Selection.gameObjects;
        db.CleanUp();
        int maxId = db.allGrids.Count > 0 ? db.allGrids.Max(g => g.id) : 0;

        foreach (var obj in selected) {
            GridNode node = obj.GetComponent<GridNode>();
            if (!node) continue;

            bool isDuplicate = db.allGrids.Any(g => g.id == node.gridId && g.node != node);
            if (node.gridId <= 0 || isDuplicate) {
                maxId++;
                node.gridId = maxId;
            }

            var entry = db.allGrids.Find(g => g.node == node);
            if (entry != null) entry.id = node.gridId;
            else db.allGrids.Add(new GridDatabase.GridEntry { id = node.gridId, node = node });
            
            EditorUtility.SetDirty(node);
        }
        db.allGrids.Sort((a, b) => a.id.CompareTo(b.id));
        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();
        Debug.Log("ID 分配与同步完成！");
    }

    private void SnapNodes() {
        foreach (var obj in Selection.gameObjects) {
            Vector3 p = obj.transform.position;
            p.x = Mathf.Round(p.x / gridStep) * gridStep;
            p.z = Mathf.Round(p.z / gridStep) * gridStep;
            obj.transform.position = p;
        }
    }

    private void LinkNodes() {
        GameObject[] selected = Selection.gameObjects;
        foreach (var a in selected) {
            GridNode nA = a.GetComponent<GridNode>();
            if (!nA) continue;
            foreach (var b in selected) {
                if (a == b) continue;
                GridNode nB = b.GetComponent<GridNode>();
                if (!nB) continue;
                Vector3 diff = b.transform.position - a.transform.position;
                if (diff.magnitude > gridStep * 1.1f) continue;
                int dir = Mathf.Abs(diff.x) > Mathf.Abs(diff.z) ? (diff.x > 0 ? 1 : 3) : (diff.z > 0 ? 0 : 2);
                nA.connections[dir] = nB;
                EditorUtility.SetDirty(nA);
            }
        }
    }

    private void ManageSlots() {
        foreach (var obj in Selection.gameObjects) {
            GridNode node = obj.GetComponent<GridNode>();
            if (!node) continue;
            for (int i = 0; i < 6; i++) {
                string sName = "Slot_" + (i + 1);
                Transform s = obj.transform.Find(sName);
                if (!s) {
                    s = new GameObject(sName).transform;
                    s.SetParent(obj.transform);
                    s.localPosition = new Vector3(((i % 3) - 1f) * slotOffset, 0.1f, ((i / 3) - 0.5f) * slotOffset);
                }
                node.slotPoints[i] = s;
            }
            EditorUtility.SetDirty(node);
        }
    }
}