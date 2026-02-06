using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using TMPro;

public class CardUIController : MonoBehaviour {
    public static CardUIController Instance;

    [Header("UI 引用")]
    public GameObject cardUIPanel;      
    public RectTransform contentHolder; 
    public GameObject cardPrefab;       
    
    [Header("按钮文本引用")]
    public TextMeshProUGUI backButtonText; // 拖入卡牌界面中的“返回”按钮文本

    [Header("卡牌尺寸控制")]
    public float cardWidth = 500f;     
    public float cardHeight = 750f;    
    public float cardSpacing = 600f;   

    [Header("滑动平滑度")]
    public float lerpSpeed = 15f;       
    
    private List<GameObject> spawnedCards = new List<GameObject>();
    private int currentIndex = 0;
    private Vector2 targetPos;          
    private bool isDragging = false;    

    void Awake() { 
        Instance = this; 
        if(cardUIPanel != null) cardUIPanel.SetActive(false);
        
        // 统一卡牌界面的返回按钮名称
        if(backButtonText != null) backButtonText.text = "返回";
    }

    public void Show(List<CardBase> playerCards) {
        if (playerCards == null || playerCards.Count == 0) return;

        cardUIPanel.SetActive(true);
        
        // --- 修正：仅隐藏视角切换按钮，不干扰卡牌按钮 ---
        if (UIManager.Instance != null && UIManager.Instance.viewButton != null) {
            UIManager.Instance.viewButton.gameObject.SetActive(false);
        }

        foreach (var c in spawnedCards) if(c != null) Destroy(c);
        spawnedCards.Clear();

        Vector2 pivot = contentHolder.pivot;
        float totalWidth = playerCards.Count * cardSpacing;
        contentHolder.sizeDelta = new Vector2(totalWidth, contentHolder.sizeDelta.y);

        for (int i = 0; i < playerCards.Count; i++) {
            GameObject cardObj = Instantiate(cardPrefab, contentHolder);
            RectTransform rt = cardObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(cardWidth, cardHeight); 
            rt.localScale = Vector3.one;
            
            float offsetX = (i * cardSpacing) - (totalWidth * pivot.x) + (cardSpacing * 0.5f);
            rt.anchoredPosition = new Vector2(offsetX, 0); 

            UpdateCardVisuals(cardObj, playerCards[i]);

            if (cardObj.GetComponent<CardDragHandler>() == null) {
                cardObj.AddComponent<CardDragHandler>();
            }
            spawnedCards.Add(cardObj);
        }
        
        currentIndex = 0;
        targetPos = contentHolder.anchoredPosition; 
    }

    private void UpdateCardVisuals(GameObject cardObj, CardBase data) {
        TextMeshProUGUI[] texts = cardObj.GetComponentsInChildren<TextMeshProUGUI>();
        foreach (var t in texts) {
            if (t.gameObject.name == "Name") t.text = data.cardName;
            else if (t.gameObject.name == "Desc") t.text = data.description;
            t.raycastTarget = false; 
        }
    }

    public void HideUI() { 
        cardUIPanel.SetActive(false); 
        
        // --- 修正：恢复视角切换按钮 ---
        if (UIManager.Instance != null && UIManager.Instance.viewButton != null) {
            UIManager.Instance.viewButton.gameObject.SetActive(true);
        }
        
        if (CameraController.Instance != null) CameraController.Instance.enabled = true;
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