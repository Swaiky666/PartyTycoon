using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;

public class GameStartManager : MonoBehaviour {
    public static GameStartManager Instance;

    [Header("基础配置")]
    public GridDatabase gridDatabase;
    public GameObject playerPrefab;
    public Transform startGrid;

    [Header("UI & 表现")]
    public TextMeshProUGUI statusText; 
    public GameObject rollButton;         
    public DiceAnimator diceAnimator; 

    private List<PlayerController> players = new List<PlayerController>();
    private Dictionary<int, int> rollResults = new Dictionary<int, int>();
    private int localPlayerId = 1; 
    private bool hasLocalRolled = false;

    void Awake() { Instance = this; }

    void Start() {
        if (gridDatabase != null) gridDatabase.CleanUp();
        InitPlayers();
        
        if (rollButton) rollButton.SetActive(false); 
        StartCoroutine(MainFlow());
    }

    IEnumerator MainFlow() {
        if (statusText) statusText.text = "准备棋盘中...";
        yield return new WaitForSeconds(1.0f);
        
        // 骰子出现并慢转
        if (diceAnimator) {
            diceAnimator.ShowAndIdle();
        }

        if (statusText) statusText.text = "请投掷骰子决定顺序！";
        if (rollButton) rollButton.SetActive(true);
    }

    public void OnClickRollDice() {
        if (hasLocalRolled) return;
        hasLocalRolled = true;
        if (rollButton) rollButton.SetActive(false);
        
        StartCoroutine(HandleLocalRoll());
        StartCoroutine(SimulateOthersRoll());
    }

    IEnumerator HandleLocalRoll() {
        int result = Random.Range(1, 7);
        // 调用快转到锁定的序列
        yield return StartCoroutine(diceAnimator.PlayRollSequence(result, () => {
            rollResults[localPlayerId] = result;
        }));
        CheckFinalStatus();
    }

    IEnumerator SimulateOthersRoll() {
        for (int i = 2; i <= 6; i++) {
            yield return new WaitForSeconds(Random.Range(0.5f, 2.0f));
            rollResults[i] = Random.Range(1, 7);
            CheckFinalStatus();
        }
    }

    void CheckFinalStatus() {
        if (rollResults.Count >= 6) {
            SortAndFinish();
        } else {
            if (statusText) statusText.text = $"等待其他玩家投掷... ({rollResults.Count}/6)";
        }
    }

    void SortAndFinish() {
        var sorted = players.OrderByDescending(p => rollResults[p.playerId])
                            .ThenBy(p => p.playerId).ToList();

        string resMsg = "<b>顺序锁定：</b>\n";
        for (int i = 0; i < sorted.Count; i++) {
            resMsg += $"{i+1}. 玩家 {sorted[i].playerId} ";
        }
        if (statusText) statusText.text = resMsg;
    }

    void InitPlayers() {
        GridNode node = startGrid.GetComponent<GridNode>();
        for (int i = 0; i < 6; i++) {
            GameObject p = Instantiate(playerPrefab);
            PlayerController pc = p.GetComponent<PlayerController>();
            pc.playerId = i + 1; 
            pc.currentGrid = node;
            p.transform.position = node.GetSlotPosition(p);
            players.Add(pc);
        }
    }
}