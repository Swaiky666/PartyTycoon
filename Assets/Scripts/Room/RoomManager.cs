using UnityEngine;
using System.Collections.Generic;

public class RoomManager : MonoBehaviour {
    public static RoomManager Instance;

    // 固定调色盘（12色，与大地图玩家颜色一致）
    public static readonly Color[] PlayerColors = {
        new Color(0.92f, 0.26f, 0.26f), // 0 红
        new Color(0.26f, 0.52f, 0.96f), // 1 蓝
        new Color(0.18f, 0.80f, 0.44f), // 2 绿
        new Color(0.98f, 0.65f, 0.14f), // 3 橙
        new Color(0.60f, 0.29f, 0.92f), // 4 紫
        new Color(0.98f, 0.92f, 0.18f), // 5 黄
        new Color(0.98f, 0.38f, 0.72f), // 6 粉
        new Color(0.10f, 0.84f, 0.94f), // 7 青
        new Color(0.68f, 0.94f, 0.14f), // 8 草绿
        new Color(0.10f, 0.72f, 0.70f), // 9 青绿
        new Color(0.72f, 0.44f, 0.18f), // 10 棕
        new Color(0.88f, 0.88f, 0.92f), // 11 银白
    };

    [HideInInspector] public SlotData[] slots = new SlotData[6];
    [HideInInspector] public int  localPlayerId  = 0;
    [HideInInspector] public int  localSlotIndex = 0;
    [HideInInspector] public bool isLocalHost    = true;
    [HideInInspector] public string roomCode     = "------";
    [HideInInspector] public int  roundCount     = 5;

    void Awake() {
        Instance = this;
        for (int i = 0; i < 6; i++) slots[i] = new SlotData(i);
    }

    // ── 初始化（联网）────────────────────────────────────────

    /// <summary>向服务端发送创建房间请求。</summary>
    public void RequestCreateRoom(string playerName) {
        if (RoomNetworkManager.Instance != null)
            RoomNetworkManager.Instance.SendCreateRoom(playerName);
        else
            InitAsHostLocal(playerName); // 无网络时本地回退
    }

    /// <summary>向服务端发送加入房间请求。</summary>
    public void RequestJoinRoom(string code, string playerName) {
        if (RoomNetworkManager.Instance != null)
            RoomNetworkManager.Instance.SendJoinRoom(code, playerName);
        else
            Debug.LogWarning("[RoomManager] 无网络，无法加入房间");
    }

    /// <summary>
    /// [服务端广播] 本客户端成功创建或加入房间，写入本地身份信息并刷新 UI。
    /// </summary>
    public void ExecuteNetRoomInit(string serverRoomCode, int playerId, int slotIndex, SlotData[] serverSlots, int serverRoundCount, bool isHost) {
        localPlayerId  = playerId;
        localSlotIndex = slotIndex;
        isLocalHost    = isHost;
        roomCode       = serverRoomCode;
        roundCount     = serverRoundCount;
        ApplyServerSlots(serverSlots);
        RoomUI.Instance?.RefreshAll();
    }

    /// <summary>[服务端广播] 整体槽位状态刷新（有人加入/离开/被踢/添加AI）。</summary>
    public void ExecuteNetFullSlotsUpdate(SlotData[] serverSlots) {
        ApplyServerSlots(serverSlots);
        RoomUI.Instance?.RefreshAll();
    }

    void ApplyServerSlots(SlotData[] serverSlots) {
        if (serverSlots == null) return;
        for (int i = 0; i < serverSlots.Length && i < slots.Length; i++)
            slots[i] = serverSlots[i];
    }

    // ── 本地回退（无服务端时） ────────────────────────────────

    void InitAsHostLocal(string playerName) {
        localPlayerId  = 0;
        localSlotIndex = 0;
        isLocalHost    = true;
        roomCode       = "LOCAL";

        slots[0].state      = SlotState.Human;
        slots[0].playerId   = 0;
        slots[0].playerName = playerName;
        slots[0].colorIndex = 0;
        slots[0].isReady    = true;
        slots[0].isHost     = true;

        RoomUI.Instance?.RefreshAll();
    }

    // ── 玩家操作 ──────────────────────────────────────────

    public void RequestUpdateName(string newName) {
        if (string.IsNullOrWhiteSpace(newName)) return;
        string trimmed = newName.Trim();
        if (RoomNetworkManager.Instance != null)
            RoomNetworkManager.Instance.SendRoomUpdateNameRequest(localPlayerId, trimmed);
        else
            ExecuteNetUpdateName(localPlayerId, trimmed);
    }

    /// <param name="direction">+1 向右，-1 向左</param>
    /// <param name="targetSlot">目标槽位（-1 表示本地玩家自己的槽）</param>
    public void RequestCycleColor(int direction, int targetSlot = -1) {
        int slot = (targetSlot >= 0) ? targetSlot : localSlotIndex;
        Debug.Log($"[RoomManager] RequestCycleColor dir={direction} targetSlot={targetSlot} → slot={slot} state={slots[slot].state} localSlot={localSlotIndex}");

        // 只有自己的槽或AI槽可以操作
        if (slots[slot].state == SlotState.Human && slot != localSlotIndex) {
            Debug.Log("[RoomManager] RequestCycleColor BLOCKED: 他人Human槽");
            return;
        }

        int current     = slots[slot].colorIndex;
        int next        = current;
        var takenColors = GetTakenColors(slot);
        Debug.Log($"[RoomManager] 当前colorIndex={current} playerId={slots[slot].playerId} takenColors=[{string.Join(",", takenColors)}]");

        int colorCount = PlayerColors.Length;
        for (int i = 0; i < colorCount; i++) {
            next = (next + direction + colorCount) % colorCount;
            if (!takenColors.Contains(next)) break;
        }
        Debug.Log($"[RoomManager] 切换到colorIndex={next} RoomNetworkManager={RoomNetworkManager.Instance}");

        if (RoomNetworkManager.Instance != null)
            RoomNetworkManager.Instance.SendRoomUpdateColorRequest(slots[slot].playerId, next);
        else
            ExecuteNetUpdateColor(slots[slot].playerId, next);
    }

    public void RequestToggleReady() {
        if (isLocalHost) return;
        bool newReady = !slots[localSlotIndex].isReady;
        if (RoomNetworkManager.Instance != null)
            RoomNetworkManager.Instance.SendRoomToggleReadyRequest(localPlayerId, newReady);
        else
            ExecuteNetToggleReady(localPlayerId, newReady);
    }

    // ── 房主操作 ──────────────────────────────────────────

    public void RequestAddAI(int slotIndex) {
        if (!isLocalHost || slots[slotIndex].state != SlotState.Empty) return;
        if (RoomNetworkManager.Instance != null)
            RoomNetworkManager.Instance.SendRoomAddAIRequest(slotIndex);
        else
            ExecuteNetAddAI(slotIndex);
    }

    public void RequestKickSlot(int slotIndex) {
        if (!isLocalHost || slotIndex == localSlotIndex) return;
        if (slots[slotIndex].state == SlotState.Empty) return;
        if (RoomNetworkManager.Instance != null)
            RoomNetworkManager.Instance.SendRoomKickRequest(slotIndex);
        else
            ExecuteNetKick(slotIndex);
    }

    public void RequestSetRounds(int delta) {
        int newCount = Mathf.Clamp(roundCount + delta, 3, 20);
        if (newCount == roundCount) return;
        if (RoomNetworkManager.Instance != null)
            RoomNetworkManager.Instance.SendRoomSetRoundsRequest(newCount);
        else
            ExecuteNetSetRounds(newCount);
    }

    public void RequestStartGame() {
        if (!isLocalHost || !CanStartGame()) return;
        if (RoomNetworkManager.Instance != null)
            RoomNetworkManager.Instance.SendRoomStartRequest();
        else
            ExecuteNetStartGame(slots, roundCount);
    }

    public void RequestLeaveOrDisband() {
        if (isLocalHost) {
            if (RoomNetworkManager.Instance != null)
                RoomNetworkManager.Instance.SendRoomDisbandRequest();
            else
                ExecuteNetDisband();
        } else {
            if (RoomNetworkManager.Instance != null)
                RoomNetworkManager.Instance.SendRoomLeaveRequest(localPlayerId);
            else
                ExecuteNetLeave(localPlayerId);
        }
    }

    // ── ExecuteNet 回调（服务端广播入口）──────────────────

    public void ExecuteNetUpdateName(int playerId, string name) {
        int idx = FindSlotByPlayerId(playerId);
        if (idx < 0) return;
        slots[idx].playerName = name;
        RoomUI.Instance?.RefreshSlot(idx);
    }

    public void ExecuteNetUpdateColor(int playerId, int colorIndex) {
        int idx = FindSlotByPlayerId(playerId);
        Debug.Log($"[RoomManager] ExecuteNetUpdateColor playerId={playerId} colorIndex={colorIndex} idx={idx}");
        if (idx < 0) return;
        slots[idx].colorIndex = colorIndex;
        RoomUI.Instance?.RefreshSlot(idx);
    }

    public void ExecuteNetToggleReady(int playerId, bool isReady) {
        int idx = FindSlotByPlayerId(playerId);
        if (idx < 0) return;
        slots[idx].isReady = isReady;
        RoomUI.Instance?.RefreshSlot(idx);
        RoomUI.Instance?.UpdateStartButton();
    }

    public void ExecuteNetAddAI(int slotIndex) {
        if (slotIndex < 0 || slotIndex >= 6) return;
        int aiColor = GetFirstAvailableColor();
        slots[slotIndex].state      = SlotState.AI;
        slots[slotIndex].playerId   = -(slotIndex + 1);  // 负数标识AI
        slots[slotIndex].playerName = "AI " + (slotIndex + 1);
        slots[slotIndex].colorIndex = aiColor;
        slots[slotIndex].isReady    = true;
        slots[slotIndex].isHost     = false;
        RoomUI.Instance?.RefreshSlot(slotIndex);
        RoomUI.Instance?.UpdateStartButton();
    }

    public void ExecuteNetKick(int slotIndex) {
        if (slotIndex < 0 || slotIndex >= 6) return;
        slots[slotIndex] = new SlotData(slotIndex);
        RoomUI.Instance?.RefreshSlot(slotIndex);
        RoomUI.Instance?.UpdateStartButton();
    }

    public void ExecuteNetSetRounds(int count) {
        roundCount = count;
        RoomUI.Instance?.RefreshSettings();
    }

    public void ExecuteNetStartGame(SlotData[] serverSlots, int serverRoundCount) {
        if (serverSlots != null) ApplyServerSlots(serverSlots);
        if (GameDataManager.Instance != null)
            GameDataManager.Instance.SetupFromRoom(slots, serverRoundCount);
        UnityEngine.SceneManagement.SceneManager.LoadScene("MainGameScene");
    }

    public void ExecuteNetDisband() {
        // TODO: 返回主菜单
        Debug.Log("[RoomManager] 房间已解散（TODO: 返回主菜单）");
    }

    public void ExecuteNetLeave(int playerId) {
        int idx = FindSlotByPlayerId(playerId);
        if (idx < 0) return;
        slots[idx] = new SlotData(idx);
        RoomUI.Instance?.RefreshSlot(idx);
        RoomUI.Instance?.UpdateStartButton();
    }

    // ── 工具方法 ──────────────────────────────────────────

    public bool CanStartGame() {
        bool hasParticipant = false;
        for (int i = 0; i < 6; i++) {
            if (slots[i].isHost || slots[i].state == SlotState.Empty) continue;
            hasParticipant = true;
            if (slots[i].state == SlotState.Human && !slots[i].isReady) return false;
        }
        return hasParticipant;
    }

    public int FindSlotByPlayerId(int playerId) {
        for (int i = 0; i < 6; i++)
            if (slots[i].playerId == playerId) return i;
        return -1;
    }

    HashSet<int> GetTakenColors(int excludeSlot) {
        var taken = new HashSet<int>();
        for (int i = 0; i < 6; i++)
            if (i != excludeSlot && slots[i].state != SlotState.Empty)
                taken.Add(slots[i].colorIndex);
        return taken;
    }

    int GetFirstAvailableColor() {
        var taken = GetTakenColors(-1);
        for (int i = 0; i < PlayerColors.Length; i++)
            if (!taken.Contains(i)) return i;
        return 0;
    }
}
