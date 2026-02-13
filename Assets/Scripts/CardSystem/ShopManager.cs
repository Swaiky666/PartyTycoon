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
        currentGoods.Clear();
        List<CardBase> pool = GameDataManager.Instance.allPossibleCards;
        if (pool == null || pool.Count == 0) return;

        for (int i = 0; i < 5; i++) {
            currentGoods.Add(pool[Random.Range(0, pool.Count)]);
        }
        RefreshShopUI();
        ShowListState();
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
        go.transform.SetParent(shopPanel.transform); 
        go.transform.SetAsLastSibling();
        go.transform.DOScale(shopCardPrefab.transform.localScale * 1.5f, 0.3f);
        go.transform.DOMove(shopPanel.transform.position, 0.3f);

        UIManager.Instance.UpdateStatus($"{selectedCard.cardName} | 价格: {selectedCard.price}");
        UIManager.Instance.ShowActionButton("购买卡牌", BuyCurrent);
    }

    void BuyCurrent() {
        if (selectedIndex < 0 || selectedIndex >= currentGoods.Count) return;
        PlayerController p = TurnManager.Instance.GetCurrentPlayer();

        // --- 核心新增：检查卡牌上限 ---
        if (p.IsHandFull()) {
            UIManager.Instance.UpdateStatus("<color=red>购买失败：卡牌包已满（上限 5 张）！</color>");
            return; 
        }

        if (p.money >= selectedCard.price) {
            p.ChangeMoney(-selectedCard.price);
            p.cards.Add(Instantiate(selectedCard));
            currentGoods.RemoveAt(selectedIndex);
            RefreshShopUI();
            ShowListState();
        } else {
            UIManager.Instance.UpdateStatus("<color=red>金币不足！</color>");
        }
    }

    public void CloseShop() {
        shopPanel.SetActive(false);
        if (GridEventManager.Instance != null) GridEventManager.Instance.NotifyShopClosed();
    }
}