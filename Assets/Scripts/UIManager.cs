using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System;

public class UIManager : MonoBehaviour {
    public static UIManager Instance;

    [Header("公共 UI 组件")]
    public TextMeshProUGUI globalStatusText; 
    public TextMeshProUGUI playerStatsText; // 玩家金币/信息文本
    public Button actionButton;              

    [Header("功能切换按钮")]
    public Button viewButton;  
    public Button cardButton;  

    private TextMeshProUGUI buttonText;      

    void Awake() {
        if (Instance == null) Instance = this;
        if (actionButton != null) {
            buttonText = actionButton.GetComponentInChildren<TextMeshProUGUI>();
        }
    }

    // 更新上方通知信息
    public void UpdateStatus(string message) {
        if (globalStatusText != null) globalStatusText.text = message;
    }

    // 更新并显示玩家资产
    public void UpdatePlayerStats(PlayerController p) {
        if (playerStatsText != null) {
            playerStatsText.text = $"玩家 {p.playerId} | 金币: <color=yellow>${p.money}</color>";
        }
    }

    // 核心修改：控制玩家信息文本的显隐
    public void SetPlayerStatsVisible(bool visible) {
        if (playerStatsText != null) {
            playerStatsText.gameObject.SetActive(visible);
        }
    }

    // 配置万能按钮
    public void ShowActionButton(string label, Action callback) {
        if (actionButton == null) return;
        actionButton.gameObject.SetActive(true);
        if (buttonText != null) buttonText.text = label;
        actionButton.onClick.RemoveAllListeners();
        actionButton.onClick.AddListener(() => callback?.Invoke());
    }

    public void HideActionButton() {
        if (actionButton != null) actionButton.gameObject.SetActive(false);
    }

    // 控制侧边功能按钮显隐
    public void SetExtraButtonsVisible(bool visible) {
        if(viewButton) viewButton.gameObject.SetActive(visible);
        if(cardButton) cardButton.gameObject.SetActive(visible);
    }

    public void SetViewButtonLabel(string label) {
        if(viewButton) viewButton.GetComponentInChildren<TextMeshProUGUI>().text = label;
    }

    public void SetCardButtonLabel(string label) {
        if(cardButton) cardButton.GetComponentInChildren<TextMeshProUGUI>().text = label;
    }
}