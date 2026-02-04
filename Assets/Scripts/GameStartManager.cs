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
        if (gridDatabase) gridDatabase.CleanUp();
        InitPlayers();
        StartCoroutine(MainFlow());
    }

    void InitPlayers() {
        GridNode node = startGrid.GetComponent<GridNode>();
        for (int i = 0; i < 6; i++) {
            GameObject pObj = Instantiate(playerPrefab);
            PlayerController pc = pObj.GetComponent<PlayerController>();
            pc.playerId = i + 1;
            pc.SetInitialPosition(node);
            players.Add(pc);
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
        yield return StartCoroutine(diceAnimator.PlayRollSequence(result, () => {
            rollResults[1] = result;
            CheckStatus();
        }));
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
            var sorted = players.OrderByDescending(p => rollResults[p.playerId]).ToList();
            UIManager.Instance.UpdateStatus("顺序已定，游戏开始！");
            Invoke("ToTurnManager", 2f);
        } else {
            UIManager.Instance.UpdateStatus($"等待投掷... ({rollResults.Count}/6)");
        }
    }

    void ToTurnManager() {
        var sorted = players.OrderByDescending(p => rollResults[p.playerId]).ToList();
        TurnManager.Instance.BeginGame(sorted);
        this.gameObject.SetActive(false);
    }
}