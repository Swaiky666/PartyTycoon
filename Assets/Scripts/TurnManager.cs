using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class TurnManager : MonoBehaviour {
    public static TurnManager Instance;

    [Header("引用")]
    public DiceAnimator diceAnimator; 
    
    public List<PlayerController> allPlayers => turnOrder;

    private List<PlayerController> turnOrder;
    private int currentIndex = 0;
    private bool isGameActive = false;
    private bool isInFreeView = false;  
    private bool isInCardMode = false;  
    private string originalStatusText = ""; 

    void Awake() { 
        if (Instance == null) Instance = this; 
    }

    public PlayerController GetCurrentPlayer() {
        if (turnOrder == null || turnOrder.Count <= currentIndex) return null;
        return turnOrder[currentIndex];
    }

    // 正常开局排序逻辑
    public void BeginGame(List<PlayerController> sortedPlayers) {
        turnOrder = sortedPlayers;
        currentIndex = 0;
        isGameActive = true;
        UIManager.Instance.SetPlayerStatsVisible(true);
        StartTurn();
    }

    // 从小游戏返回后的初始化逻辑：重新随机化行动顺序
    public void BeginGameFromMinigame(List<PlayerController> players) {
        // 使用随机值对玩家列表重新洗牌
        turnOrder = players.OrderBy(x => Random.value).ToList();
        currentIndex = 0;
        isGameActive = true;
        UIManager.Instance.SetPlayerStatsVisible(true);
        
        Debug.Log("【系统】从小游戏归来，本轮新顺序：" + string.Join(",", turnOrder.Select(p => p.playerId)));
        StartTurn();
    }

    public void StartTurn() {
        if (!isGameActive) return;

        PlayerController p = GetCurrentPlayer();
        if (p == null) return;

        if (CameraController.Instance != null) {
            CameraController.Instance.enabled = true;
            CameraController.Instance.SetTarget(p.transform);
            CameraController.Instance.SetFreeMode(false); 
        }
        
        isInFreeView = false;
        isInCardMode = false;

        UIManager.Instance.SetPlayerStatsVisible(true);
        UIManager.Instance.UpdatePlayerStats(p);
        
        originalStatusText = $"当前回合：玩家 {p.playerId}";
        UIManager.Instance.UpdateStatus(originalStatusText);
        
        if (diceAnimator != null) diceAnimator.ShowAndIdle();
        
        UIManager.Instance.ShowActionButton("投掷骰子", () => StartDiceRoll());

        UIManager.Instance.SetExtraButtonsVisible(true);
        UIManager.Instance.viewButton.gameObject.SetActive(true);
        UIManager.Instance.cardButton.gameObject.SetActive(true);
        
        UIManager.Instance.SetViewButtonLabel("自由俯视");
        UIManager.Instance.SetCardButtonLabel("道具卡");

        UIManager.Instance.viewButton.onClick.RemoveAllListeners();
        UIManager.Instance.viewButton.onClick.AddListener(ToggleFreeView);
        UIManager.Instance.cardButton.onClick.RemoveAllListeners();
        UIManager.Instance.cardButton.onClick.AddListener(ToggleCardMode);
    }

    void ToggleCardMode() {
        if (isInFreeView) return; 
        isInCardMode = !isInCardMode;
        if (isInCardMode) {
            if (diceAnimator != null) diceAnimator.ShowDice(false);
            UIManager.Instance.HideActionButton(); 
            UIManager.Instance.SetCardButtonLabel("返回"); 
            CardUIController.Instance.Show(GetCurrentPlayer().cards);
            UIManager.Instance.UpdateStatus("请选择要使用的道具卡");
        } else {
            CardUIController.Instance.HideUI();
            if (CardRangeFinder.Instance != null) CardRangeFinder.Instance.ClearHighlight();
            StartTurn(); 
        }
    }

    void ToggleFreeView() {
        if (isInCardMode) return; 
        isInFreeView = !isInFreeView;
        if (diceAnimator != null) diceAnimator.ShowDice(!isInFreeView);
        if (CameraController.Instance != null) CameraController.Instance.SetFreeMode(isInFreeView);
        UIManager.Instance.SetViewButtonLabel(isInFreeView ? "返回" : "自由俯视");
        UIManager.Instance.cardButton.gameObject.SetActive(!isInFreeView);
        UIManager.Instance.actionButton.gameObject.SetActive(!isInFreeView);
    }

    void StartDiceRoll() {
        if (isInFreeView) ToggleFreeView();
        UIManager.Instance.SetExtraButtonsVisible(false);
        UIManager.Instance.HideActionButton();
        StartCoroutine(ProcessTurnSequence());
    }

    IEnumerator ProcessTurnSequence() {
        PlayerController p = turnOrder[currentIndex];

        if (p.CheckFreezeStatus()) {
            if (diceAnimator != null) diceAnimator.ShowDice(false);
            yield return new WaitForSeconds(2.0f);
            EndTurn();
            yield break; 
        }

        int steps = Random.Range(1, 7);
        UIManager.Instance.UpdateStatus("骰子旋转中..."); 

        if (diceAnimator != null) {
            yield return StartCoroutine(diceAnimator.PlayRollSequence(steps, null));
        }

        UIManager.Instance.UpdateStatus($"玩家 {p.playerId} 投出了 {steps} 点！");
        yield return new WaitForSeconds(0.5f);

        bool moveDone = false;
        p.StartMoving(steps, () => moveDone = true);
        while (!moveDone) yield return null;

        bool eventDone = false;
        if (GridEventManager.Instance != null) {
            yield return StartCoroutine(GridEventManager.Instance.HandleGridEvent(p, p.currentGrid, () => eventDone = true));
            while (!eventDone) yield return null;
        }

        yield return new WaitForSeconds(1.0f);
        EndTurn();
    }

    void EndTurn() {
        if (diceAnimator != null) diceAnimator.ShowDice(false);

        // 检测是否所有人都走完了
        if (currentIndex >= turnOrder.Count - 1) {
            Debug.Log("【流程】本轮结束，准备进入小游戏...");
            StartCoroutine(EnterMinigameFlow());
        } else {
            currentIndex++;
            StartTurn();
        }
    }

    IEnumerator EnterMinigameFlow() {
        UIManager.Instance.UpdateStatus("本轮结束！准备进入小游戏环节...");
        yield return new WaitForSeconds(2.0f);
        // 保存并跳转
        GameDataManager.Instance.SwitchToRandomMinigame(allPlayers);
    }

    public void CompleteCardAction() {
        StartTurn(); 
    }

    public void EnterCardTargetingMode() {
        isInCardMode = true;
        if (diceAnimator != null) diceAnimator.ShowDice(false);
        if (CameraController.Instance != null) CameraController.Instance.SetFreeMode(true);
        UIManager.Instance.viewButton.gameObject.SetActive(false);
        UIManager.Instance.actionButton.gameObject.SetActive(false);
    }
}