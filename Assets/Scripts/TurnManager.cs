using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class TurnManager : MonoBehaviour {
    public static TurnManager Instance;

    [Header("引用")]
    public DiceAnimator diceAnimator;

    [Header("税收系统")]
    [Tooltip("每次税率上涨幅度（0.05 = 5%）")]
    public float taxRateStep = 0.05f;
    [Tooltip("每隔几轮涨一次税")]
    public int taxRoundInterval = 3;

    [Header("小游戏系统")]
    [Tooltip("每隔几轮进入一次小游戏（0 = 禁用）")]
    public int minigameRoundInterval = 3;

    public List<PlayerController> allPlayers => turnOrder;

    [HideInInspector] public float currentTaxRate = 0f;
    private int roundCount = 0;

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
        
        if (diceAnimator != null) { diceAnimator.ShowDice(true); diceAnimator.ShowAndIdle(); }
        
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
        GameNetworkManager.Instance.SendRollDiceRequest(p.playerId);
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
        // 冰冻检查：CheckFreezeStatus() 负责递减计数、更新 UI、解冻
        if (p.CheckFreezeStatus()) {
            if (diceAnimator != null) diceAnimator.ShowDice(false);
            yield return new WaitForSeconds(2.0f);
            GameNetworkManager.Instance.SendEndTurnRequest(p.playerId);
            yield break;
        }

        UIManager.Instance.UpdateStatus("骰子旋转中...");
        if (diceAnimator != null) {
            yield return StartCoroutine(diceAnimator.PlayRollSequence(steps, null));
            diceAnimator.ShowDice(false); // 掷出结果后隐藏，移动/事件阶段不显示骰子
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
        GameNetworkManager.Instance.SendEndTurnRequest(p.playerId);
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
        currentIndex = (currentIndex + 1) % turnOrder.Count;

        // 所有玩家都走完一次 = 一轮结束
        if (currentIndex == 0) {
            roundCount++;

            // 税率检查
            if (roundCount % taxRoundInterval == 0) {
                currentTaxRate += taxRateStep;
                UIManager.Instance.ShowTaxNotification(
                    $"<color=red>税率上涨！当前税率：{currentTaxRate * 100:0}%</color>");
                Debug.Log($"<color=orange>[税收] 第 {roundCount} 轮结束，税率升至 {currentTaxRate * 100:0}%</color>");
            }

            // 小游戏触发
            if (minigameRoundInterval > 0 && roundCount % minigameRoundInterval == 0) {
                StartCoroutine(EnterMinigameFlow());
                return; // 不进入下一回合，等待场景切换
            }
        }

        StartTurn();
    }

    /// <summary>
    /// [网络广播] 执行回合结束（来自 GameNetworkManager）
    /// </summary>
    public void ExecuteNetEndTurn(int playerId)
    {
        // 校验是否是当前玩家
        PlayerController p = GetCurrentPlayer();
        if (p == null || p.playerId != playerId) return;

        EndTurn();
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

    //========================================
    // 淘汰机制
    //========================================

    /// <summary>
    /// 淘汰指定玩家：清空房产 → 移出回合序列 → 检查游戏结束。
    /// 由 GameNetworkManager 在任何扣钱导致资金 ≤ 0 后调用。
    /// </summary>
    public void EliminatePlayer(PlayerController player)
    {
        if (turnOrder == null || !turnOrder.Contains(player)) return;

        Debug.Log($"<color=red>[淘汰] 玩家 {player.playerId} 资金耗尽，退出游戏</color>");
        UIManager.Instance.UpdateStatus($"<color=red>玩家 {player.playerId} 已被淘汰！</color>");

        // 1. 清空名下所有房产
        var allNodes = GameDataManager.Instance?.database?.GetAllNodes();
        if (allNodes != null)
        {
            foreach (var node in allNodes)
            {
                if (node.owner != player) continue;
                node.owner = null;
                if (node.currentBuilding != null) { Destroy(node.currentBuilding); node.currentBuilding = null; }
                Renderer rend = node.GetComponent<Renderer>();
                if (rend != null) rend.material.color = Color.white;
            }
        }

        // 2. 隐藏玩家模型
        player.gameObject.SetActive(false);

        // 3. 调整 currentIndex 后再移出列表
        bool wasCurrentPlayer = (turnOrder[currentIndex] == player);
        int playerIndex = turnOrder.IndexOf(player);
        turnOrder.Remove(player);

        if (playerIndex < currentIndex)
            currentIndex--;           // 被移出的在当前之前，索引后移
        else if (wasCurrentPlayer && currentIndex >= turnOrder.Count)
            currentIndex = 0;

        // 4. 检查游戏结束
        if (turnOrder.Count <= 1)
        {
            HandleGameOver();
            return;
        }

        // 5. 若淘汰的是当前玩家，强制跳到下一位
        if (wasCurrentPlayer)
        {
            StopAllCoroutines();
            StartTurn();
        }
    }

    private void HandleGameOver()
    {
        isGameActive = false;
        string msg = turnOrder.Count == 1
            ? $"游戏结束！玩家 {turnOrder[0].playerId} 获胜！"
            : "游戏结束！";
        UIManager.Instance.UpdateStatus($"<color=gold>{msg}</color>");
        UIManager.Instance.HideActionButton();
        UIManager.Instance.SetExtraButtonsVisible(false);
        if (diceAnimator != null) diceAnimator.ShowDice(false);
        Debug.Log($"<color=gold>[游戏结束] {msg}</color>");
    }
}