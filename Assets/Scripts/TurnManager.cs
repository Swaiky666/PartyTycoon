using UnityEngine;
using System.Collections;
using System.Collections.Generic;

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

    public void EnterCardTargetingMode() {
        isInCardMode = true;
        if (diceAnimator != null) diceAnimator.ShowDice(false);
        if (CameraController.Instance != null) CameraController.Instance.SetFreeMode(true);
        UIManager.Instance.viewButton.gameObject.SetActive(false);
        UIManager.Instance.actionButton.gameObject.SetActive(false);
        UIManager.Instance.UpdateStatus("点击红色地块使用道具（可拖拽屏幕平移视角）");
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

    public void CompleteCardAction() {
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

        // 1. 计算点数，但不立即公布
        int steps = Random.Range(1, 7);
        UIManager.Instance.UpdateStatus("骰子旋转中..."); 

        // 2. 等待骰子旋转动画及缩放反馈结束
        if (diceAnimator != null) {
            yield return StartCoroutine(diceAnimator.PlayRollSequence(steps, null));
        }

        // 3. 动画结束后公布结果
        UIManager.Instance.UpdateStatus($"玩家 {p.playerId} 投出了 {steps} 点！");
        yield return new WaitForSeconds(0.5f); // 停留一下让玩家看清文字

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