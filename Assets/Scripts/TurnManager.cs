using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class TurnManager : MonoBehaviour {
    public static TurnManager Instance;

    [Header("引用")]
    public DiceAnimator diceAnimator; 
    
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

    public void StartTurn() {
        if (!isGameActive) return;

        PlayerController p = GetCurrentPlayer();
        if (p == null) return;

        // 恢复相机状态
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

        // 初始 UI 状态
        UIManager.Instance.SetExtraButtonsVisible(true);
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
            if (CardUIController.Instance == null) {
                Debug.LogError("场景中缺少 CardUIController 实例！");
                isInCardMode = false;
                return;
            }

            UIManager.Instance.HideActionButton(); 
            UIManager.Instance.SetCardButtonLabel("返回"); 
            
            if (CameraController.Instance != null) CameraController.Instance.enabled = false;
            
            CardUIController.Instance.Show(GetCurrentPlayer().cards);
            UIManager.Instance.UpdateStatus("请选择要使用的道具卡");
        } else {
            if (CameraController.Instance != null) CameraController.Instance.enabled = true;
            
            CardUIController.Instance.HideUI();
            if (CardRangeFinder.Instance != null) CardRangeFinder.Instance.ClearHighlight();
            
            UIManager.Instance.SetCardButtonLabel("道具卡");
            
            UIManager.Instance.ShowActionButton("投掷骰子", () => StartDiceRoll());
            UIManager.Instance.UpdateStatus(originalStatusText);
        }
    }

    void ToggleFreeView() {
        if (isInCardMode) ToggleCardMode(); 

        isInFreeView = !isInFreeView;
        CameraController.Instance.SetFreeMode(isInFreeView);
        UIManager.Instance.SetViewButtonLabel(isInFreeView ? "返回" : "自由俯视");
        
        UIManager.Instance.cardButton.gameObject.SetActive(!isInFreeView);
        UIManager.Instance.actionButton.gameObject.SetActive(!isInFreeView);
    }

    public void CompleteCardAction() {
        isInCardMode = false;
        if (CameraController.Instance != null) CameraController.Instance.enabled = true;
        StartTurn(); 
    }

    void StartDiceRoll() {
        if (isInFreeView) ToggleFreeView();
        
        // 核心修改：在按下投掷按钮后，先检查冰冻状态
        PlayerController p = GetCurrentPlayer();
        if (p != null && p.remainingFreezeTurns > 0) {
            // 如果被冰冻，直接进入处理序列（处理扣减回合和跳过逻辑）
            UIManager.Instance.SetExtraButtonsVisible(false);
            UIManager.Instance.HideActionButton();
            StartCoroutine(ProcessTurnSequence());
        } else {
            // 正常逻辑
            UIManager.Instance.SetExtraButtonsVisible(false);
            UIManager.Instance.HideActionButton();
            StartCoroutine(ProcessTurnSequence());
        }
    }

    IEnumerator ProcessTurnSequence() {
        PlayerController p = turnOrder[currentIndex];

        // --- 新增：冰冻状态检测与拦截 ---
        if (p.CheckFreezeStatus()) {
            // 如果玩家被冰冻，此方法内部会扣除剩余回合并更新UI
            // 我们等待一会儿让玩家看清状态，然后直接结束本回合
            yield return new WaitForSeconds(2.0f);
            EndTurn();
            yield break; // 彻底跳过后续的掷骰子和移动逻辑
        }

        // --- 以下为原有掷骰子和移动逻辑 ---
        int steps = Random.Range(1, 7);
        UIManager.Instance.UpdateStatus($"玩家 {p.playerId} 投出了 {steps} 点！");
        if (diceAnimator != null) yield return StartCoroutine(diceAnimator.PlayRollSequence(steps, null));

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
        currentIndex = (currentIndex + 1) % turnOrder.Count;
        StartTurn();
    }
}