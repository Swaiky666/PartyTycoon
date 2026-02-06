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

    [Header("卡牌尺寸控制")]
    public float cardWidth = 500f;     
    public float cardHeight = 750f;    // 现在支持在 Inspector 自由调整高度
    public float cardSpacing = 650f;   
    public float swipeSensitivity = 1.1f; 

    private List<GameObject> spawnedCards = new List<GameObject>();
    private List<CardBase> currentDataList;
    private float minX; 
    private float maxX; 

    void Awake() { Instance = this; }

    public void HideUI() {
        DOTween.Kill(contentHolder);
        cardUIPanel.SetActive(false);
        if (UIManager.Instance.viewButton) UIManager.Instance.viewButton.gameObject.SetActive(true);
    }

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

        // 1. 强制居中坐标系
        contentHolder.anchorMin = contentHolder.anchorMax = contentHolder.pivot = new Vector2(0.5f, 0.5f);
        // 自动适配高度到 Content 容器
        contentHolder.sizeDelta = new Vector2(contentHolder.sizeDelta.x, cardHeight);

        // 2. 边界：第一张卡居中时 x=0
        maxX = 0; 
        minX = -(currentDataList.Count - 1) * cardSpacing;

        for (int i = 0; i < currentDataList.Count; i++) {
            GameObject cardObj = Instantiate(cardPrefab, contentHolder);
            cardObj.name = i.ToString();
            RectTransform rt = cardObj.GetComponent<RectTransform>();
            
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            // 这里应用你设置的 cardWidth 和 cardHeight
            rt.sizeDelta = new Vector2(cardWidth, cardHeight); 
            rt.anchoredPosition = new Vector2(i * cardSpacing, 0);
            
            UpdateCardVisuals(cardObj, currentDataList[i]);
            if (!cardObj.GetComponent<CardDragHandler>()) cardObj.AddComponent<CardDragHandler>();
            spawnedCards.Add(cardObj);
        }
        contentHolder.anchoredPosition = Vector2.zero;
    }

    public void OnDragging(Vector2 delta) {
        DOTween.Kill(contentHolder);
        float newX = contentHolder.anchoredPosition.x + delta.x * swipeSensitivity;
        // 弹性区间
        newX = Mathf.Clamp(newX, minX - 300f, maxX + 300f);
        contentHolder.anchoredPosition = new Vector2(newX, 0);
    }

    public void OnDragEnd(float velocityX) {
        float inertia = velocityX * 0.12f;
        float predictedX = contentHolder.anchoredPosition.x + inertia;

        int targetIndex = Mathf.RoundToInt(-predictedX / cardSpacing);
        targetIndex = Mathf.Clamp(targetIndex, 0, currentDataList.Count - 1);
        
        float finalX = -targetIndex * cardSpacing;
        // OutBack 效果在手机上非常有弹性感
        contentHolder.DOAnchorPosX(finalX, 0.45f).SetEase(Ease.OutBack);
    }

    private void OnBackFromList() {
        HideUI();
        if (TurnManager.Instance != null) TurnManager.Instance.StartTurn();
    }

    public void OnCardClicked(int index) {
        if (currentDataList == null || index >= currentDataList.Count) return;
        CardBase selectedCard = currentDataList[index];
        cardUIPanel.SetActive(false);
        if (CardRangeFinder.Instance != null) CardRangeFinder.Instance.ShowRange(selectedCard);
        SetupCardButton("返回", () => {
            if (CardRangeFinder.Instance != null) CardRangeFinder.Instance.ClearHighlight();
            Show(currentDataList);
        });
    }

    private void SetupCardButton(string label, UnityEngine.Events.UnityAction action) {
        if (UIManager.Instance.cardButton != null) {
            UIManager.Instance.SetCardButtonLabel(label);
            UIManager.Instance.cardButton.onClick.RemoveAllListeners();
            UIManager.Instance.cardButton.onClick.AddListener(action);
        }
    }

    private void UpdateCardVisuals(GameObject cardObj, CardBase data) {
        TextMeshProUGUI[] texts = cardObj.GetComponentsInChildren<TextMeshProUGUI>();
        foreach (var t in texts) {
            if (t.gameObject.name == "Name") t.text = data.cardName;
            else if (t.gameObject.name == "Desc") t.text = data.description;
            t.raycastTarget = false; 
        }
    }
}