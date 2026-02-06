using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

public class CardUIController : MonoBehaviour {
    public static CardUIController Instance;

    [Header("UI 引用")]
    public GameObject cardUIPanel;      
    public RectTransform contentHolder; 
    public GameObject cardPrefab;       
    
    [Header("卡牌尺寸控制 (Inspector)")]
    public float cardWidth = 500f;     // 在这里设置宽度
    public float cardHeight = 750f;    // 在这里设置高度
    public float cardSpacing = 600f;   // 卡牌之间的间距
    public float lerpSpeed = 15f;       
    
    private List<GameObject> spawnedCards = new List<GameObject>();
    private List<CardBase> currentDataList;
    private int currentIndex = 0;
    private Vector2 targetPos;          
    private bool isDragging = false;    
    private CardBase selectedCard;

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

    public void OnCardClicked(int index) {
        if (currentDataList == null || index >= currentDataList.Count) return;
        selectedCard = currentDataList[index];

        if (CameraController.Instance != null) {
            CameraController.Instance.enabled = true; 
            CameraController.Instance.SetFreeMode(true); 
        }

        cardUIPanel.SetActive(false);
        if (CardRangeFinder.Instance != null) CardRangeFinder.Instance.ShowRange(selectedCard);

        SetupCardButton("返回", OnBackFromPreview);
        UIManager.Instance.UpdateStatus($"已选:{selectedCard.cardName}，滑动屏幕平移视角");
    }

    public void OnTargetNodeSelected(GridNode node) {
        UIManager.Instance.ShowActionButton("确认位置", () => {
            PlayerController p = TurnManager.Instance.GetCurrentPlayer();
            if (selectedCard != null && selectedCard.UseCard(p, node)) {
                FinishProcess();
            }
        });
    }

    private void OnBackFromPreview() {
        if (CardRangeFinder.Instance != null) CardRangeFinder.Instance.ClearHighlight();
        if (CameraController.Instance != null) {
            CameraController.Instance.SetFreeMode(false);
            CameraController.Instance.enabled = false; 
        }
        if (UIManager.Instance != null) UIManager.Instance.HideActionButton();
        Show(currentDataList); 
    }

    private void OnBackFromList() {
        HideUI();
        if (TurnManager.Instance != null) TurnManager.Instance.StartTurn();
    }

    private void FinishProcess() {
        if (CardRangeFinder.Instance != null) CardRangeFinder.Instance.ClearHighlight();
        if (CameraController.Instance != null) {
            CameraController.Instance.enabled = true;
            CameraController.Instance.SetFreeMode(false);
        }
        if (UIManager.Instance != null) UIManager.Instance.HideActionButton();
        TurnManager.Instance.CompleteCardAction();
        HideUI();
    }

    public void HideUI() { 
        cardUIPanel.SetActive(false); 
        if (UIManager.Instance.viewButton) UIManager.Instance.viewButton.gameObject.SetActive(true);
    }

    private void SetupCardButton(string label, UnityEngine.Events.UnityAction action) {
        if (UIManager.Instance.cardButton != null) {
            UIManager.Instance.SetCardButtonLabel(label);
            UIManager.Instance.cardButton.onClick.RemoveAllListeners();
            UIManager.Instance.cardButton.onClick.AddListener(action);
        }
    }

    private void RefreshCardList() {
        foreach (var c in spawnedCards) if(c != null) Destroy(c);
        spawnedCards.Clear();
        
        float totalWidth = currentDataList.Count * cardSpacing;
        contentHolder.sizeDelta = new Vector2(totalWidth, contentHolder.sizeDelta.y);

        for (int i = 0; i < currentDataList.Count; i++) {
            GameObject cardObj = Instantiate(cardPrefab, contentHolder);
            cardObj.name = i.ToString();
            
            RectTransform rt = cardObj.GetComponent<RectTransform>();
            // 确保锚点居中以便计算
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            
            // --- 核心修改：应用 Inspector 中的尺寸 ---
            rt.sizeDelta = new Vector2(cardWidth, cardHeight); 
            
            float offsetX = (i * cardSpacing) - (totalWidth * 0.5f) + (cardSpacing * 0.5f);
            rt.anchoredPosition = new Vector2(offsetX, 0); 
            
            UpdateCardVisuals(cardObj, currentDataList[i]);
            cardObj.AddComponent<CardDragHandler>();
            spawnedCards.Add(cardObj);
        }
        targetPos = new Vector2(-currentIndex * cardSpacing, 0);
    }

    private void UpdateCardVisuals(GameObject cardObj, CardBase data) {
        TextMeshProUGUI[] texts = cardObj.GetComponentsInChildren<TextMeshProUGUI>();
        foreach (var t in texts) {
            if (t.gameObject.name == "Name") t.text = data.cardName;
            else if (t.gameObject.name == "Desc") t.text = data.description;
            // 避免文字遮挡射线检测
            t.raycastTarget = false; 
        }
    }

    public void SetDragging(bool dragging) { isDragging = dragging; }
    public void HandleEndDrag() {
        isDragging = false;
        float currentX = contentHolder.anchoredPosition.x;
        currentIndex = Mathf.RoundToInt(-currentX / cardSpacing);
        currentIndex = Mathf.Clamp(currentIndex, 0, spawnedCards.Count - 1);
        targetPos = new Vector2(-currentIndex * cardSpacing, contentHolder.anchoredPosition.y);
    }

    void Update() {
        if (cardUIPanel.activeSelf && !isDragging) {
            contentHolder.anchoredPosition = Vector2.Lerp(contentHolder.anchoredPosition, targetPos, Time.deltaTime * lerpSpeed);
        }
    }
}