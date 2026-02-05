using UnityEngine;
using UnityEngine.EventSystems;

public class ArrowClicker : MonoBehaviour {
    public GridNode targetNode;
    private System.Action<GridNode> onChosen;
    private Vector3 pressScreenPos;
    private bool isPotentialClick = false;
    private float dragThreshold = 20f; // 拖动超过20像素则视为旋转视角

    public void Setup(GridNode node, System.Action<GridNode> callback) {
        targetNode = node;
        onChosen = callback;
    }

    void Update() {
        // 1. 检测按下
        if (Input.GetMouseButtonDown(0) || (Input.touchCount == 1 && Input.GetTouch(0).phase == TouchPhase.Began)) {
            if (IsPointingAtThis()) {
                pressScreenPos = Input.mousePosition;
                isPotentialClick = true;
            }
        }

        // 2. 检测抬起
        if (Input.GetMouseButtonUp(0) || (Input.touchCount == 1 && Input.GetTouch(0).phase == TouchPhase.Ended)) {
            if (isPotentialClick) {
                isPotentialClick = false;
                
                // 计算手指滑动的物理距离
                float moveDist = Vector3.Distance(pressScreenPos, Input.mousePosition);
                
                // 只有在松开时依然指着箭头，且滑动距离很小，才认为是“点击确认”
                if (moveDist < dragThreshold && IsPointingAtThis()) {
                    onChosen?.Invoke(targetNode);
                }
            }
        }
    }

    private bool IsPointingAtThis() {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return false;
        
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit)) {
            return hit.collider.gameObject == this.gameObject;
        }
        return false;
    }
}