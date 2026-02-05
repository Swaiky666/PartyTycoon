using UnityEngine;
using UnityEngine.EventSystems;

public class CameraController : MonoBehaviour {
    public static CameraController Instance;

    [Header("追踪设置")]
    public Transform target;
    public float followSmoothTime = 0.2f;

    [Header("旋转/缩放感度")]
    public float rotationSpeed = 0.15f;
    public float zoomSpeed = 0.5f;

    [Header("自由模式限制")]
    public float panRange = 15f; // 在 Inspector 中更改此数值限制移动范围

    private float currentDistance = 12f;
    private float targetDistance = 12f;
    private float yaw = 0f;
    private float pitch = 40f; 

    private Vector3 currentVelocity;
    private Vector3 smoothPivotPoint;
    private bool isOrbiting = false;

    // --- 自由平移变量 ---
    private bool isFreeMode = false;
    private Vector3 freePivotOffset; 
    private Vector3 lastMousePos;

    void Awake() { Instance = this; }

    void LateUpdate() {
        if (target == null) return;
        
        HandleZoom();
        HandleRotation();
        
        if (isFreeMode && isOrbiting) {
            HandleFreePan();
        }

        UpdatePosition();
    }

    public void SetFreeMode(bool free) {
        isFreeMode = free;
        if (free) {
            pitch = 90f;
            yaw = 0f;
            lastMousePos = Input.mousePosition;
        } else {
            pitch = 40f;
            freePivotOffset = Vector3.zero;
        }
    }

    void HandleZoom() {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        targetDistance = Mathf.Clamp(targetDistance - scroll * 10f, 5f, 30f);
        currentDistance = Mathf.Lerp(currentDistance, targetDistance, Time.deltaTime * 5f);
    }

    void HandleRotation() {
        if (Input.GetMouseButtonDown(0) || (Input.touchCount == 1 && Input.GetTouch(0).phase == TouchPhase.Began)) {
            if (!IsPointerOverUI()) {
                isOrbiting = true;
                lastMousePos = Input.mousePosition;
            }
        }
        if (Input.GetMouseButtonUp(0) || (Input.touchCount == 1 && Input.GetTouch(0).phase == TouchPhase.Ended)) isOrbiting = false;

        if (isOrbiting && !isFreeMode) {
            float mouseX = (Input.touchCount > 0) ? Input.GetTouch(0).deltaPosition.x : Input.GetAxis("Mouse X") * 10f;
            float mouseY = (Input.touchCount > 0) ? Input.GetTouch(0).deltaPosition.y : Input.GetAxis("Mouse Y") * 10f;
            yaw += mouseX * rotationSpeed;
            pitch -= mouseY * rotationSpeed;
            pitch = Mathf.Clamp(pitch, 10f, 85f);
        }
    }

    void HandleFreePan() {
        Vector3 currentMousePos = Input.mousePosition;
        Vector3 delta = currentMousePos - lastMousePos;
        lastMousePos = currentMousePos;

        Vector3 panDir = new Vector3(-delta.x, 0, -delta.y);
        
        // 计算新的偏移量
        Vector3 nextOffset = freePivotOffset + panDir * 0.015f * (currentDistance / 10f);
        
        // 关键逻辑：限制偏移向量的模长，使其不超出 panRange
        freePivotOffset = Vector3.ClampMagnitude(nextOffset, panRange);
    }

    void UpdatePosition() {
        Vector3 targetPoint = target.position + freePivotOffset;
        smoothPivotPoint = Vector3.SmoothDamp(smoothPivotPoint, targetPoint, ref currentVelocity, followSmoothTime);
        
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0);
        transform.position = smoothPivotPoint + rotation * new Vector3(0, 0, -currentDistance);
        transform.LookAt(smoothPivotPoint + (isFreeMode ? Vector3.zero : Vector3.up * 1.5f));
    }

    public void SetTarget(Transform newTarget) { target = newTarget; }

    private bool IsPointerOverUI() {
        if (EventSystem.current == null) return false;
        return EventSystem.current.IsPointerOverGameObject() || (Input.touchCount > 0 && EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId));
    }
}