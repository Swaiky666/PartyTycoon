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
        wasDragged = true;
        if (controller != null) controller.SetDragging(true);
    }

    public void OnDrag(PointerEventData eventData) {
        if (contentRT != null) contentRT.anchoredPosition += new Vector2(eventData.delta.x, 0);
    }

    public void OnEndDrag(PointerEventData eventData) {
        if (controller != null) controller.HandleEndDrag();
        Invoke("ResetDragFlag", 0.15f);
    }

    public void OnPointerClick(PointerEventData eventData) {
        if (!wasDragged) {
            if (int.TryParse(gameObject.name, out int index)) {
                CardUIController.Instance.OnCardClicked(index);
            }
        }
    }
    private void ResetDragFlag() { wasDragged = false; }
}