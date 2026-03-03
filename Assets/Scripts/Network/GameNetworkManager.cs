using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 游戏网络管理器 - 模拟联网的中间层（唯一联网切换点）
///
/// 架构：
///   业务逻辑 → SendXxxRequest(params, onComplete)  [客户端发请求]
///           → 模拟延迟 + 服务端权威处理
///           → onComplete(result) / ExecuteNetXxx()  [服务端广播回调]
///           → 业务逻辑继续
///
/// 切换真实网络时：只替换此文件实现，TurnManager / GridEventManager / GameStartManager 无需改动。
///
/// 命名规范：
///   SendXxxRequest()    — 客户端发起请求（含模拟延迟）
///   SimulateXxxServer() — 服务端处理逻辑（真实联网时改为协议收发）
///   ExecuteNetXxx()     — 服务端广播后各 Manager 的回调入口
/// </summary>
public class GameNetworkManager : MonoBehaviour
{
    public static GameNetworkManager Instance;

    [Header("模拟延迟（秒）")]
    public float simulatedLatency = 0.1f;

    [Header("房间配置（真实联网时由服务端下发）")]
    [Tooltip("当前局玩家数量，服务端据此判断是否收齐所有人")]
    public int playerCount = 6;

    // 决定顺序：暂存各玩家的骰子结果，收齐后广播
    private readonly Dictionary<int, int> _pendingOrderRolls = new Dictionary<int, int>();

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log("<color=green>[网络管理器] GameNetworkManager 已初始化</color>");
        }
        else
        {
            Destroy(gameObject);
        }
    }

    //========================================
    // 决定出场顺序（开局掷骰）
    //========================================

    /// <summary>
    /// 重置顺序决定状态，开局前必须调用。
    /// <para>count：本局参与玩家数量（服务端据此判断是否收齐）</para>
    /// </summary>
    public void ResetOrderRolls(int count)
    {
        playerCount = count;
        _pendingOrderRolls.Clear();
        Debug.Log($"<color=green>[网络管理器] 顺序决定已重置，等待 {count} 名玩家提交骰子</color>");
    }

    /// <summary>
    /// [请求] 玩家提交自己的开局骰子结果。
    /// <para>模拟阶段：GameStartManager 代替所有玩家逐一调用此方法。</para>
    /// <para>真实联网：每个客户端只调用一次（自己的玩家），服务端汇总广播。</para>
    /// </summary>
    public void SendRollForOrderRequest(int playerId, int rollResult)
    {
        Debug.Log($"<color=cyan>[网络请求] 玩家 {playerId} 开局骰子: {rollResult} 点</color>");
        StartCoroutine(SimulateRollForOrderServer(playerId, rollResult));
    }

    private IEnumerator SimulateRollForOrderServer(int playerId, int rollResult)
    {
        yield return new WaitForSeconds(simulatedLatency);

        _pendingOrderRolls[playerId] = rollResult;
        Debug.Log($"<color=yellow>[服务端] 收到玩家 {playerId} 骰子 {rollResult}（{_pendingOrderRolls.Count}/{playerCount}）</color>");

        // 收齐所有玩家的结果后，服务端排序并广播最终出场顺序
        if (_pendingOrderRolls.Count >= playerCount)
        {
            List<int> sortedIds = _pendingOrderRolls
                .OrderByDescending(kv => kv.Value)
                .Select(kv => kv.Key)
                .ToList();

            Debug.Log($"<color=yellow>[服务端] 全员骰子收齐，广播出场顺序: [{string.Join(", ", sortedIds)}]</color>");
            _pendingOrderRolls.Clear();

            // [广播] 通知 GameStartManager 最终顺序
            GameStartManager.Instance?.ExecuteNetFinalizeOrder(sortedIds);
        }
    }

    //========================================
    // 掷骰子（回合内）
    //========================================

    /// <summary>[请求] 玩家回合内掷骰子</summary>
    public void SendRollDiceRequest(int playerId)
    {
        Debug.Log($"<color=cyan>[网络请求] 玩家 {playerId} 掷骰子</color>");
        StartCoroutine(SimulateRollDiceServer(playerId));
    }

    private IEnumerator SimulateRollDiceServer(int playerId)
    {
        yield return new WaitForSeconds(simulatedLatency);
        int result = UnityEngine.Random.Range(1, 7);
        Debug.Log($"<color=yellow>[服务端] 玩家 {playerId} 掷出 {result} 点</color>");
        TurnManager.Instance?.ExecuteNetRollDice(playerId, result);
    }

    //========================================
    // 金钱变动（单人，用于奖励/银行/特殊格等）
    //========================================

    /// <summary>
    /// [请求] 单个玩家金钱变动（奖励、惩罚、银行等）
    /// <para>onComplete：服务端广播执行完毕后回调，用于驱动业务协程继续</para>
    /// </summary>
    public void SendMoneyChangeRequest(int playerId, int amount, string reason = "", Action onComplete = null)
    {
        Debug.Log($"<color=cyan>[网络请求] 玩家 {playerId} 金钱 {(amount >= 0 ? "+" : "")}{amount} ({reason})</color>");
        StartCoroutine(SimulateMoneyChangeServer(playerId, amount, reason, onComplete));
    }

    private IEnumerator SimulateMoneyChangeServer(int playerId, int amount, string reason, Action onComplete)
    {
        yield return new WaitForSeconds(simulatedLatency);
        Debug.Log($"<color=yellow>[服务端] 玩家 {playerId} 金钱更新 {amount} ({reason})</color>");
        GetPlayerById(playerId)?.ChangeMoney(amount);
        onComplete?.Invoke();
    }

    //========================================
    // 租金（原子操作：付款方 + 收款方同步）
    //========================================

    /// <summary>
    /// [请求] 玩家缴纳租金
    /// <para>服务端原子执行双方金钱变动，保证不出现一方成功一方失败的情况。</para>
    /// <para>onComplete：双方金钱均更新完毕后回调</para>
    /// </summary>
    public void SendRentRequest(int payerId, int ownerId, int rentAmount, Action onComplete = null)
    {
        Debug.Log($"<color=cyan>[网络请求] 玩家 {payerId} 向玩家 {ownerId} 缴租 ${rentAmount}</color>");
        StartCoroutine(SimulateRentServer(payerId, ownerId, rentAmount, onComplete));
    }

    private IEnumerator SimulateRentServer(int payerId, int ownerId, int rentAmount, Action onComplete)
    {
        yield return new WaitForSeconds(simulatedLatency);
        Debug.Log($"<color=yellow>[服务端] 租金 ${rentAmount}：玩家 {payerId} → 玩家 {ownerId}</color>");
        GetPlayerById(payerId)?.ChangeMoney(-rentAmount);
        GetPlayerById(ownerId)?.ChangeMoney(rentAmount);
        onComplete?.Invoke();
    }

    //========================================
    // 回合结束
    //========================================

    /// <summary>[请求] 当前玩家结束回合</summary>
    public void SendEndTurnRequest(int playerId)
    {
        Debug.Log($"<color=cyan>[网络请求] 玩家 {playerId} 结束回合</color>");
        StartCoroutine(SimulateEndTurnServer(playerId));
    }

    private IEnumerator SimulateEndTurnServer(int playerId)
    {
        yield return new WaitForSeconds(simulatedLatency);
        Debug.Log($"<color=yellow>[服务端] 玩家 {playerId} 回合结束</color>");
        TurnManager.Instance?.ExecuteNetEndTurn(playerId);
    }

    //========================================
    // 地块购买（服务端权威：验证 → 执行 → 广播）
    //========================================

    /// <summary>
    /// [请求] 玩家购买地块
    /// <para>onComplete(true)  = 购买成功（服务端已执行：扣款、归属、建筑动画）</para>
    /// <para>onComplete(false) = 服务端拒绝（金钱不足或地块已被买走）</para>
    /// </summary>
    public void SendBuyPropertyRequest(int playerId, int gridNodeId, int price, Action<bool> onComplete = null)
    {
        Debug.Log($"<color=cyan>[网络请求] 玩家 {playerId} 请求购买地块 {gridNodeId}（${price}）</color>");
        StartCoroutine(SimulateBuyPropertyServer(playerId, gridNodeId, price, onComplete));
    }

    private IEnumerator SimulateBuyPropertyServer(int playerId, int gridNodeId, int price, Action<bool> onComplete)
    {
        yield return new WaitForSeconds(simulatedLatency);

        PlayerController player = GetPlayerById(playerId);
        GridNode node = GetGridNodeById(gridNodeId);

        // 服务端权威校验：地块存在 + 无主 + 玩家钱够
        if (player != null && node != null && node.owner == null && player.money >= price)
        {
            Debug.Log($"<color=yellow>[服务端] 玩家 {playerId} 购买地块 {gridNodeId} 成功</color>");

            player.ChangeMoney(-price);
            node.owner = player;

            // 建筑落下动画（视觉逻辑保留在 GridEventManager）
            if (GridEventManager.Instance != null && node.buildingAnchor != null
                && GameDataManager.Instance != null && GameDataManager.Instance.housePrefab != null)
            {
                GridEventManager.Instance.StartCoroutine(
                    GridEventManager.Instance.PlayEnhancedHouseDropAnimation(node));
            }

            Renderer rend = node.GetComponent<Renderer>();
            if (rend != null) rend.material.color = new Color(0.6f, 1f, 0.6f);

            onComplete?.Invoke(true);
        }
        else
        {
            Debug.Log($"<color=red>[服务端拒绝] 玩家 {playerId} 购买地块 {gridNodeId} 失败（金钱不足或已有人购买）</color>");
            onComplete?.Invoke(false);
        }
    }

    //========================================
    // 商店卡牌购买（服务端权威：验证 → 扣款 → 加入手牌 → 广播）
    //========================================

    /// <summary>
    /// [请求] 玩家在商店购买卡牌
    /// <para>服务端权威校验：金钱足够 + 手牌未满 + 卡牌存在</para>
    /// <para>onComplete(true)  = 购买成功（服务端已扣款并将卡加入手牌）</para>
    /// <para>onComplete(false) = 服务端拒绝</para>
    /// </summary>
    public void SendBuyCardRequest(int playerId, string cardName, int price, Action<bool> onComplete = null)
    {
        Debug.Log($"<color=cyan>[网络请求] 玩家 {playerId} 购买卡牌 {cardName}（${price}）</color>");
        StartCoroutine(SimulateBuyCardServer(playerId, cardName, price, onComplete));
    }

    private IEnumerator SimulateBuyCardServer(int playerId, string cardName, int price, Action<bool> onComplete)
    {
        yield return new WaitForSeconds(simulatedLatency);

        PlayerController player = GetPlayerById(playerId);
        // 通过 cardName 在卡池里找到原始 ScriptableObject
        CardBase card = GameDataManager.Instance?.allPossibleCards?.Find(c => c.cardName == cardName);

        if (player != null && card != null && !player.IsHandFull() && player.money >= price)
        {
            Debug.Log($"<color=yellow>[服务端] 玩家 {playerId} 购买卡牌 {cardName} 成功</color>");
            player.ChangeMoney(-price);
            player.cards.Add(Instantiate(card)); // Instantiate 创建运行时副本，不污染原始 SO
            onComplete?.Invoke(true);
        }
        else
        {
            string reason = player == null ? "玩家不存在"
                : card == null ? "卡牌不在卡池中"
                : player.IsHandFull() ? "手牌已满"
                : "金钱不足";
            Debug.Log($"<color=red>[服务端拒绝] 玩家 {playerId} 购买卡牌 {cardName} 失败：{reason}</color>");
            onComplete?.Invoke(false);
        }
    }

    //========================================
    // 道具使用（TODO: 第三阶段网络化，对应 CMD_CARD_USE）
    //========================================

    /// <summary>
    /// [请求] 玩家使用道具卡
    /// <para>真实联网时对应 CMD_CARD_USE 协议广播</para>
    /// </summary>
    public void SendUseCardRequest(int playerId, string cardType, int targetGridNodeId = -1)
    {
        Debug.Log($"<color=cyan>[网络请求] 玩家 {playerId} 使用道具 {cardType}（目标地块: {targetGridNodeId}）</color>");
        StartCoroutine(SimulateUseCardServer(playerId, cardType, targetGridNodeId));
    }

    private IEnumerator SimulateUseCardServer(int playerId, string cardType, int targetGridNodeId)
    {
        yield return new WaitForSeconds(simulatedLatency);
        Debug.Log($"<color=yellow>[服务端] 玩家 {playerId} 使用道具 {cardType} — TODO: 第三阶段实现</color>");
        // TODO: 根据 cardType 路由到对应道具处理逻辑
    }

    //========================================
    // 辅助方法
    //========================================

    private PlayerController GetPlayerById(int playerId)
    {
        if (TurnManager.Instance == null) return null;
        foreach (var p in TurnManager.Instance.allPlayers)
            if (p.playerId == playerId) return p;
        return null;
    }

    private GridNode GetGridNodeById(int gridNodeId)
    {
        return GameDataManager.Instance?.database?.GetGridById(gridNodeId);
    }

    /// <summary>获取所有玩家（供外部系统查询）</summary>
    public PlayerController[] GetAllPlayers()
    {
        return TurnManager.Instance != null
            ? TurnManager.Instance.allPlayers.ToArray()
            : new PlayerController[0];
    }
}
