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
        
        // --- 核心修改：使用 ShowDice(true) 显示骰子模型 ---
        if (diceAnimator != null) diceAnimator.ShowAndIdle();
        
        UIManager.Instance.ShowActionButton("投掷骰子", () => StartDiceRoll());

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

            // --- 核心修改：进入卡牌选择，隐藏骰子模型 ---
            if (diceAnimator != null) diceAnimator.ShowDice(false);

            UIManager.Instance.HideActionButton(); 
            UIManager.Instance.SetCardButtonLabel("返回"); 
            
            if (CameraController.Instance != null) CameraController.Instance.enabled = false;
            
            CardUIController.Instance.Show(GetCurrentPlayer().cards);
            UIManager.Instance.UpdateStatus("请选择要使用的道具卡");
        } else {
            if (CameraController.Instance != null) CameraController.Instance.enabled = true;
            
            // --- 核心修改：切回投骰子，显示骰子模型并继续旋转 ---
            if (diceAnimator != null) diceAnimator.ShowAndIdle();

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
        
        // --- 核心修改：自由视角界面隐藏骰子模型 ---
        if (diceAnimator != null) {
            if (isInFreeView) diceAnimator.ShowDice(false);
            else diceAnimator.ShowAndIdle();
        }

        CameraController.Instance.SetFreeMode(isInFreeView);
        UIManager.Instance.SetViewButtonLabel(isInFreeView ? "返回" : "自由俯视");
        
        UIManager.Instance.cardButton.gameObject.SetActive(!isInFreeView);
        UIManager.Instance.actionButton.gameObject.SetActive(!isInFreeView);
    }

    public void CompleteCardAction() {
        isInCardMode = false;
        if (CameraController.Instance != null) CameraController.Instance.enabled = true;
        // StartTurn 会自动处理骰子模型的 ShowAndIdle
        StartTurn(); 
    }

    void StartDiceRoll() {
        if (isInFreeView) ToggleFreeView();
        
        PlayerController p = GetCurrentPlayer();
        
        // 投骰子前确保模型可见
        if (diceAnimator != null) diceAnimator.ShowDice(true);

        UIManager.Instance.SetExtraButtonsVisible(false);
        UIManager.Instance.HideActionButton();
        StartCoroutine(ProcessTurnSequence());
    }

    IEnumerator ProcessTurnSequence() {
        PlayerController p = turnOrder[currentIndex];

        if (p.CheckFreezeStatus()) {
            // 冰冻跳过回合时隐藏模型
            if (diceAnimator != null) diceAnimator.ShowDice(false);
            yield return new WaitForSeconds(2.0f);
            EndTurn();
            yield break; 
        }

        int steps = Random.Range(1, 7);
        UIManager.Instance.UpdateStatus($"玩家 {p.playerId} 投出了 {steps} 点！");
        
        // 播放投掷序列，完成后 PlayRollSequence 内部会根据 autoHide 决定是否隐藏模型
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
        // 回合结束确保隐藏模型
        if (diceAnimator != null) diceAnimator.ShowDice(false);
        currentIndex = (currentIndex + 1) % turnOrder.Count;
        StartTurn();
    }
}