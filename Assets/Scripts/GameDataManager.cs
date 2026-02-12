using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.SceneManagement;

[System.Serializable]
public class PlayerState {
    public int playerId;
    public int currentGridId;
    public int lastGridId; // 新增：记录上一个格子的ID，用于防回退逻辑
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

    [Header("小游戏池配置")]
    public List<string> minigameScenes = new List<string>(); 

    [Header("通用资源预制体")]
    public List<CardBase> allPossibleCards = new List<CardBase>();
    public GameObject barricadePrefab;
    public GameObject housePrefab;
    public GameObject iceEffectPrefab;
    public GameObject smokeEffectPrefab; 

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

    public void SwitchToRandomMinigame(List<PlayerController> players) {
        SaveGameState(players);
        if (minigameScenes.Count > 0) {
            int randomIndex = Random.Range(0, minigameScenes.Count);
            string targetScene = minigameScenes[randomIndex];
            SceneManager.LoadScene(targetScene);
        } else {
            Debug.LogError("【系统】请在 GameDataManager 的 Inspector 中配置小游戏场景名！");
        }
    }

    public void SaveGameState(List<PlayerController> players) {
        savedPlayers.Clear();
        foreach (var p in players) {
            PlayerState state = new PlayerState();
            state.playerId = p.playerId;
            state.currentGridId = (p.currentGrid != null) ? p.currentGrid.gridId : -1;
            // 记录 lastGrid 的 ID
            state.lastGridId = (p.GetLastGrid() != null) ? p.GetLastGrid().gridId : -1;
            state.money = p.money;
            state.freezeTurns = p.remainingFreezeTurns;
            state.rotation = p.transform.rotation;
            state.cardNames = new List<string>();
            foreach (var card in p.cards) {
                if (card != null) state.cardNames.Add(card.cardName);
            }
            savedPlayers.Add(state);
        }

        savedGrids.Clear();
        foreach (var node in database.GetAllNodes()) {
            savedGrids.Add(node.GetCurrentState());
        }
    }

    public void LoadGameState(List<PlayerController> players) {
        if (savedPlayers.Count == 0 || database == null) return;

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
                p.transform.rotation = state.rotation;

                p.cards.Clear();
                foreach (string cName in state.cardNames) {
                    CardBase refCard = allPossibleCards.Find(c => c.cardName == cName);
                    if (refCard != null) p.cards.Add(Instantiate(refCard));
                }

                // 恢复当前格子
                GridNode currNode = database.GetGridById(state.currentGridId);
                if (currNode != null) {
                    p.currentGrid = currNode;
                    p.SetInitialPosition(currNode);
                }

                // 核心修复：恢复上一个格子，确保防回退逻辑生效
                if (state.lastGridId != -1) {
                    GridNode lastNode = database.GetGridById(state.lastGridId);
                    p.SetLastGrid(lastNode);
                } else {
                    p.SetLastGrid(null);
                }

                if (state.freezeTurns > 0 && iceEffectPrefab != null) {
                    p.ApplyFreeze(state.freezeTurns, iceEffectPrefab);
                }
            }
        }
        ClearCache();
    }

    public void ClearCache() {
        savedPlayers.Clear();
        savedGrids.Clear();
    }
}