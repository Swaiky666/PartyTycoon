using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class GameStartManager : MonoBehaviour {
    public GridDatabase gridDatabase;
    public GameObject playerPrefab;
    public Transform startGrid;
    public DiceAnimator diceAnimator; 

    private List<PlayerController> players = new List<PlayerController>();
    private Dictionary<int, int> rollResults = new Dictionary<int, int>();

    void Start() {
        UIManager.Instance.SetExtraButtonsVisible(false);
        UIManager.Instance.SetPlayerStatsVisible(false);
        
        // 1. 初始化地图（现在使用新数据库的扫描方法）
        InitMapBuildings();
        
        // 2. 生成玩家实例
        CreatePlayerInstances();

        // 3. 核心逻辑：判断是新游戏还是从小游戏返回
        if (GameDataManager.Instance != null && GameDataManager.Instance.savedPlayers.Count > 0) {
            // 加载存档：这会设置玩家的 currentGrid 并归位
            GameDataManager.Instance.LoadGameState(players);
            
            // 加载后直接进入回合管理
            ToTurnManagerFromLoad(); 
        } else {
            // 只有新游戏才跑初始定位和开场流程
            InitPlayersToStart();
            StartCoroutine(MainFlow());
        }
    }

    void InitMapBuildings() {
        if (gridDatabase == null) return;

        // 核心修复：调用 GetAllNodes() 替代已删除的 allGrids
        List<GridNode> allNodes = gridDatabase.GetAllNodes();
        
        foreach (GridNode node in allNodes) {
            if (node == null || node.buildingAnchor == null || node.currentBuilding != null) continue;

            // 根据地块类型（商店、银行等）生成固定建筑
            GameObject prefab = GameDataManager.Instance.GetFixedBuildingPrefab(node.type);
            if (prefab != null) {
                GameObject building = Instantiate(prefab, node.buildingAnchor.position, node.buildingAnchor.rotation);
                building.transform.SetParent(node.buildingAnchor); 
                node.currentBuilding = building;
            }
        }
    }

    void CreatePlayerInstances() {
        for (int i = 1; i <= 6; i++) {
            GameObject go = Instantiate(playerPrefab);
            PlayerController p = go.GetComponent<PlayerController>();
            p.playerId = i;
            players.Add(p);
            UIManager.Instance.UpdatePlayerStats(p);
        }
    }

    void InitPlayersToStart() {
        if (startGrid == null) return;
        GridNode node = startGrid.GetComponent<GridNode>();
        foreach (var p in players) {
            p.SetInitialPosition(node);
        }
    }

    IEnumerator MainFlow() {
        UIManager.Instance.UpdateStatus("准备决定顺序...");
        yield return new WaitForSeconds(1.0f);
        diceAnimator.ShowDice(true);
        diceAnimator.ShowAndIdle();
        
        UIManager.Instance.ShowActionButton("决定顺序", () => {
            UIManager.Instance.HideActionButton();
            StartCoroutine(HandleLocalRoll());
            StartCoroutine(SimulateOthersRoll());
        });
    }

    IEnumerator HandleLocalRoll() {
        int result = Random.Range(1, 7);
        yield return StartCoroutine(diceAnimator.PlayRollSequence(result, null));
        rollResults[1] = result;
        CheckStatus();
    }

    IEnumerator SimulateOthersRoll() {
        for (int i = 2; i <= 6; i++) {
            yield return new WaitForSeconds(Random.Range(0.5f, 1.2f));
            rollResults[i] = Random.Range(1, 7);
            CheckStatus();
        }
    }

    void CheckStatus() {
        if (rollResults.Count >= 6) {
            UIManager.Instance.UpdateStatus("顺序已定，游戏开始！");
            Invoke("ToTurnManager", 2f);
        }
    }

    void ToTurnManager() {
        var sorted = players.OrderByDescending(p => rollResults[p.playerId]).ToList();
        TurnManager.Instance.BeginGame(sorted);
        gameObject.SetActive(false);
    }

    void ToTurnManagerFromLoad() {
        // 从存档加载时，默认按玩家ID顺序开始
        TurnManager.Instance.BeginGame(players);
        gameObject.SetActive(false);
    }
}