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

    public void BeginGame(List<PlayerController> sortedPlayers) {
        turnOrder = sortedPlayers;
        currentIndex = 0;
        isGameActive = true;
        UIManager.Instance.SetPlayerStatsVisible(true);
        StartTurn();
    }

    public void BeginGameFromMinigame(List<PlayerController> players) {
        turnOrder = players.OrderBy(x => Random.value).ToList();
        currentIndex = 0;
        isGameActive = true;
        UIManager.Instance.SetPlayerStatsVisible(true);
        StartTurn();
    }

    public void StartTurn() {
        if (!isGameActive) return;

        PlayerController p = GetCurrentPlayer();
        if (p == null) return;

        // --- 相机重置 ---
        if (CameraController.Instance != null) {
            CameraController.Instance.enabled = true;
            CameraController.Instance.SetTarget(p.transform);
            CameraController.Instance.SetFreeMode(false); 
        }
        
        isInFreeView = false;
        isInCardMode = false;

        // --- UI 初始化 ---
        UIManager.Instance.SetPlayerStatsVisible(true);
        UIManager.Instance.UpdatePlayerStats(p);
        
        originalStatusText = $"当前回合：玩家 {p.playerId}";
        UIManager.Instance.UpdateStatus(originalStatusText);
        
        if (diceAnimator != null) diceAnimator.ShowAndIdle();
        
        // --- 模拟阶段：为当前玩家直接开放 UI ---
        UIManager.Instance.ShowActionButton("投掷骰子", () => StartDiceRollRequest());

        UIManager.Instance.SetExtraButtonsVisible(true);
        UIManager.Instance.viewButton.gameObject.SetActive(true);
        UIManager.Instance.cardButton.gameObject.SetActive(true);
        
        UIManager.Instance.SetViewButtonLabel("自由俯视");
        UIManager.Instance.SetCardButtonLabel("道具卡");

        // 绑定按钮事件
        UIManager.Instance.viewButton.onClick.RemoveAllListeners();
        UIManager.Instance.viewButton.onClick.AddListener(ToggleFreeView);
        UIManager.Instance.cardButton.onClick.RemoveAllListeners();
        UIManager.Instance.cardButton.onClick.AddListener(ToggleCardMode);
    }

    // 核心请求：不直接执行，而是发给“网络”
    void StartDiceRollRequest() {
        if (isInFreeView) ToggleFreeView();
        
        // 禁用 UI 防止连点
        UIManager.Instance.SetExtraButtonsVisible(false);
        UIManager.Instance.HideActionButton();
        
        PlayerController p = GetCurrentPlayer();
        NetworkManager.Instance.SendRollDiceRequest(p.playerId);
    }

    // 核心广播：收到网络点数后执行
    public void ExecuteNetRollDice(int playerId, int result) {
        // 校验是否是当前玩家的消息
        PlayerController p = GetCurrentPlayer();
        if (p.playerId != playerId) return;

        StopAllCoroutines();
        StartCoroutine(ProcessTurnSequenceNet(p, result));
    }

    IEnumerator ProcessTurnSequenceNet(PlayerController p, int steps) {
        // 保留冰冻检查
        if (p.remainingFreezeTurns > 0) {
            UIManager.Instance.UpdateStatus($"玩家 {p.playerId} 冰冻中...");
            if (diceAnimator != null) diceAnimator.ShowDice(false);
            yield return new WaitForSeconds(2.0f);
            EndTurn();
            yield break; 
        }

        UIManager.Instance.UpdateStatus("骰子旋转中..."); 
        if (diceAnimator != null) {
            yield return StartCoroutine(diceAnimator.PlayRollSequence(steps, null));
        }

        UIManager.Instance.UpdateStatus($"玩家 {p.playerId} 投出了 {steps} 点！");
        yield return new WaitForSeconds(0.5f);

        // 移动
        bool moveDone = false;
        p.StartMoving(steps, () => moveDone = true);
        while (!moveDone) yield return null;

        // 地块事件
        bool eventDone = false;
        if (GridEventManager.Instance != null) {
            yield return StartCoroutine(GridEventManager.Instance.HandleGridEvent(p, p.currentGrid, () => eventDone = true));
            while (!eventDone) yield return null;
        }

        yield return new WaitForSeconds(1.0f);
        EndTurn();
    }

    // --- 视图与卡牌 (保留单机版所有功能) ---
    void ToggleCardMode() {
        if (isInFreeView) return; 
        isInCardMode = !isInCardMode;
        if (isInCardMode) {
            if (diceAnimator != null) diceAnimator.ShowDice(false);
            UIManager.Instance.HideActionButton(); 
            UIManager.Instance.SetCardButtonLabel("返回"); 
            CardUIController.Instance.Show(GetCurrentPlayer().cards);
        } else {
            CardUIController.Instance.HideUI();
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

    void EndTurn() {
        if (diceAnimator != null) diceAnimator.ShowDice(false);
        currentIndex = (currentIndex + 1) % turnOrder.Count;
        StartTurn();
    }

    IEnumerator EnterMinigameFlow() {
        UIManager.Instance.UpdateStatus("本轮结束！准备进入小游戏...");
        yield return new WaitForSeconds(2.0f);
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