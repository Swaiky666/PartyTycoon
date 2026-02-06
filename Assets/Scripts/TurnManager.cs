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

    private bool isInFreeView = false;  // 主动点击“自由俯视”按钮的状态
    private bool isInCardMode = false;  // 正在打开卡牌列表或正在选择释放目标
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

        // 状态重置：恢复相机跟随玩家模式
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
        
        // 显示并旋转骰子
        if (diceAnimator != null) diceAnimator.ShowAndIdle();
        
        UIManager.Instance.ShowActionButton("投掷骰子", () => StartDiceRoll());

        // 刷新 UI 按钮初始显隐
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

    // 道具卡按钮逻辑
    void ToggleCardMode() {
        if (isInFreeView) return; 
        
        isInCardMode = !isInCardMode;

        if (isInCardMode) {
            if (diceAnimator != null) diceAnimator.ShowDice(false);
            UIManager.Instance.HideActionButton(); 
            UIManager.Instance.SetCardButtonLabel("返回"); 
            
            // 打开列表
            CardUIController.Instance.Show(GetCurrentPlayer().cards);
            UIManager.Instance.UpdateStatus("请选择要使用的道具卡");
        } else {
            // “返回”逻辑：清理并刷新回合 UI
            CardUIController.Instance.HideUI();
            if (CardRangeFinder.Instance != null) CardRangeFinder.Instance.ClearHighlight();
            StartTurn(); 
        }
    }

    // 关键：点击具体卡牌后，由 CardUIController 调用此方法
    public void EnterCardTargetingMode() {
        isInCardMode = true;
        // 1. 隐藏骰子
        if (diceAnimator != null) diceAnimator.ShowDice(false);
        // 2. 相机强制进入 90度 垂直俯视并开启平移模式
        if (CameraController.Instance != null) CameraController.Instance.SetFreeMode(true);
        // 3. 隐藏不相关的 UI，只留“取消”按钮（取消按钮由 CardUIController 控制显示）
        UIManager.Instance.viewButton.gameObject.SetActive(false);
        UIManager.Instance.actionButton.gameObject.SetActive(false);
        UIManager.Instance.UpdateStatus("点击红色地块使用道具（可拖拽屏幕平移视角）");
    }

    // 自由俯视按钮逻辑（纯观察模式）
    void ToggleFreeView() {
        if (isInCardMode) return; 

        isInFreeView = !isInFreeView;
        
        // 自由视角界面隐藏骰子模型
        if (diceAnimator != null) diceAnimator.ShowDice(!isInFreeView);

        if (CameraController.Instance != null) {
            CameraController.Instance.SetFreeMode(isInFreeView);
        }

        UIManager.Instance.SetViewButtonLabel(isInFreeView ? "返回" : "自由俯视");
        
        // 修改点：自由俯视角模式下隐藏卡牌按钮
        UIManager.Instance.cardButton.gameObject.SetActive(!isInFreeView);
        UIManager.Instance.actionButton.gameObject.SetActive(!isInFreeView);
    }

    public void CompleteCardAction() {
        // 卡牌使用完成，重置状态
        StartTurn(); 
    }

    void StartDiceRoll() {
        if (isInFreeView) ToggleFreeView();
        
        PlayerController p = GetCurrentPlayer();
        if (diceAnimator != null) diceAnimator.ShowDice(true);

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
        if (diceAnimator != null) diceAnimator.ShowDice(false);
        currentIndex = (currentIndex + 1) % turnOrder.Count;
        StartTurn();
    }
}