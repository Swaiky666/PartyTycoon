using UnityEngine;
using UnityEngine.EventSystems;

public class CardDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler {
    private RectTransform contentRT;
    private CardUIController controller;
    private bool wasDragged = false;

    void Start() {
        controller = CardUIController.Instance;
        if (controller != null) contentRT = controller.contentHolder;
    }

    public void OnBeginDrag(PointerEventData eventData) {
        wasDragged = true; // 标记正在拖拽，防止触发点击效果
        if (controller != null) controller.SetDragging(true);
        Debug.Log("[Drag] 开始拖拽: " + name);
    }

    public void OnDrag(PointerEventData eventData) {
        if (contentRT != null) {
            // 物理同步位移
            contentRT.anchoredPosition += new Vector2(eventData.delta.x, 0);
        }
    }

    public void OnEndDrag(PointerEventData eventData) {
        if (controller != null) controller.HandleEndDrag();
        // 延迟一小会儿重置拖拽标记，确保点击事件能正确识别
        Invoke("ResetDragFlag", 0.1f);
    }

    public void OnPointerClick(PointerEventData eventData) {
        // 只有没发生过拖拽的纯点击，才触发卡牌使用
        if (!wasDragged) {
            Debug.Log("[Click] 点击了卡牌: " + name);
            // 这里执行原本 Button 的逻辑
            // 假设我们通过名字或顺序找到这张牌（此处可扩展）
            // CardRangeFinder.Instance.ShowRange(...);
            // controller.HideUI();
        }
    }

    private void ResetDragFlag() { wasDragged = false; }
}