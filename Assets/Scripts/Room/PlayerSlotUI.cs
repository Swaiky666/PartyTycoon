using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 单个玩家槽位的 UI 组件。
/// Inspector 绑定所有子物件引用；按钮回调通过 OnClick 事件绑定或直接在代码中 AddListener。
/// </summary>
public class PlayerSlotUI : MonoBehaviour {

    [Header("顶层面板（二选一显示）")]
    public GameObject emptyPanel;    // 空槽（含+按钮）
    public GameObject occupiedPanel; // 有人或AI时显示

    [Header("空槽元素")]
    public Button addAIButton;
    public GameObject emptyIcon;       // 必须放在 emptyPanel 内或槽位根节点，不能放在 occupiedPanel 内

    [Header("占用槽元素")]
    public Image  colorImage;
    public TextMeshProUGUI colorText;
    public TMP_InputField nameInput;   // 仅本地玩家槽可编辑
    public TextMeshProUGUI nameLabel;  // AI 或他人名字（只读）
    public GameObject readyIcon;
    public GameObject unreadyIcon;
    public GameObject hostIcon;
    public GameObject aiIcon;
    public Button colorLeftBtn;
    public Button colorRightBtn;
    public Button readyButton;
    public TextMeshProUGUI readyBtnText;
    public Button kickButton;          // 房主才可见

    int _slotIndex;

    void Awake() {
        Debug.Log($"[PlayerSlotUI] Awake {gameObject.name} colorLeftBtn={colorLeftBtn} colorRightBtn={colorRightBtn}");
        nameInput?.onEndEdit.AddListener(OnNameEndEdit);
        addAIButton?.onClick.AddListener(OnAddAIClicked);
        colorLeftBtn?.onClick.AddListener(OnColorLeftClicked);
        colorRightBtn?.onClick.AddListener(OnColorRightClicked);
        readyButton?.onClick.AddListener(OnReadyClicked);
        kickButton?.onClick.AddListener(OnKickClicked);
    }

    /// <summary>由 RoomUI.RefreshSlot 调用，根据数据刷新显示。</summary>
    public void SetData(SlotData data, bool isMe, bool iAmHost) {
        _slotIndex = data.slotIndex;
        bool isEmpty = data.state == SlotState.Empty;
        bool isAI    = data.state == SlotState.AI;

        emptyPanel.SetActive(isEmpty);
        occupiedPanel.SetActive(!isEmpty);

        if (isEmpty) {
            if (addAIButton != null) addAIButton.gameObject.SetActive(iAmHost);
            if (colorText   != null) colorText.gameObject.SetActive(false);
            if (emptyIcon   != null) emptyIcon.SetActive(true);
            return;
        }
        if (emptyIcon != null) emptyIcon.SetActive(false);

        // 颜色色块与提示文字
        if (colorImage != null)
            colorImage.color = RoomManager.PlayerColors[data.colorIndex];
        if (colorText != null)
            colorText.gameObject.SetActive(true);

        // 名字：本地玩家用 InputField，其他人用 Label
        bool showInput = isMe && !isAI;
        if (nameInput != null) {
            nameInput.gameObject.SetActive(showInput);
            if (showInput) nameInput.SetTextWithoutNotify(data.playerName);
        }
        if (nameLabel != null) {
            nameLabel.gameObject.SetActive(!showInput);
            if (!showInput) nameLabel.text = data.playerName;
        }

        // 状态图标
        bool showReadyIcons = !data.isHost && !isAI;
        if (readyIcon   != null) readyIcon.SetActive(showReadyIcons && data.isReady);
        if (unreadyIcon != null) unreadyIcon.SetActive(showReadyIcons && !data.isReady);
        if (hostIcon    != null) hostIcon.SetActive(data.isHost);
        if (aiIcon      != null) aiIcon.SetActive(isAI);

        // 颜色切换箭头：自己的槽，或任何人都可改的AI槽
        bool showColor = isMe || isAI;
        if (colorLeftBtn  != null) colorLeftBtn.gameObject.SetActive(showColor);
        if (colorRightBtn != null) colorRightBtn.gameObject.SetActive(showColor);

        // 准备按钮（仅本地且非房主）
        bool showReady = isMe && !data.isHost;
        if (readyButton != null) {
            readyButton.gameObject.SetActive(showReady);
            if (showReady && readyBtnText != null)
                readyBtnText.text = data.isReady ? "取消准备" : "准备";
        }

        // 踢出按钮（房主可见，非自己，非空）
        if (kickButton != null)
            kickButton.gameObject.SetActive(iAmHost && !isMe);

        if (colorLeftBtn != null)
            Debug.Log($"[PlayerSlotUI] SetData slot={_slotIndex} colorLeftBtn active={colorLeftBtn.gameObject.activeSelf} activeInHierarchy={colorLeftBtn.gameObject.activeInHierarchy} interactable={colorLeftBtn.interactable}");
    }

    // ── 按钮回调（Inspector OnClick 或 Awake AddListener）──

    public void OnAddAIClicked()     => RoomManager.Instance?.RequestAddAI(_slotIndex);
    public void OnColorLeftClicked() {
        Debug.Log($"[PlayerSlotUI] OnColorLeftClicked slotIndex={_slotIndex} RoomManager={RoomManager.Instance}");
        RoomManager.Instance?.RequestCycleColor(-1, _slotIndex);
    }
    public void OnColorRightClicked() {
        Debug.Log($"[PlayerSlotUI] OnColorRightClicked slotIndex={_slotIndex} RoomManager={RoomManager.Instance}");
        RoomManager.Instance?.RequestCycleColor(1, _slotIndex);
    }
    public void OnReadyClicked()     => RoomManager.Instance?.RequestToggleReady();
    public void OnKickClicked()      => RoomManager.Instance?.RequestKickSlot(_slotIndex);
    void OnNameEndEdit(string newName) => RoomManager.Instance?.RequestUpdateName(newName);
}
