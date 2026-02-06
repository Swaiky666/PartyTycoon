using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;
using DG.Tweening; // 确保已安装 DOTween

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

    // 外部调用：打开 UI
    public void Show(List<CardBase> playerCards) {
        if (playerCards == null || playerCards.Count == 0) return;
        currentDataList = playerCards;
        cardUIPanel.SetActive(true);
        
        // 隐藏主界面视角切换按钮
        if (UIManager.Instance.viewButton != null) 
            UIManager.Instance.viewButton.gameObject.SetActive(false);

        SetupCardButton("返回", OnBackFromList);
        RefreshCardList();
    }

    // 核心：刷新与生成列表
    private void RefreshCardList() {
        DOTween.Kill(contentHolder);
        foreach (var c in spawnedCards) if(c != null) Destroy(c);
        spawnedCards.Clear();

        // 1. 强制初始化容器 Anchor 和 Pivot 到正中心 (0.5, 0.5)
        contentHolder.anchorMin = contentHolder.anchorMax = contentHolder.pivot = new Vector2(0.5f, 0.5f);
        contentHolder.sizeDelta = new Vector2(contentHolder.sizeDelta.x, cardHeight);

        // 2. 边界计算：第一张在 0，最后一张在 -(count-1)*spacing
        maxX = 0; 
        minX = -(currentDataList.Count - 1) * cardSpacing;

        // 3. 生成卡牌
        for (int i = 0; i < currentDataList.Count; i++) {
            GameObject cardObj = Instantiate(cardPrefab, contentHolder);
            cardObj.name = i.ToString(); // 用于点击判定
            
            RectTransform rt = cardObj.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(cardWidth, cardHeight); 
            rt.anchoredPosition = new Vector2(i * cardSpacing, 0);
            
            UpdateCardVisuals(cardObj, currentDataList[i]);
            
            // 确保挂载了拖拽处理器
            if (!cardObj.GetComponent<CardDragHandler>()) 
                cardObj.AddComponent<CardDragHandler>();
                
            spawnedCards.Add(cardObj);
        }
        
        // 4. 重置位置：第一张卡牌居中
        contentHolder.anchoredPosition = Vector2.zero;
    }

    // 处理拖拽位移
    public void OnDragging(Vector2 delta) {
        DOTween.Kill(contentHolder);
        float newX = contentHolder.anchoredPosition.x + delta.x * swipeSensitivity;
        // 弹性区间：允许拖过头 300 像素
        newX = Mathf.Clamp(newX, minX - 300f, maxX + 300f);
        contentHolder.anchoredPosition = new Vector2(newX, 0);
    }

    // 处理拖拽结束：惯性吸附
    public void OnDragEnd(float velocityX) {
        float inertia = velocityX * 0.12f;
        float predictedX = contentHolder.anchoredPosition.x + inertia;

        // 计算最接近的卡牌索引
        int targetIndex = Mathf.RoundToInt(-predictedX / cardSpacing);
        targetIndex = Mathf.Clamp(targetIndex, 0, currentDataList.Count - 1);
        
        float finalX = -targetIndex * cardSpacing;

        // 丝滑吸附回弹
        contentHolder.DOAnchorPosX(finalX, 0.45f).SetEase(Ease.OutBack);
    }

    // 业务：更新卡牌视觉（文字 + 图片）
    private void UpdateCardVisuals(GameObject cardObj, CardBase data) {
        // 更新文字
        TextMeshProUGUI[] texts = cardObj.GetComponentsInChildren<TextMeshProUGUI>();
        foreach (var t in texts) {
            if (t.gameObject.name == "Name") t.text = data.cardName;
            else if (t.gameObject.name == "Desc") t.text = data.description;
            t.raycastTarget = false; 
        }

        // 更新图片：查找名为 "Icon" 的子物体
        Transform iconTransform = cardObj.transform.Find("Icon");
        if (iconTransform != null) {
            Image img = iconTransform.GetComponent<Image>();
            if (img != null && data.cardIcon != null) {
                img.sprite = data.cardIcon;
                img.raycastTarget = false;
            }
        }
    }

    // 业务：点击卡牌
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

    // 提供给 TurnManager 的关闭接口
    public void HideUI() {
        DOTween.Kill(contentHolder);
        cardUIPanel.SetActive(false);
        if (UIManager.Instance.viewButton) UIManager.Instance.viewButton.gameObject.SetActive(true);
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