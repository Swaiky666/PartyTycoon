using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System;

public class UIManager : MonoBehaviour {
    public static UIManager Instance;

    public TextMeshProUGUI globalStatusText; 
    public Button actionButton;              
    
    // --- 新增：功能按钮 ---
    public Button viewButton;  // 切换视角按钮
    public Button cardButton;  // 切换卡牌按钮
    
    void Awake() {
        if (Instance == null) Instance = this;
    }

    public void UpdateStatus(string message) {
        if (globalStatusText != null) globalStatusText.text = message;
    }

    public void ShowActionButton(string label, Action callback) {
        actionButton.gameObject.SetActive(true);
        actionButton.GetComponentInChildren<TextMeshProUGUI>().text = label;
        actionButton.onClick.RemoveAllListeners();
        actionButton.onClick.AddListener(() => callback?.Invoke());
    }

    public void HideActionButton() {
        actionButton.gameObject.SetActive(false);
    }

    // --- 新增：快捷控制面板显示隐藏 ---
    public void SetExtraButtonsVisible(bool visible) {
        viewButton.gameObject.SetActive(visible);
        cardButton.gameObject.SetActive(visible);
    }

    public void SetViewButtonLabel(string label) {
        viewButton.GetComponentInChildren<TextMeshProUGUI>().text = label;
    }

    public void SetCardButtonLabel(string label) {
        cardButton.GetComponentInChildren<TextMeshProUGUI>().text = label;
    }
}