using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// 主菜单 UI 控制器。
/// 在 Inspector 中绑定四个按钮和设置面板引用。
/// </summary>
public class MainMenuUI : MonoBehaviour {

    [Header("主按钮")]
    public GameObject mainButtonsPanel; // 包含四个主按钮的父节点，切换子面板时整体隐藏
    public Button hostButton;       // 主持游戏
    public Button joinButton;       // 加入游戏
    public Button settingsButton;   // 设置
    public Button quitButton;       // 退出游戏

    [Header("加入游戏面板")]
    public GameObject joinPanel;            // 输入房间码的面板
    public TMPro.TMP_InputField roomCodeInput;
    public Button joinConfirmButton;
    public Button joinCancelButton;

    [Header("设置面板")]
    public GameObject settingsPanel;
    public Button settingsCloseButton;
    // TODO: 音量滑块、语言选项等待后续接入

    void Start() {
        hostButton.onClick.AddListener(OnHostClicked);
        joinButton.onClick.AddListener(OnJoinClicked);
        settingsButton.onClick.AddListener(OnSettingsClicked);
        quitButton.onClick.AddListener(OnQuitClicked);

        if (joinConfirmButton  != null) joinConfirmButton.onClick.AddListener(OnJoinConfirm);
        if (joinCancelButton   != null) joinCancelButton.onClick.AddListener(CloseJoinPanel);
        if (settingsCloseButton != null) settingsCloseButton.onClick.AddListener(CloseSettingsPanel);

        CloseJoinPanel();
        CloseSettingsPanel();
    }

    // ── 主按钮回调 ────────────────────────────────────────

    void OnHostClicked() {
        if (GameDataManager.Instance != null)
            GameDataManager.Instance.pendingRoomAction = PendingRoomAction.CreateRoom;
        SceneManager.LoadScene("RoomScene");
    }

    void OnJoinClicked() {
        if (mainButtonsPanel != null) mainButtonsPanel.SetActive(false);
        if (joinPanel != null) joinPanel.SetActive(true);
    }

    void OnSettingsClicked() {
        if (mainButtonsPanel != null) mainButtonsPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(true);
    }

    void OnQuitClicked() {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ── 加入游戏面板 ──────────────────────────────────────

    void OnJoinConfirm() {
        string code = roomCodeInput != null ? roomCodeInput.text.Trim().ToUpper() : "";
        if (code.Length != 6) {
            Debug.Log("[MainMenuUI] 房间码长度不对: " + code);
            return;
        }
        if (GameDataManager.Instance != null) {
            GameDataManager.Instance.pendingRoomAction = PendingRoomAction.JoinRoom;
            GameDataManager.Instance.pendingRoomCode   = code;
        }
        SceneManager.LoadScene("RoomScene");
    }

    void CloseJoinPanel() {
        if (joinPanel != null) joinPanel.SetActive(false);
        if (mainButtonsPanel != null) mainButtonsPanel.SetActive(true);
    }

    // ── 设置面板 ──────────────────────────────────────────

    void CloseSettingsPanel() {
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (mainButtonsPanel != null) mainButtonsPanel.SetActive(true);
    }
}
