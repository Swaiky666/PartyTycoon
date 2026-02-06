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

    [Header("卡牌尺寸控制")]
    public float cardWidth = 500f;     // 在 Inspector 中控制宽度
    public float cardHeight = 750f;    // 在 Inspector 中控制高度
    public float cardSpacing = 600f;   // 卡牌中心点之间的间距

    [Header("滑动设置")]
    public float lerpSpeed = 15f;       
    
    private List<GameObject> spawnedCards = new List<GameObject>();
    private int currentIndex = 0;
    private Vector2 targetPos;          
    private bool isDragging = false;    

    void Awake() { 
        Instance = this; 
        if(cardUIPanel != null) cardUIPanel.SetActive(false);
    }

    public void Show(List<CardBase> playerCards) {
        if (playerCards == null || playerCards.Count == 0) return;

        cardUIPanel.SetActive(true);
        cardUIPanel.transform.SetAsLastSibling(); 

        foreach (var c in spawnedCards) if(c != null) Destroy(c);
        spawnedCards.Clear();

        // 自动计算 ContentHolder 的总宽度
        contentHolder.sizeDelta = new Vector2(playerCards.Count * cardSpacing, contentHolder.sizeDelta.y);

        // 获取你手动在 Inspector 设置的 Pivot
        Vector2 pivot = contentHolder.pivot;

        for (int i = 0; i < playerCards.Count; i++) {
            GameObject cardObj = Instantiate(cardPrefab, contentHolder);
            cardObj.name = "Card_" + i;

            RectTransform rt = cardObj.GetComponent<RectTransform>();

            // 1. 强制断开锚点关联（设为中心点），确保卡牌形状不随父物体拉伸
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            
            // 2. 应用你在 Inspector 中设置的长宽
            rt.sizeDelta = new Vector2(cardWidth, cardHeight); 
            rt.localScale = Vector3.one;
            
            // 3. 核心：根据 ContentHolder 的 Pivot 动态计算 X 轴坐标
            // 计算逻辑：(当前索引 * 间距) - (总宽度 * Pivot偏移量) + (首张半宽偏移确保居中)
            float totalWidth = playerCards.Count * cardSpacing;
            float offsetX = (i * cardSpacing) - (totalWidth * pivot.x) + (cardSpacing * 0.5f);
            rt.anchoredPosition = new Vector2(offsetX, 0); 

            // 4. 填充内容
            UpdateCardVisuals(cardObj, playerCards[i]);

            // 5. 挂载转发脚本
            if (cardObj.GetComponent<CardDragHandler>() == null) {
                cardObj.AddComponent<CardDragHandler>();
            }

            spawnedCards.Add(cardObj);
        }
        
        currentIndex = 0;
        // 初始目标位置也需要根据 Pivot 保持为当前 anchoredPosition (即 0)
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
    }

    public void SetDragging(bool dragging) {
        isDragging = dragging;
    }

    public void HandleEndDrag() {
        isDragging = false;
        float currentX = contentHolder.anchoredPosition.x;
        
        // 基于相对位移计算当前索引
        // 无论 Pivot 在哪，相对起始位置的偏移量 / 间距 就能得到索引
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