using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

[System.Serializable]
public class PlayerState {
    public int playerId;
    public int currentGridId;
    public int lastGridId; 
    public int money;
    public List<string> cardNames = new List<string>();
    public int freezeTurns; 
    public Quaternion rotation; 
}

[System.Serializable]
public class GridState {
    public int gridId;
    public int ownerId;
    public bool hasBarricade;
    public bool hasHouse;
}

public class GameDataManager : MonoBehaviour {
    public static GameDataManager Instance;

    [Header("核心数据库引用")]
    public GridDatabase database; 

    [Header("快照数据")]
    public List<PlayerState> savedPlayers = new List<PlayerState>();
    public List<GridState> savedGrids = new List<GridState>();

    [Header("通用资源预制体")]
    public List<CardBase> allPossibleCards = new List<CardBase>();
    public GameObject barricadePrefab;
    public GameObject housePrefab;
    public GameObject iceEffectPrefab;
    public GameObject smokeEffectPrefab; 

    [Header("地图固定建筑预制体")]
    public GameObject shopPrefab;         
    public GameObject hospitalPrefab;     
    public GameObject bankPrefab;         
    public GameObject prisonPrefab;       

    [Header("动画配置")]
    public float dropHeight = 15f; 

    void Awake() {
        if (Instance == null) {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        } else {
            Destroy(gameObject);
            return;
        }
    }

    public void SaveGameState(List<PlayerController> players) {
        savedPlayers.Clear();
        savedGrids.Clear();

        if (database != null) {
            List<GridNode> nodes = database.GetAllNodes();
            foreach (var node in nodes) {
                savedGrids.Add(node.GetCurrentState());
            }
        }

        foreach (var p in players) {
            PlayerState s = new PlayerState {
                playerId = p.playerId,
                currentGridId = p.currentGrid != null ? p.currentGrid.gridId : -1,
                lastGridId = p.GetLastGrid() != null ? p.GetLastGrid().gridId : -1,
                money = p.money,
                freezeTurns = p.remainingFreezeTurns,
                rotation = p.transform.rotation
            };
            foreach (var card in p.cards) s.cardNames.Add(card.cardName);
            savedPlayers.Add(s);
        }
        Debug.Log("【系统】数据已存入快照。");
    }

    public void LoadGameState(List<PlayerController> players) {
        if (savedPlayers.Count == 0 || database == null) return;

        database.RefreshCache();
        List<GridNode> allNodes = database.GetAllNodes();

        // --- 核心修复：首先恢复/生成所有固定地图建筑 ---
        foreach (GridNode node in allNodes) {
            // 如果该地块是固定功能型且目前没有模型，则生成
            if (node.currentBuilding == null && node.buildingAnchor != null) {
                GameObject prefabToSpawn = null;
                switch (node.type) {
                    case GridType.Shop: prefabToSpawn = shopPrefab; break;
                    case GridType.Bank: prefabToSpawn = bankPrefab; break;
                    case GridType.Hospital: prefabToSpawn = hospitalPrefab; break;
                    case GridType.Prison: prefabToSpawn = prisonPrefab; break;
                }
                if (prefabToSpawn != null) {
                    GameObject b = Instantiate(prefabToSpawn, node.buildingAnchor.position, node.buildingAnchor.rotation);
                    b.transform.SetParent(node.buildingAnchor);
                    node.currentBuilding = b;
                }
            }
        }

        // --- 恢复动态地块状态（房子、路障、归属权） ---
        foreach (var gState in savedGrids) {
            GridNode node = database.GetGridById(gState.gridId);
            if (node != null) {
                node.ApplyState(gState, players, housePrefab, barricadePrefab);
            }
        }

        // --- 恢复玩家状态 ---
        foreach (var state in savedPlayers) {
            PlayerController p = players.Find(x => x.playerId == state.playerId);
            if (p != null) {
                p.money = state.money;
                p.remainingFreezeTurns = state.freezeTurns;
                p.transform.rotation = state.rotation;
                
                p.cards.Clear();
                foreach (string cName in state.cardNames) {
                    CardBase refCard = allPossibleCards.Find(c => c.cardName == cName);
                    if (refCard != null) p.cards.Add(Instantiate(refCard));
                }

                GridNode currNode = database.GetGridById(state.currentGridId);
                if (currNode != null) {
                    p.currentGrid = currNode;
                    Vector3 targetPos = currNode.GetSlotPosition(p.gameObject);
                    targetPos.y += p.heightOffset;
                    p.transform.position = targetPos;
                }

                if (state.lastGridId != -1) p.SetLastGrid(database.GetGridById(state.lastGridId));
                if (state.freezeTurns > 0 && iceEffectPrefab != null) p.ApplyFreeze(state.freezeTurns, iceEffectPrefab);
            }
        }
        
        Debug.Log("【系统】数据及地图建筑恢复完成。");
        ClearCache();
    }

    public void ClearCache() {
        savedPlayers.Clear();
        savedGrids.Clear();
    }

    public void SwitchToRandomMinigame(List<PlayerController> currentPlayers) {
        SaveGameState(currentPlayers);
        SceneManager.LoadScene("MinigameScene"); 
    }
}