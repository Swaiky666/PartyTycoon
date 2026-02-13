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
    public float cardSpacing = 650f;   
    public float swipeSensitivity = 1.1f; 

    private List<GameObject> spawnedCards = new List<GameObject>();
    private List<CardBase> currentDataList;
    private float minX; 
    // maxX 已删除，解决了 CS0414 警告

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
        
        // maxX 直接设为0，minX 计算范围
        minX = -(currentDataList.Count - 1) * cardSpacing;

        for (int i = 0; i < currentDataList.Count; i++) {
            GameObject cardObj = Instantiate(cardPrefab, contentHolder);
            cardObj.name = i.ToString(); 
            
            RectTransform rt = cardObj.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            
            // 保持 Inspector 原始大小，仅设置初始 X 位置
            rt.anchoredPosition = new Vector2(i * cardSpacing, 0);
            
            UpdateCardVisuals(cardObj, currentDataList[i]);
            
            if (!cardObj.GetComponent<CardDragHandler>()) 
                cardObj.AddComponent<CardDragHandler>();
                
            spawnedCards.Add(cardObj);
        }
        contentHolder.anchoredPosition = Vector2.zero;
    }

    // --- 补回被丢失的滑动处理函数 ---

    public void OnDragging(Vector2 delta) {
        DOTween.Kill(contentHolder);
        float newX = contentHolder.anchoredPosition.x + delta.x * swipeSensitivity;
        // 增加一点边缘回弹感
        newX = Mathf.Clamp(newX, minX - 300f, 300f); 
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

    // --------------------------------

    private void UpdateCardVisuals(GameObject cardObj, CardBase data) {
        TextMeshProUGUI[] texts = cardObj.GetComponentsInChildren<TextMeshProUGUI>();
        foreach (var t in texts) {
            if (t.gameObject.name == "Name") t.text = data.cardName;
            else if (t.gameObject.name == "Desc") t.text = data.description;
            t.raycastTarget = false; 
        }

        Transform bgTransform = cardObj.transform.Find("Background");
        if (bgTransform != null) {
            Image bgImg = bgTransform.GetComponent<Image>();
            if (bgImg != null) bgImg.raycastTarget = true; 
        }

        Transform iconTransform = cardObj.transform.Find("Icon");
        if (iconTransform != null) {
            Image img = iconTransform.GetComponent<Image>();
            if (img != null && data.cardIcon != null) {
                img.sprite = data.cardIcon;
                // 注意：这里没有改动 icon 的 raycastTarget
            }
        }
    }

    public void OnCardClicked(int index) {
        if (currentDataList == null || index >= currentDataList.Count) return;
        CardBase selectedCard = currentDataList[index];
        
        cardUIPanel.SetActive(false);

        if (TurnManager.Instance != null) {
            TurnManager.Instance.EnterCardTargetingMode();
        }

        if (CardRangeFinder.Instance != null) CardRangeFinder.Instance.ShowRange(selectedCard);
        
        SetupCardButton("取消", () => {
            if (CardRangeFinder.Instance != null) CardRangeFinder.Instance.ClearHighlight();
            if (TurnManager.Instance != null) TurnManager.Instance.StartTurn();
        });

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