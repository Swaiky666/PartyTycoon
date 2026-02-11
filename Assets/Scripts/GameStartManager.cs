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

        if (GameDataManager.Instance.savedGrids.Count > 0) {
            GameDataManager.Instance.LoadGameState(players);
            ToTurnManagerFromLoad();
        } else {
            InitPlayersToStart();
            StartCoroutine(MainFlow());
        }
    }

    void InitMapBuildings() {
        if (gridDatabase == null) return;
        foreach (var entry in gridDatabase.allGrids) {
            GridNode node = entry.node;
            if (node == null || node.buildingAnchor == null || node.currentBuilding != null) continue;

            GameObject prefab = GameDataManager.Instance.GetFixedBuildingPrefab(node.type);
            if (prefab != null) {
                GameObject building = Instantiate(prefab, node.buildingAnchor.position, node.buildingAnchor.rotation);
                building.transform.SetParent(node.buildingAnchor); 
                node.currentBuilding = building;
            }
        }
    }

    void CreatePlayerInstances() {
        for (int i = 0; i < 6; i++) {
            GameObject pObj = Instantiate(playerPrefab);
            PlayerController pc = pObj.GetComponent<PlayerController>();
            pc.playerId = i + 1;
            players.Add(pc);
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
        TurnManager.Instance.BeginGame(players);
        gameObject.SetActive(false);
    }
}