using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.SceneManagement;

[System.Serializable]
public class PlayerState {
    public int playerId;
    public int currentGridId;
    public int money;
    public List<string> cardNames = new List<string>();
    public int freezeTurns; 
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
    
    [Header("配置参数")]
    public float dropHeight = 15f;       

    void Awake() {
        if (Instance == null) { 
            Instance = this; 
            DontDestroyOnLoad(gameObject); 
            SyncDatabaseOnStart();
        } else {
            Destroy(gameObject);
        }
    }

    public void SyncDatabaseOnStart() {
        if (database == null) return;
        if (database.allGrids == null || database.allGrids.Count == 0) {
            GridNode[] allNodes = Object.FindObjectsByType<GridNode>(FindObjectsSortMode.None);
            database.allGrids.Clear();
            foreach (var node in allNodes) {
                database.allGrids.Add(new GridDatabase.GridEntry { id = node.gridId, node = node });
            }
        }
    }

    public GameObject GetFixedBuildingPrefab(GridType type) {
        switch (type) {
            case GridType.Shop: return shopPrefab;
            case GridType.Hospital: return hospitalPrefab;
            case GridType.Bank: return bankPrefab;
            case GridType.Prison: return prisonPrefab;
            default: return null;
        }
    }

    // 保存并跳转场景
    public void SwitchToMinigame(string sceneName, List<PlayerController> currentPlayers) {
        SaveGameState(currentPlayers);
        SceneManager.LoadScene(sceneName);
    }

    public void SaveGameState(List<PlayerController> players) {
        savedPlayers.Clear();
        foreach (var p in players) {
            savedPlayers.Add(new PlayerState {
                playerId = p.playerId,
                currentGridId = p.currentGrid != null ? p.currentGrid.gridId : -1,
                money = p.money,
                cardNames = p.cards.Select(c => c.cardName).ToList(),
                freezeTurns = p.remainingFreezeTurns
            });
        }

        savedGrids.Clear();
        List<GridNode> allNodes = database.GetAllNodes();
        foreach (var node in allNodes) {
            if (node != null) {
                savedGrids.Add(node.GetCurrentState());
            }
        }
        Debug.Log($"【系统】数据已存入缓存，准备切换场景。");
    }

    public void LoadGameState(List<PlayerController> players) {
        if (savedPlayers.Count == 0 || database == null) {
            Debug.LogWarning("加载数据失败：没有可用的保存快照。");
            return;
        }

        foreach (var state in savedPlayers) {
            PlayerController p = players.Find(x => x.playerId == state.playerId);
            if (p != null) {
                p.money = state.money;
                p.remainingFreezeTurns = state.freezeTurns;
                
                GridNode node = database.GetGridById(state.currentGridId);
                if (node != null) p.SetInitialPosition(node);
                
                p.cards.Clear();
                foreach (string cName in state.cardNames) {
                    CardBase refCard = allPossibleCards.Find(c => c.cardName == cName);
                    if (refCard != null) p.cards.Add(Instantiate(refCard));
                }
                
                if (state.freezeTurns > 0 && iceEffectPrefab != null) {
                    p.ApplyFreeze(state.freezeTurns, iceEffectPrefab);
                }
            }
        }

        foreach (var gState in savedGrids) {
            GridNode node = database.GetGridById(gState.gridId);
            if (node != null) {
                node.ApplyState(gState, players, housePrefab, barricadePrefab);
            }
        }
        Debug.Log("【系统】场景数据已根据快照完全恢复。");
    }
}