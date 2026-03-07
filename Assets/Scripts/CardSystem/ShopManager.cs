using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;
using DG.Tweening;
using UnityEngine.EventSystems;

public class ShopManager : MonoBehaviour {
    public static ShopManager Instance;

    [Header("UI 面板与遮罩")]
    public GameObject shopPanel;
    public Button backgroundOverlay;

    [Header("卡牌固定槽位")]
    public RectTransform[] slotTransforms;

    [Header("资源预制体")]
    public GameObject shopCardPrefab;

    [Header("横排布局")]
    [Tooltip("卡牌之间的像素间距")]
    public float cardSpacing = 350f;
    [Tooltip("横排整体的垂直偏移（相对面板中心，0 = 正中间）")]
    public float rowOffsetY = 0f;

    private List<CardBase> currentGoods = new List<CardBase>();
    private List<GameObject> spawnedCards = new List<GameObject>();
    private CardBase selectedCard;
    private GameObject selectedCardObj; 
    private int selectedIndex = -1;      
    private bool isInspecting = false;   

    void Awake() { 
        Instance = this; 
        if(shopPanel) shopPanel.SetActive(false);
    }

    void Update() {
        if (isInspecting && Input.GetMouseButtonDown(0)) {
            GameObject hitObject = GetOverlappingUIObject();
            if (hitObject == null || (hitObject != selectedCardObj && hitObject.GetComponentInParent<Button>() == null)) {
                ShowListState();
            }
        }
    }

    private GameObject GetOverlappingUIObject() {
        PointerEventData eventData = new PointerEventData(EventSystem.current);
        eventData.position = Input.mousePosition;
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);
        return results.Count > 0 ? results[0].gameObject : null;
    }

    public void OpenShop() {
        shopPanel.SetActive(true);
        LayoutSlotsHorizontal();

        currentGoods.Clear();
        List<CardBase> pool = GameDataManager.Instance.allPossibleCards;
        if (pool == null || pool.Count == 0) return;

        for (int i = 0; i < 5; i++) {
            currentGoods.Add(pool[Random.Range(0, pool.Count)]);
        }
        RefreshShopUI();
        ShowListState();
    }

    // 将所有 slot 重新排列成一排，水平+垂直居中于 shopPanel
    void LayoutSlotsHorizontal() {
        if (slotTransforms == null || slotTransforms.Length == 0) return;

        float cardWidth = slotTransforms[0].rect.width;
        if (cardWidth <= 0f) cardWidth = slotTransforms[0].sizeDelta.x;

        int count = slotTransforms.Length;
        float totalWidth = cardWidth * count + cardSpacing * (count - 1);
        float startX = -totalWidth / 2f + cardWidth / 2f;

        for (int i = 0; i < count; i++) {
            // 将 anchor/pivot 全部设为中心，确保 anchoredPosition (0,0) = 面板正中心
            slotTransforms[i].anchorMin  = new Vector2(0.5f, 0.5f);
            slotTransforms[i].anchorMax  = new Vector2(0.5f, 0.5f);
            slotTransforms[i].pivot      = new Vector2(0.5f, 0.5f);
            slotTransforms[i].anchoredPosition = new Vector2(startX + i * (cardWidth + cardSpacing), rowOffsetY);
        }
    }

    void RefreshShopUI() {
        foreach (var go in spawnedCards) if(go) Destroy(go);
        spawnedCards.Clear();

        for (int i = 0; i < currentGoods.Count; i++) {
            int index = i; 
            GameObject go = Instantiate(shopCardPrefab, slotTransforms[i]);
            go.name = "Card_" + i;
            go.transform.localScale = shopCardPrefab.transform.localScale;
            go.transform.localPosition = Vector3.zero; 

            Transform bgTrans = go.transform.Find("Background");
            if (bgTrans != null) {
                Button btn = bgTrans.GetComponent<Button>();
                if (btn != null) {
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(() => OnClickCard(index, go));
                }
                SetCardInfo(go, bgTrans, currentGoods[i]);
            }
            spawnedCards.Add(go);
        }
    }

    void SetCardInfo(GameObject root, Transform bgTrans, CardBase data) {
        var nameTxt = root.transform.Find("Name")?.GetComponent<TextMeshProUGUI>();
        var descTxt = root.transform.Find("Desc")?.GetComponent<TextMeshProUGUI>();
        var iconImg = root.transform.Find("Icon")?.GetComponent<Image>();
        var bgImg = bgTrans.GetComponent<Image>();

        if (nameTxt) nameTxt.text = data.cardName;
        if (descTxt) descTxt.text = data.description;
        if (iconImg && data.cardIcon != null) iconImg.sprite = data.cardIcon;
        if (bgImg != null) bgImg.raycastTarget = true;
    }

    public void ShowListState() {
        isInspecting = false;
        selectedCard = null;
        selectedCardObj = null;
        selectedIndex = -1;
        backgroundOverlay.gameObject.SetActive(false);

        for (int i = 0; i < spawnedCards.Count; i++) {
            if(spawnedCards[i] == null) continue;
            spawnedCards[i].SetActive(true);
            spawnedCards[i].transform.SetParent(slotTransforms[i]); 
            spawnedCards[i].transform.localScale = shopCardPrefab.transform.localScale;
            spawnedCards[i].transform.localPosition = Vector3.zero;
        }
        UIManager.Instance.UpdateStatus("请选择卡牌");
        UIManager.Instance.ShowActionButton("退出商店", CloseShop);
    }

    void OnClickCard(int index, GameObject go) {
        if (index < 0 || index >= currentGoods.Count) return;
        selectedCard = currentGoods[index];
        selectedIndex = index;
        selectedCardObj = go;
        isInspecting = true;

        for (int i = 0; i < spawnedCards.Count; i++) {
            if (spawnedCards[i] != go && spawnedCards[i] != null) spawnedCards[i].SetActive(false);
        }

        backgroundOverlay.gameObject.SetActive(true);

        // 先做点击反馈 punch，再平滑移到面板正中心
        go.transform.DOKill();
        go.transform.DOPunchScale(Vector3.one * 0.06f, 0.10f, 4, 0.3f)
            .OnComplete(() => {
                go.transform.SetParent(shopPanel.transform);
                go.transform.SetAsLastSibling();

                // 移到面板 RectTransform 正中心（anchoredPosition = 0,0）
                RectTransform rt = go.GetComponent<RectTransform>();
                if (rt != null) {
                    rt.DOAnchorPos(Vector2.zero, 0.28f).SetEase(Ease.OutCubic);
                } else {
                    go.transform.DOMove(shopPanel.transform.position, 0.28f).SetEase(Ease.OutCubic);
                }
                go.transform.DOScale(shopCardPrefab.transform.localScale * 1.5f, 0.28f).SetEase(Ease.OutCubic);
            });

        UIManager.Instance.UpdateStatus($"{selectedCard.cardName} | 价格: {selectedCard.price}");
        UIManager.Instance.ShowActionButton("购买卡牌", BuyCurrent);
    }

    void BuyCurrent() {
        if (selectedIndex < 0 || selectedIndex >= currentGoods.Count) return;
        PlayerController p = TurnManager.Instance.GetCurrentPlayer();

        // UI 层前置拦截（快速反馈，服务端会再次权威校验）
        if (p.IsHandFull()) {
            UIManager.Instance.UpdateStatus("<color=red>购买失败：卡牌包已满！</color>");
            return;
        }
        if (p.money < selectedCard.price) {
            UIManager.Instance.UpdateStatus("<color=red>金币不足！</color>");
            return;
        }

        // 捕获当前选择，防止异步回调时 selectedCard/selectedIndex 已变更
        CardBase cardToBuy = selectedCard;
        int indexToRemove = selectedIndex;
        GameObject cardObj = selectedCardObj;

        // 确认动画：先放大再缩回，完成后发网络请求
        if (cardObj != null) {
            cardObj.transform.DOKill();
            Vector3 baseScale = shopCardPrefab.transform.localScale * 1.5f;
            cardObj.transform.DOScale(baseScale * 1.09f, 0.07f).SetEase(Ease.OutQuad)
                .OnComplete(() =>
                    cardObj.transform.DOScale(baseScale * 0.93f, 0.07f).SetEase(Ease.InQuad)
                        .OnComplete(() => DoSendBuy(p, cardToBuy, indexToRemove)));
        } else {
            DoSendBuy(p, cardToBuy, indexToRemove);
        }
    }

    void DoSendBuy(PlayerController p, CardBase cardToBuy, int indexToRemove) {
        // [网络] 服务端权威购买：验证 → 扣款 → 加入手牌
        GameNetworkManager.Instance.SendBuyCardRequest(
            p.playerId, cardToBuy.cardName, cardToBuy.price,
            onComplete: (success) => {
                if (success) {
                    currentGoods.RemoveAt(indexToRemove);
                    RefreshShopUI();
                    ShowListState();
                } else {
                    UIManager.Instance.UpdateStatus("<color=red>购买失败，请重试</color>");
                }
            }
        );
    }

    public void CloseShop() {
        shopPanel.SetActive(false);
        if (GridEventManager.Instance != null) GridEventManager.Instance.NotifyShopClosed();
    }
}