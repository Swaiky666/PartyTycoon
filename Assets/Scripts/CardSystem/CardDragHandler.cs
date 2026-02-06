using UnityEngine;
using UnityEngine.EventSystems;

public class CardDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler {
    private bool wasDragged = false;
    private float startTime;
    private Vector2 startPos;

    public void OnBeginDrag(PointerEventData eventData) {
        wasDragged = false;
        startTime = Time.time;
        startPos = eventData.position;
    }

    public void OnDrag(PointerEventData eventData) {
        // 记录是否有大幅度滑动，防止手指抖动误判为拖拽
        if (eventData.delta.magnitude > 2f) wasDragged = true;
        
        if (CardUIController.Instance != null)
            CardUIController.Instance.OnDragging(eventData.delta);
    }

    public void OnEndDrag(PointerEventData eventData) {
        float duration = Time.time - startTime;
        // 这里的 velocity 计算兼容了鼠标和触摸屏
        float velocityX = (eventData.position.x - startPos.x) / (duration > 0 ? duration : 0.01f);
        
        if (CardUIController.Instance != null)
            CardUIController.Instance.OnDragEnd(velocityX);
            
        // 延迟重置，确保在松手那一帧不会触发 OnPointerClick
        Invoke("ResetDragFlag", 0.12f);
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