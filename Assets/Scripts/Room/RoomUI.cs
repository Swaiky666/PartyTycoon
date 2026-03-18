using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// RoomScene 的整体 UI 控制器。
/// 在 Inspector 中将所有 UI 引用拖入，并将 6 个 PlayerSlotUI 拖入 slotUIs 数组。
/// </summary>
public class RoomUI : MonoBehaviour {
    public static RoomUI Instance;

    [Header("顶部")]
    public TextMeshProUGUI roomCodeText;
    public Button backButton;

    [Header("槽位（共6个，按顺序拖入）")]
    public PlayerSlotUI[] slotUIs;

    [Header("房主设置区")]
    public GameObject hostSettingsPanel;
    public TextMeshProUGUI roundCountText;
    public Button roundsMinusBtn;
    public Button roundsPlusBtn;
    // TODO: 地图选择（计划中，暂不实现）

    [Header("开始游戏按钮")]
    public Button startButton;
    public CanvasGroup startButtonGroup; // 用于透明度变暗，拖入按钮同级或父级 CanvasGroup

    void Awake() { Instance = this; }

    void Start() {
        backButton.onClick.AddListener(OnBackClicked);
        startButton.onClick.AddListener(OnStartClicked);
        if (roundsMinusBtn != null) roundsMinusBtn.onClick.AddListener(() => RoomManager.Instance?.RequestSetRounds(-1));
        if (roundsPlusBtn  != null) roundsPlusBtn.onClick.AddListener(() => RoomManager.Instance?.RequestSetRounds(1));

        // 根据主菜单传入的 pending action 决定创建或加入
        var gdm = GameDataManager.Instance;
        string playerName = gdm != null ? gdm.pendingPlayerName : "玩家1";
        if (gdm == null || gdm.pendingRoomAction == PendingRoomAction.CreateRoom)
            RoomManager.Instance?.RequestCreateRoom(playerName);
        else
            RoomManager.Instance?.RequestJoinRoom(gdm.pendingRoomCode, playerName);
    }

    // ── 刷新 ──────────────────────────────────────────────

    public void RefreshAll() {
        RefreshRoomCode();
        for (int i = 0; i < slotUIs.Length; i++) RefreshSlot(i);
        RefreshSettings();
        UpdateStartButton();

        bool iAmHost = RoomManager.Instance != null && RoomManager.Instance.isLocalHost;
        if (hostSettingsPanel != null) hostSettingsPanel.SetActive(iAmHost);
        if (startButton != null) startButton.gameObject.SetActive(iAmHost);
    }

    public void RefreshSlot(int index) {
        if (index < 0 || index >= slotUIs.Length || RoomManager.Instance == null) return;
        var rm   = RoomManager.Instance;
        bool isMe = rm.localSlotIndex == index;
        slotUIs[index].SetData(rm.slots[index], isMe, rm.isLocalHost);
    }

    public void RefreshSettings() {
        if (roundCountText != null && RoomManager.Instance != null)
            roundCountText.text = RoomManager.Instance.roundCount.ToString();
    }

    public void UpdateStartButton() {
        if (startButton == null || RoomManager.Instance == null) return;
        bool iAmHost  = RoomManager.Instance.isLocalHost;
        bool canStart = RoomManager.Instance.CanStartGame();

        startButton.gameObject.SetActive(iAmHost);
        startButton.interactable = canStart;
        if (startButtonGroup != null)
            startButtonGroup.alpha = canStart ? 1f : 0.4f;
    }

    void RefreshRoomCode() {
        if (roomCodeText != null && RoomManager.Instance != null)
            roomCodeText.text = "房间码: " + RoomManager.Instance.roomCode;
    }

    // ── 按钮回调 ──────────────────────────────────────────

    void OnBackClicked() {
        RoomManager.Instance?.RequestLeaveOrDisband();
        SceneManager.LoadScene("MainMenuScene");
    }

    void OnStartClicked() {
        RoomManager.Instance?.RequestStartGame();
    }
}
