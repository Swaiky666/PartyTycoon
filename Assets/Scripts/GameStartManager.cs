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
        
        // 1. 无论是否加载，先尝试执行一次基础建筑生成
        InitMapBuildings();
        
        // 2. 实例化玩家 GameObject
        CreatePlayerInstances();

        // 3. 检查存档
        if (GameDataManager.Instance != null && GameDataManager.Instance.savedPlayers.Count > 0) {
            // 在 LoadGameState 内部现在也会检查并生成缺失的建筑
            GameDataManager.Instance.LoadGameState(players);
            ToTurnManagerFromLoad(); 
        } else {
            InitPlayersToStart();
            StartCoroutine(MainFlow());
        }
    }

    void InitMapBuildings() {
        if (gridDatabase == null) return;
        gridDatabase.RefreshCache();
        List<GridNode> allNodes = gridDatabase.GetAllNodes();
        foreach (GridNode node in allNodes) {
            if (node.currentBuilding != null) continue;
            
            GameObject prefabToSpawn = null;
            switch (node.type) {
                case GridType.Shop: prefabToSpawn = GameDataManager.Instance.shopPrefab; break;
                case GridType.Bank: prefabToSpawn = GameDataManager.Instance.bankPrefab; break;
                case GridType.Hospital: prefabToSpawn = GameDataManager.Instance.hospitalPrefab; break;
                case GridType.Prison: prefabToSpawn = GameDataManager.Instance.prisonPrefab; break;
            }
            
            if (prefabToSpawn != null && node.buildingAnchor != null) {
                GameObject b = Instantiate(prefabToSpawn, node.buildingAnchor.position, node.buildingAnchor.rotation);
                b.transform.SetParent(node.buildingAnchor);
                node.currentBuilding = b;
            }
        }
    }

    void CreatePlayerInstances() {
        for (int i = 1; i <= 6; i++) {
            GameObject pObj = Instantiate(playerPrefab);
            PlayerController pc = pObj.GetComponent<PlayerController>();
            pc.playerId = i;
            players.Add(pc);
        }
    }

    void InitPlayersToStart() {
        GridNode startNode = startGrid.GetComponent<GridNode>();
        foreach (var p in players) {
            p.currentGrid = startNode;
            Vector3 pos = startNode.GetSlotPosition(p.gameObject);
            pos.y += p.heightOffset;
            p.transform.position = pos;
        }
    }

    IEnumerator MainFlow() {
        UIManager.Instance.UpdateStatus("决定本局游戏顺序...");
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
            yield return new WaitForSeconds(Random.Range(0.2f, 0.5f));
            rollResults[i] = Random.Range(1, 7);
            CheckStatus();
        }
    }

    void CheckStatus() {
        if (rollResults.Count >= 6) {
            UIManager.Instance.UpdateStatus("顺序已定，正式开始！");
            Invoke("ToTurnManager", 1.5f);
        }
    }

    void ToTurnManager() {
        var sorted = players.OrderByDescending(p => rollResults[p.playerId]).ToList();
        TurnManager.Instance.BeginGame(sorted);
        gameObject.SetActive(false);
    }

    void ToTurnManagerFromLoad() {
        TurnManager.Instance.BeginGameFromMinigame(players);
        gameObject.SetActive(false);
    }
}