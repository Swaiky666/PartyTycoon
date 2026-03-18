using UnityEngine;
using System;
using System.Collections.Generic;
using NativeWebSocket;

/// <summary>
/// WebSocket 连接管理器（DontDestroyOnLoad）。
/// 负责连接/断连/发送消息，并将收到的消息按 cmd 字段分发给已注册的处理器。
///
/// 依赖：NativeWebSocket（支持 Editor/Desktop/WebGL）
/// 安装：Unity Package Manager → Add from Git URL →
///   https://github.com/endel/NativeWebSocket.git#upm
/// </summary>
public class WebSocketManager : MonoBehaviour {
    public static WebSocketManager Instance;

    [Header("服务器")]
    public string serverUrl = "ws://localhost:3000";

    WebSocket _ws;
    readonly Dictionary<string, Action<string>> _handlers = new Dictionary<string, Action<string>>();

    public bool IsConnected => _ws?.State == WebSocketState.Open;

    public event Action OnConnected;

    void Awake() {
        if (Instance == null) {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        } else {
            Destroy(gameObject);
        }
    }

    // ── 连接 ─────────────────────────────────────────────────

    public async void Connect() {
        if (_ws != null && _ws.State == WebSocketState.Open) return;

        _ws = new WebSocket(serverUrl);
        _ws.OnOpen    += () => { Debug.Log("[WS] Connected: " + serverUrl); OnConnected?.Invoke(); };
        _ws.OnError   += (e) => Debug.LogError("[WS] Error: " + e);
        _ws.OnClose   += (e) => Debug.Log("[WS] Closed: " + e);
        _ws.OnMessage += OnRawMessage;

        await _ws.Connect();
    }

    public async void Disconnect() {
        if (_ws != null) await _ws.Close();
    }

    // ── 发送 ─────────────────────────────────────────────────

    public async void Send(string json) {
        if (_ws == null || _ws.State != WebSocketState.Open) {
            Debug.LogWarning("[WS] Not connected, dropped: " + json);
            return;
        }
        await _ws.SendText(json);
    }

    // ── 消息处理器注册 ────────────────────────────────────────

    public void Register(string cmd, Action<string> handler)  => _handlers[cmd] = handler;
    public void Unregister(string cmd)                         => _handlers.Remove(cmd);

    // ── 内部分发 ──────────────────────────────────────────────

    void OnRawMessage(byte[] bytes) {
        string json = System.Text.Encoding.UTF8.GetString(bytes);
        try {
            var w = JsonUtility.FromJson<CmdWrapper>(json);
            if (_handlers.TryGetValue(w.cmd, out var h))
                h(json);
            else
                Debug.Log("[WS] Unhandled cmd: " + w.cmd);
        } catch (Exception e) {
            Debug.LogError("[WS] Parse error: " + e.Message + "\n" + json);
        }
    }

    void Update() {
        // 非 WebGL 平台需要手动 Dispatch（NativeWebSocket 要求）
#if !UNITY_WEBGL || UNITY_EDITOR
        _ws?.DispatchMessageQueue();
#endif
    }

    void OnDestroy() {
        _ws?.Close();
    }

    [Serializable] class CmdWrapper { public string cmd; }
}
