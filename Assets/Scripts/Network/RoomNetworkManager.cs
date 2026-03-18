using UnityEngine;
using System;
using UnityEngine.SceneManagement;

/// <summary>
/// 房间阶段网络管理器。
/// 向服务端发送房间相关指令，并将收到的广播转交 RoomManager 处理。
/// </summary>
public class RoomNetworkManager : MonoBehaviour {
    public static RoomNetworkManager Instance;

    void Awake() {
        Instance = this;
        RegisterHandlers();
    }

    void Start() {
        WebSocketManager.Instance?.Connect();
    }

    void OnDestroy() {
        UnregisterHandlers();
    }

    // ── 注册/注销 ─────────────────────────────────────────────

    static readonly string[] _cmds = {
        "ROOM_CREATED", "JOIN_SUCCESS", "JOIN_FAILED",
        "PLAYER_JOINED", "PLAYER_LEFT",
        "COLOR_UPDATED", "NAME_UPDATED", "READY_UPDATED",
        "AI_ADDED", "PLAYER_KICKED",
        "ROUNDS_SET", "GAME_STARTED",
        "ROOM_DISBANDED", "KICKED",
    };

    void RegisterHandlers() {
        var ws = WebSocketManager.Instance;
        if (ws == null) return;
        ws.Register("ROOM_CREATED",  OnRoomCreated);
        ws.Register("JOIN_SUCCESS",  OnJoinSuccess);
        ws.Register("JOIN_FAILED",   OnJoinFailed);
        ws.Register("PLAYER_JOINED", OnSlotsUpdate);
        ws.Register("PLAYER_LEFT",   OnSlotsUpdate);
        ws.Register("COLOR_UPDATED", OnColorUpdated);
        ws.Register("NAME_UPDATED",  OnNameUpdated);
        ws.Register("READY_UPDATED", OnReadyUpdated);
        ws.Register("AI_ADDED",      OnSlotsUpdate);
        ws.Register("PLAYER_KICKED", OnSlotsUpdate);
        ws.Register("ROUNDS_SET",    OnRoundsSet);
        ws.Register("GAME_STARTED",  OnGameStarted);
        ws.Register("ROOM_DISBANDED",OnRoomDisbanded);
        ws.Register("KICKED",        OnKicked);
    }

    void UnregisterHandlers() {
        var ws = WebSocketManager.Instance;
        if (ws == null) return;
        foreach (var cmd in _cmds) ws.Unregister(cmd);
    }

    // ── Send 请求 ─────────────────────────────────────────────

    public void SendCreateRoom(string playerName) =>
        WebSocketManager.Instance?.Send($"{{\"cmd\":\"CREATE_ROOM\",\"playerName\":\"{Esc(playerName)}\"}}");

    public void SendJoinRoom(string roomCode, string playerName) =>
        WebSocketManager.Instance?.Send($"{{\"cmd\":\"JOIN_ROOM\",\"roomCode\":\"{roomCode}\",\"playerName\":\"{Esc(playerName)}\"}}");

    public void SendRoomUpdateColorRequest(int playerId, int colorIndex) =>
        WebSocketManager.Instance?.Send($"{{\"cmd\":\"UPDATE_COLOR\",\"colorIndex\":{colorIndex}}}");

    public void SendRoomUpdateNameRequest(int playerId, string name) =>
        WebSocketManager.Instance?.Send($"{{\"cmd\":\"UPDATE_NAME\",\"name\":\"{Esc(name)}\"}}");

    public void SendRoomToggleReadyRequest(int playerId, bool isReady) =>
        WebSocketManager.Instance?.Send("{\"cmd\":\"TOGGLE_READY\"}");

    public void SendRoomAddAIRequest(int slotIndex) =>
        WebSocketManager.Instance?.Send($"{{\"cmd\":\"ADD_AI\",\"slotIndex\":{slotIndex}}}");

    public void SendRoomKickRequest(int slotIndex) =>
        WebSocketManager.Instance?.Send($"{{\"cmd\":\"KICK_PLAYER\",\"slotIndex\":{slotIndex}}}");

    public void SendRoomSetRoundsRequest(int count) =>
        WebSocketManager.Instance?.Send($"{{\"cmd\":\"SET_ROUNDS\",\"count\":{count}}}");

    public void SendRoomStartRequest() =>
        WebSocketManager.Instance?.Send("{\"cmd\":\"START_GAME\"}");

    public void SendRoomDisbandRequest() =>
        WebSocketManager.Instance?.Send("{\"cmd\":\"LEAVE_ROOM\"}");

    public void SendRoomLeaveRequest(int playerId) =>
        WebSocketManager.Instance?.Send("{\"cmd\":\"LEAVE_ROOM\"}");

    // ── 收到消息 ──────────────────────────────────────────────

    void OnRoomCreated(string json) {
        var msg = JsonUtility.FromJson<RoomInitMsg>(json);
        RoomManager.Instance?.ExecuteNetRoomInit(msg.roomCode, msg.playerId, msg.slotIndex, msg.slots, msg.roundCount, isHost: true);
    }

    void OnJoinSuccess(string json) {
        var msg = JsonUtility.FromJson<RoomInitMsg>(json);
        RoomManager.Instance?.ExecuteNetRoomInit(msg.roomCode, msg.playerId, msg.slotIndex, msg.slots, msg.roundCount, isHost: false);
    }

    void OnJoinFailed(string json) {
        var msg = JsonUtility.FromJson<ReasonMsg>(json);
        Debug.LogWarning("[Room] 加入失败: " + msg.reason);
        // TODO: 显示错误提示给用户（通过 RoomUI 或 MainMenuUI 反馈）
    }

    void OnSlotsUpdate(string json) {
        var msg = JsonUtility.FromJson<SlotsMsg>(json);
        RoomManager.Instance?.ExecuteNetFullSlotsUpdate(msg.slots);
    }

    void OnColorUpdated(string json) {
        var msg = JsonUtility.FromJson<ColorMsg>(json);
        RoomManager.Instance?.ExecuteNetUpdateColor(msg.playerId, msg.colorIndex);
    }

    void OnNameUpdated(string json) {
        var msg = JsonUtility.FromJson<NameMsg>(json);
        RoomManager.Instance?.ExecuteNetUpdateName(msg.playerId, msg.name);
    }

    void OnReadyUpdated(string json) {
        var msg = JsonUtility.FromJson<ReadyMsg>(json);
        RoomManager.Instance?.ExecuteNetToggleReady(msg.playerId, msg.isReady);
    }

    void OnRoundsSet(string json) {
        var msg = JsonUtility.FromJson<RoundsMsg>(json);
        RoomManager.Instance?.ExecuteNetSetRounds(msg.count);
    }

    void OnGameStarted(string json) {
        var msg = JsonUtility.FromJson<GameStartedMsg>(json);
        RoomManager.Instance?.ExecuteNetStartGame(msg.slots, msg.roundCount);
    }

    void OnRoomDisbanded(string _) => RoomManager.Instance?.ExecuteNetDisband();

    void OnKicked(string _) => SceneManager.LoadScene("MainMenuScene");

    // ── 工具 ─────────────────────────────────────────────────

    static string Esc(string s) => (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");

    // ── 消息结构体 ────────────────────────────────────────────

    [Serializable] class RoomInitMsg {
        public string    cmd;
        public string    roomCode;
        public int       playerId;
        public int       slotIndex;
        public SlotData[] slots;
        public int       roundCount;
    }

    [Serializable] class SlotsMsg {
        public string    cmd;
        public SlotData[] slots;
    }

    [Serializable] class ColorMsg {
        public string cmd;
        public int    playerId;
        public int    colorIndex;
    }

    [Serializable] class NameMsg {
        public string cmd;
        public int    playerId;
        public string name;
    }

    [Serializable] class ReadyMsg {
        public string cmd;
        public int    playerId;
        public bool   isReady;
    }

    [Serializable] class RoundsMsg {
        public string cmd;
        public int    count;
    }

    [Serializable] class GameStartedMsg {
        public string    cmd;
        public SlotData[] slots;
        public int       roundCount;
    }

    [Serializable] class ReasonMsg {
        public string cmd;
        public string reason;
    }
}
