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
        
        InitMapBuildings();
        CreatePlayerInstances();

        if (GameDataManager.Instance != null && GameDataManager.Instance.savedPlayers.Count > 0) {
            GameDataManager.Instance.LoadGameState(players);
            ToTurnManagerFromLoad(); 
        } else {
            InitPlayersToStart();
            StartCoroutine(MainFlow());
        }
    }

    void InitMapBuildings() {
        if (gridDatabase == null) return;
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
                GameObject go = Instantiate(prefabToSpawn, node.buildingAnchor.position, node.buildingAnchor.rotation);
                go.transform.SetParent(node.buildingAnchor);
                node.currentBuilding = go;
            }
        }
        Debug.Log("【系统】数据库刷新完成，固定建筑已生成。");
    }

    void CreatePlayerInstances() {
        for (int i = 1; i <= 6; i++) {
            GameObject go = Instantiate(playerPrefab);
            PlayerController p = go.GetComponent<PlayerController>();
            p.playerId = i;
            players.Add(p);
        }
    }

    void InitPlayersToStart() {
        GridNode startNode = startGrid.GetComponent<GridNode>();
        foreach (var p in players) p.SetInitialPosition(startNode);
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
            yield return new WaitForSeconds(Random.Range(0.3f, 0.7f));
            rollResults[i] = Random.Range(1, 7);
            CheckStatus();
        }
    }

    void CheckStatus() {
        if (rollResults.Count >= 6) {
            UIManager.Instance.UpdateStatus("顺序已定，正式开始！");
            Invoke("ToTurnManager", 2f);
        }
    }

    void ToTurnManager() {
        var sorted = players.OrderByDescending(p => rollResults[p.playerId]).ToList();
        TurnManager.Instance.BeginGame(sorted);
        gameObject.SetActive(false);
    }

    void ToTurnManagerFromLoad() {
        var sorted = players.OrderBy(p => p.playerId).ToList();
        TurnManager.Instance.BeginGame(sorted);
        gameObject.SetActive(false);
    }
}