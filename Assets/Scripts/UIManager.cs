using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System;
using System.Collections.Generic;

public class UIManager : MonoBehaviour {
    public static UIManager Instance;

    [Header("公共 UI 组件")]
    public TextMeshProUGUI globalStatusText; 
    public TextMeshProUGUI playerStatsText; 
    public Button actionButton;              

    [Header("功能切换按钮")]
    public Button viewButton;  
    public Button cardButton;
    public Button testSwitchSceneButton; 

    private TextMeshProUGUI buttonText;      

    void Awake() {
        if (Instance == null) Instance = this;
        if (actionButton != null) {
            buttonText = actionButton.GetComponentInChildren<TextMeshProUGUI>();
        }

        if (testSwitchSceneButton != null) {
            testSwitchSceneButton.onClick.AddListener(OnTestSwitchClicked);
        }
    }

    private void OnTestSwitchClicked() {
        // 修复：更新方法名为 SwitchToRandomMinigame
        if (TurnManager.Instance != null && TurnManager.Instance.allPlayers != null) {
            GameDataManager.Instance.SwitchToRandomMinigame(TurnManager.Instance.allPlayers);
        } else {
            Debug.LogError("UIManager: 无法找到玩家列表，请检查 TurnManager.allPlayers 是否公开。");
        }
    }

    public void UpdateStatus(string message) {
        if (globalStatusText != null) globalStatusText.text = message;
    }

    public void UpdatePlayerStats(PlayerController p) {
        if (playerStatsText != null && p != null) {
            playerStatsText.text = $"玩家 {p.playerId} | 金币: <color=yellow>${p.money}</color>";
        }
    }

    public void SetPlayerStatsVisible(bool visible) {
        if (playerStatsText != null) playerStatsText.gameObject.SetActive(visible);
    }

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

    public void SetExtraButtonsVisible(bool visible) {
        if(viewButton) viewButton.gameObject.SetActive(visible);
        if(cardButton) cardButton.gameObject.SetActive(visible);
        if(testSwitchSceneButton) testSwitchSceneButton.gameObject.SetActive(visible);
    }

    public void SetViewButtonLabel(string label) {
        if(viewButton != null) {
            var txt = viewButton.GetComponentInChildren<TextMeshProUGUI>();
            if(txt != null) txt.text = label;
        }
    }

    public void SetCardButtonLabel(string label) {
        if(cardButton != null) {
            var txt = cardButton.GetComponentInChildren<TextMeshProUGUI>();
            if(txt != null) txt.text = label;
        }
    }
}