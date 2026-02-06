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

    // --- 借鉴自由跟随效果的修改 ---
    void ToggleCardMode() {
        if (isInFreeView) return; 
        
        // 核心逻辑：切换开关状态
        isInCardMode = !isInCardMode;

        if (isInCardMode) {
            // 检查单例是否存在，防止报错
            if (CardUIController.Instance == null) {
                Debug.LogError("场景中缺少 CardUIController 实例！");
                isInCardMode = false;
                return;
            }

            // 1. 进入卡牌模式
            UIManager.Instance.HideActionButton(); 
            UIManager.Instance.SetCardButtonLabel("返回"); // 改变按钮文案
            
            // 2. 禁用相机，防止拖拽干扰
            if (CameraController.Instance != null) CameraController.Instance.enabled = false;
            
            CardUIController.Instance.Show(GetCurrentPlayer().cards);
            UIManager.Instance.UpdateStatus("请选择要使用的道具卡");
        } else {
            // 3. 再次点击，返回正常模式
            if (CameraController.Instance != null) CameraController.Instance.enabled = true;
            
            CardUIController.Instance.HideUI();
            if (CardRangeFinder.Instance != null) CardRangeFinder.Instance.ClearHighlight();
            
            UIManager.Instance.SetCardButtonLabel("道具卡");
            
            // 恢复“投骰子”按钮显示
            UIManager.Instance.ShowActionButton("投掷骰子", () => StartDiceRoll());
            UIManager.Instance.UpdateStatus(originalStatusText);
        }
    }

    void ToggleFreeView() {
        if (isInCardMode) ToggleCardMode(); // 如果在看牌，先关掉牌

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
        UIManager.Instance.SetExtraButtonsVisible(false);
        UIManager.Instance.HideActionButton();
        StartCoroutine(ProcessTurnSequence());
    }

    IEnumerator ProcessTurnSequence() {
        PlayerController p = turnOrder[currentIndex];
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