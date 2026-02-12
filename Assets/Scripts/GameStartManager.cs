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

        // 判断是否有加载数据
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
        gridDatabase.RefreshCache();
    }

    void CreatePlayerInstances() {
        for (int i = 1; i <= 6; i++) {
            GameObject go = Instantiate(playerPrefab);
            PlayerController pc = go.GetComponent<PlayerController>();
            pc.playerId = i;
            players.Add(pc);
        }
    }

    void InitPlayersToStart() {
        GridNode startNode = startGrid.GetComponent<GridNode>();
        foreach (var p in players) {
            p.currentGrid = startNode;
            p.SetInitialPosition(startNode);
        }
    }

    IEnumerator MainFlow() {
        UIManager.Instance.UpdateStatus("准备决定本局游戏顺序...");
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

    // 从小游戏返回时执行的逻辑
    void ToTurnManagerFromLoad() {
        // 直接调用 TurnManager 的洗牌排序方法
        TurnManager.Instance.BeginGameFromMinigame(players);
        gameObject.SetActive(false);
    }
}