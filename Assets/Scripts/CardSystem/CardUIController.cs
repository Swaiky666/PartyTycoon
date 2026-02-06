using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;
using DG.Tweening; 

public class CardUIController : MonoBehaviour {
    public static CardUIController Instance;

    [Header("UI 引用")]
    public GameObject cardUIPanel;      
    public RectTransform contentHolder; 
    public GameObject cardPrefab;       

    [Header("卡牌显示设置")]
    public float cardWidth = 500f;     
    public float cardHeight = 750f;    
    public float cardSpacing = 650f;   
    public float swipeSensitivity = 1.1f; 

    private List<GameObject> spawnedCards = new List<GameObject>();
    private List<CardBase> currentDataList;
    private float minX; 
    private float maxX; 

    void Awake() { Instance = this; }

    public void Show(List<CardBase> playerCards) {
        if (playerCards == null || playerCards.Count == 0) return;
        currentDataList = playerCards;
        cardUIPanel.SetActive(true);
        
        if (UIManager.Instance.viewButton != null) 
            UIManager.Instance.viewButton.gameObject.SetActive(false);

        SetupCardButton("返回", OnBackFromList);
        RefreshCardList();
    }

    private void RefreshCardList() {
        DOTween.Kill(contentHolder);
        foreach (var c in spawnedCards) if(c != null) Destroy(c);
        spawnedCards.Clear();

        contentHolder.anchorMin = contentHolder.anchorMax = contentHolder.pivot = new Vector2(0.5f, 0.5f);
        contentHolder.sizeDelta = new Vector2(contentHolder.sizeDelta.x, cardHeight);

        maxX = 0; 
        minX = -(currentDataList.Count - 1) * cardSpacing;

        for (int i = 0; i < currentDataList.Count; i++) {
            GameObject cardObj = Instantiate(cardPrefab, contentHolder);
            cardObj.name = i.ToString(); 
            
            RectTransform rt = cardObj.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(cardWidth, cardHeight); 
            rt.anchoredPosition = new Vector2(i * cardSpacing, 0);
            
            UpdateCardVisuals(cardObj, currentDataList[i]);
            
            if (!cardObj.GetComponent<CardDragHandler>()) 
                cardObj.AddComponent<CardDragHandler>();
                
            spawnedCards.Add(cardObj);
        }
        contentHolder.anchoredPosition = Vector2.zero;
    }

    public void OnDragging(Vector2 delta) {
        DOTween.Kill(contentHolder);
        float newX = contentHolder.anchoredPosition.x + delta.x * swipeSensitivity;
        newX = Mathf.Clamp(newX, minX - 300f, maxX + 300f);
        contentHolder.anchoredPosition = new Vector2(newX, 0);
    }

    public void OnDragEnd(float velocityX) {
        float inertia = velocityX * 0.12f;
        float predictedX = contentHolder.anchoredPosition.x + inertia;
        int targetIndex = Mathf.RoundToInt(-predictedX / cardSpacing);
        targetIndex = Mathf.Clamp(targetIndex, 0, currentDataList.Count - 1);
        float finalX = -targetIndex * cardSpacing;
        contentHolder.DOAnchorPosX(finalX, 0.45f).SetEase(Ease.OutBack);
    }

    private void UpdateCardVisuals(GameObject cardObj, CardBase data) {
        TextMeshProUGUI[] texts = cardObj.GetComponentsInChildren<TextMeshProUGUI>();
        foreach (var t in texts) {
            if (t.gameObject.name == "Name") t.text = data.cardName;
            else if (t.gameObject.name == "Desc") t.text = data.description;
            t.raycastTarget = false; 
        }

        Transform iconTransform = cardObj.transform.Find("Icon");
        if (iconTransform != null) {
            Image img = iconTransform.GetComponent<Image>();
            if (img != null && data.cardIcon != null) {
                img.sprite = data.cardIcon;
                img.raycastTarget = false;
            }
        }
    }

    // 关键修改点：点击具体卡牌后的行为
    public void OnCardClicked(int index) {
        if (currentDataList == null || index >= currentDataList.Count) return;
        CardBase selectedCard = currentDataList[index];
        
        cardUIPanel.SetActive(false);

        // --- 核心修复：通知 TurnManager 进入目标选择模式（垂直俯视+平移） ---
        if (TurnManager.Instance != null) {
            TurnManager.Instance.EnterCardTargetingMode();
        }

        if (CardRangeFinder.Instance != null) CardRangeFinder.Instance.ShowRange(selectedCard);
        
        // 将“道具卡”按钮配置为“取消”
        SetupCardButton("取消", () => {
            if (CardRangeFinder.Instance != null) CardRangeFinder.Instance.ClearHighlight();
            // 取消后回到 StartTurn（会重置视角和 UI）
            if (TurnManager.Instance != null) TurnManager.Instance.StartTurn();
        });

        // 确保“取消”按钮是激活的
        if (UIManager.Instance.cardButton != null) 
            UIManager.Instance.cardButton.gameObject.SetActive(true);
    }

    public void HideUI() {
        DOTween.Kill(contentHolder);
        cardUIPanel.SetActive(false);
    }

    private void OnBackFromList() {
        HideUI();
        if (TurnManager.Instance != null) TurnManager.Instance.StartTurn();
    }

    private void SetupCardButton(string label, UnityEngine.Events.UnityAction action) {
        if (UIManager.Instance.cardButton != null) {
            UIManager.Instance.SetCardButtonLabel(label);
            UIManager.Instance.cardButton.onClick.RemoveAllListeners();
            UIManager.Instance.cardButton.onClick.AddListener(action);
        }
    }
}