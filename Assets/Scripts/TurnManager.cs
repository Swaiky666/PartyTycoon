using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class TurnManager : MonoBehaviour {
    public static TurnManager Instance;
    public DiceAnimator diceAnimator; 

    private List<PlayerController> turnOrder;
    private int currentIndex = 0;
    private bool isGameActive = false;

    private bool isInFreeView = false;
    private bool isInCardMode = false;
    private string originalStatus = "";

    void Awake() { if (Instance == null) Instance = this; }

    public void BeginGame(List<PlayerController> sortedPlayers) {
        turnOrder = sortedPlayers;
        currentIndex = 0;
        isGameActive = true;
        StartTurn();
    }

    void StartTurn() {
        if (!isGameActive) return;
        PlayerController p = turnOrder[currentIndex];
        
        // 1. 确保玩家信息 UI 是开启状态
        UIManager.Instance.SetPlayerStatsVisible(true);
        UIManager.Instance.UpdatePlayerStats(p);

        // 2. 相机锁定玩家
        CameraController.Instance.SetTarget(p.transform);

        // 3. 更新状态文字
        originalStatus = $"当前回合：玩家 {p.playerId}";
        UIManager.Instance.UpdateStatus(originalStatus);
        
        // 4. 显示骰子和功能按钮
        if (diceAnimator != null) diceAnimator.ShowAndIdle();
        UIManager.Instance.ShowActionButton("投骰子", () => StartDiceRoll());

        UIManager.Instance.SetExtraButtonsVisible(true);
        UIManager.Instance.SetViewButtonLabel("俯视角");
        UIManager.Instance.SetCardButtonLabel("道具卡");

        UIManager.Instance.viewButton.onClick.RemoveAllListeners();
        UIManager.Instance.viewButton.onClick.AddListener(ToggleFreeView);
        UIManager.Instance.cardButton.onClick.RemoveAllListeners();
        UIManager.Instance.cardButton.onClick.AddListener(ToggleCardMode);
    }

    void ToggleFreeView() {
        if (isInCardMode) return;
        isInFreeView = !isInFreeView;
        CameraController.Instance.SetFreeMode(isInFreeView);
        UIManager.Instance.SetViewButtonLabel(isInFreeView ? "返回跟随" : "俯视角");
        
        // 自由视角时隐藏投骰子和卡牌按钮
        UIManager.Instance.cardButton.gameObject.SetActive(!isInFreeView);
        UIManager.Instance.actionButton.gameObject.SetActive(!isInFreeView);
        
        if(isInFreeView) UIManager.Instance.UpdateStatus("自由视角：滑动屏幕移动，双指旋转");
        else UIManager.Instance.UpdateStatus(originalStatus);
    }

    void ToggleCardMode() {
        if (isInFreeView) return;
        isInCardMode = !isInCardMode;
        UIManager.Instance.SetCardButtonLabel(isInCardMode ? "返回跟随" : "道具卡");
        
        UIManager.Instance.viewButton.gameObject.SetActive(!isInCardMode);
        UIManager.Instance.actionButton.gameObject.SetActive(!isInCardMode);

        if(isInCardMode) UIManager.Instance.UpdateStatus("道具商店/背包模式...");
        else UIManager.Instance.UpdateStatus(originalStatus);
    }

    void StartDiceRoll() {
        if (isInFreeView) ToggleFreeView();
        if (isInCardMode) ToggleCardMode();
        
        UIManager.Instance.SetExtraButtonsVisible(false);
        UIManager.Instance.HideActionButton();
        StartCoroutine(ProcessTurn());
    }

    IEnumerator ProcessTurn() {
        PlayerController p = turnOrder[currentIndex];
        int diceResult = Random.Range(1, 7);
        UIManager.Instance.UpdateStatus($"玩家 {p.playerId} 投出了 {diceResult} 点");

        if (diceAnimator != null) yield return StartCoroutine(diceAnimator.PlayRollSequence(diceResult, null));

        bool moveFinished = false;
        p.StartMoving(diceResult, () => moveFinished = true);
        while (!moveFinished) yield return null;

        bool eventFinished = false;
        yield return StartCoroutine(GridEventManager.Instance.HandleGridEvent(p, p.currentGrid, () => eventFinished = true));
        while (!eventFinished) yield return null;

        yield return new WaitForSeconds(1.0f);
        EndTurn();
    }

    void EndTurn() {
        currentIndex = (currentIndex + 1) % turnOrder.Count;
        StartTurn();
    }
}