using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.SceneManagement;

// --- 必须保留这些类定义，否则会报 CS0246 错误 ---
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
        } else {
            Destroy(gameObject);
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

    public void SwitchToMinigame(string sceneName, List<PlayerController> players) {
        SaveGameState(players);
        SceneManager.LoadScene(sceneName);
    }

    public void SaveGameState(List<PlayerController> players) {
        if (database != null) database.RefreshCache();

        savedPlayers.Clear();
        savedGrids.Clear();

        if (players == null || players.Count == 0) {
            Debug.LogError("【系统】保存失败：玩家列表为空！");
            return;
        }

        foreach (var p in players) {
            PlayerState s = new PlayerState {
                playerId = p.playerId,
                money = p.money,
                freezeTurns = p.remainingFreezeTurns,
                currentGridId = p.currentGrid != null ? p.currentGrid.gridId : -1,
                cardNames = p.cards.Select(c => c.cardName).ToList()
            };
            savedPlayers.Add(s);
        }

        GridNode[] allSceneNodes = GameObject.FindObjectsOfType<GridNode>();
        foreach (var node in allSceneNodes) {
            if (node.gridId != -1) {
                savedGrids.Add(node.GetCurrentState());
            }
        }
        Debug.Log($"【系统】数据存入缓存完成：玩家 {savedPlayers.Count}，地块 {savedGrids.Count}。");
    }

    public void LoadGameState(List<PlayerController> players) {
        if (database != null) database.RefreshCache();

        if (savedPlayers.Count == 0 || database == null) {
            Debug.LogWarning("【系统】没有找到存档数据。");
            return;
        }

        foreach (var gState in savedGrids) {
            GridNode node = database.GetGridById(gState.gridId);
            if (node != null) {
                node.ApplyState(gState, players, housePrefab, barricadePrefab);
            }
        }

        foreach (var state in savedPlayers) {
            PlayerController p = players.Find(x => x.playerId == state.playerId);
            if (p != null) {
                p.money = state.money;
                p.remainingFreezeTurns = state.freezeTurns;
                
                p.cards.Clear();
                foreach (string cName in state.cardNames) {
                    CardBase refCard = allPossibleCards.Find(c => c.cardName == cName);
                    if (refCard != null) p.cards.Add(Instantiate(refCard));
                }

                GridNode node = database.GetGridById(state.currentGridId);
                if (node != null) {
                    p.currentGrid = node;
                    p.SetInitialPosition(node);
                }

                if (state.freezeTurns > 0 && iceEffectPrefab != null) {
                    p.ApplyFreeze(state.freezeTurns, iceEffectPrefab);
                }
            }
        }

        Debug.Log("【系统】数据恢复成功！");
        ClearCache();
    }

    public void ClearCache() {
        savedPlayers.Clear();
        savedGrids.Clear();
    }
}