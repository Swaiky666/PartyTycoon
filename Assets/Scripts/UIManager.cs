using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System;

public class UIManager : MonoBehaviour {
    public static UIManager Instance;

    [Header("公共 UI 组件")]
    public TextMeshProUGUI globalStatusText; 
    public Button actionButton;              
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

    // 配置万能按钮：文字内容 + 点击后执行的逻辑
    public void ShowActionButton(string label, Action callback) {
        if (actionButton == null) return;
        
        actionButton.gameObject.SetActive(true); // 修正：使用 .gameObject
        if (buttonText != null) buttonText.text = label;

        actionButton.onClick.RemoveAllListeners();
        actionButton.onClick.AddListener(() => {
            callback?.Invoke();
        });
    }

    public void HideActionButton() {
        if (actionButton != null) {
            actionButton.gameObject.SetActive(false); // 修正：使用 .gameObject
        }
    }
}