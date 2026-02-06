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
        // 增加判定阈值，防止手指抖动误触发拖拽
        if (eventData.delta.magnitude > 2f) wasDragged = true;
        CardUIController.Instance.OnDragging(eventData.delta);
    }

    public void OnEndDrag(PointerEventData eventData) {
        // 手动计算速度: (当前像素位置 - 开始位置) / 经过的时间
        float duration = Time.time - startTime;
        Vector2 force = (eventData.position - startPos) / (duration > 0 ? duration : 0.01f);
        
        // 限制一下最大速度，防止滑得太离谱
        float velocityX = Mathf.Clamp(force.x, -2000f, 2000f);
        
        CardUIController.Instance.OnDragEnd(velocityX);
        Invoke("ResetDragFlag", 0.1f);
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