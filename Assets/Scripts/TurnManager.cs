using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class TurnManager : MonoBehaviour {
    public static TurnManager Instance;
    public DiceAnimator diceAnimator; 

    private List<PlayerController> turnOrder;
    private int currentIndex = 0;
    private bool isGameActive = false;

    // --- 新增：状态标记 ---
    private bool isInFreeView = false;
    private bool isInCardMode = false;
    private string originalStatus = ""; // 用于切回时的文字还原

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
        CameraController.Instance.SetTarget(p.transform);

        originalStatus = $"当前回合：玩家 {p.playerId}";
        UIManager.Instance.UpdateStatus(originalStatus);
        
        if (diceAnimator != null) diceAnimator.ShowAndIdle();

        // 1. 显示投骰子主按钮
        UIManager.Instance.ShowActionButton("投骰子", () => StartDiceRoll());

        // 2. 显示视角和道具按钮，并绑定逻辑
        UIManager.Instance.SetExtraButtonsVisible(true);
        UIManager.Instance.SetViewButtonLabel("俯视角");
        UIManager.Instance.SetCardButtonLabel("道具卡");

        UIManager.Instance.viewButton.onClick.RemoveAllListeners();
        UIManager.Instance.viewButton.onClick.AddListener(ToggleFreeView);

        UIManager.Instance.cardButton.onClick.RemoveAllListeners();
        UIManager.Instance.cardButton.onClick.AddListener(ToggleCardMode);
    }

    // 逻辑：视角切换
    void ToggleFreeView() {
        if (isInCardMode) return; // 互斥
        isInFreeView = !isInFreeView;
        
        CameraController.Instance.SetFreeMode(isInFreeView);
        UIManager.Instance.SetViewButtonLabel(isInFreeView ? "返回跟随" : "俯视角");
        UIManager.Instance.cardButton.gameObject.SetActive(!isInFreeView); // 隐藏另一个
        UIManager.Instance.actionButton.gameObject.SetActive(!isInFreeView); // 视角模式不能投骰子
        
        if(isInFreeView) UIManager.Instance.UpdateStatus("自由观察模式：拖动屏幕平移");
        else UIManager.Instance.UpdateStatus(originalStatus);
    }

    // 逻辑：道具切换
    void ToggleCardMode() {
        if (isInFreeView) return; // 互斥
        isInCardMode = !isInCardMode;

        UIManager.Instance.SetCardButtonLabel(isInCardMode ? "返回跟随" : "道具卡");
        UIManager.Instance.viewButton.gameObject.SetActive(!isInCardMode); // 隐藏另一个
        UIManager.Instance.actionButton.gameObject.SetActive(!isInCardMode); // 道具模式不能投骰子

        if(isInCardMode) UIManager.Instance.UpdateStatus("请选择要使用的道具卡...");
        else UIManager.Instance.UpdateStatus(originalStatus);
    }

    void StartDiceRoll() {
        // 开始移动流程前，必须强制关闭所有特殊模式
        if (isInFreeView) ToggleFreeView();
        if (isInCardMode) ToggleCardMode();

        // 隐藏所有额外按钮，玩家移动中禁止操作
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

        yield return new WaitForSeconds(1.0f);
        EndTurn();
    }

    void EndTurn() {
        currentIndex = (currentIndex + 1) % turnOrder.Count;
        StartTurn();
    }
}