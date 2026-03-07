using UnityEngine;
using System.Collections;

/// <summary>
/// 房间网络中间层，遵循 Send/ExecuteNet 模式。
/// 真联网阶段只需将协程替换为实际消息发送，RoomManager 无需改动。
/// </summary>
public class RoomNetworkManager : MonoBehaviour {
    public static RoomNetworkManager Instance;

    [Header("模拟延迟（秒）")]
    public float simulatedLatency = 0.05f;

    void Awake() { Instance = this; }

    // ── Send 入口 ─────────────────────────────────────────

    public void SendRoomUpdateNameRequest(int playerId, string name) =>
        StartCoroutine(Simulate(() => RoomManager.Instance?.ExecuteNetUpdateName(playerId, name)));

    public void SendRoomUpdateColorRequest(int playerId, int colorIndex) =>
        StartCoroutine(Simulate(() => RoomManager.Instance?.ExecuteNetUpdateColor(playerId, colorIndex)));

    public void SendRoomToggleReadyRequest(int playerId, bool isReady) =>
        StartCoroutine(Simulate(() => RoomManager.Instance?.ExecuteNetToggleReady(playerId, isReady)));

    public void SendRoomAddAIRequest(int slotIndex) =>
        StartCoroutine(Simulate(() => RoomManager.Instance?.ExecuteNetAddAI(slotIndex)));

    public void SendRoomKickRequest(int slotIndex) =>
        StartCoroutine(Simulate(() => RoomManager.Instance?.ExecuteNetKick(slotIndex)));

    public void SendRoomSetRoundsRequest(int count) =>
        StartCoroutine(Simulate(() => RoomManager.Instance?.ExecuteNetSetRounds(count)));

    public void SendRoomStartRequest() =>
        StartCoroutine(Simulate(() => RoomManager.Instance?.ExecuteNetStartGame()));

    public void SendRoomDisbandRequest() =>
        StartCoroutine(Simulate(() => RoomManager.Instance?.ExecuteNetDisband()));

    public void SendRoomLeaveRequest(int playerId) =>
        StartCoroutine(Simulate(() => RoomManager.Instance?.ExecuteNetLeave(playerId)));

    // ── 内部模拟 ──────────────────────────────────────────

    IEnumerator Simulate(System.Action callback) {
        yield return new WaitForSeconds(simulatedLatency);
        callback?.Invoke();
    }
}
