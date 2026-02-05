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

    private float currentDistance = 12f;
    private float targetDistance = 12f;
    private float yaw = 0f;
    private float pitch = 40f; // 默认跟随俯角

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

    // 关键修正：模式切换
    public void SetFreeMode(bool free) {
        isFreeMode = free;
        if (free) {
            // 进入俯视角：俯角90度直视下方，偏航角归零
            pitch = 90f;
            yaw = 0f;
            lastMousePos = Input.mousePosition;
        } else {
            // 切回跟随视角：恢复默认的俯视角和偏移量
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

        // 仅在非自由模式下允许旋转视角
        if (isOrbiting && !isFreeMode) {
            float mouseX = (Input.touchCount > 0) ? Input.GetTouch(0).deltaPosition.x : Input.GetAxis("Mouse X") * 10f;
            float mouseY = (Input.touchCount > 0) ? Input.GetTouch(0).deltaPosition.y : Input.GetAxis("Mouse Y") * 10f;
            yaw += mouseX * rotationSpeed;
            pitch -= mouseY * rotationSpeed;
            pitch = Mathf.Clamp(pitch, 10f, 85f); // 俯视角不宜超过90度，防止死锁
        }
    }

    void HandleFreePan() {
        Vector3 currentMousePos = Input.mousePosition;
        Vector3 delta = currentMousePos - lastMousePos;
        lastMousePos = currentMousePos;

        // 俯视图下的移动逻辑：鼠标向上划 -> 相机向前移
        // 此时相机正对着地面，所以平移非常直观
        Vector3 panDir = new Vector3(-delta.x, 0, -delta.y); // 根据实际感官调整正负号
        freePivotOffset += panDir * 0.015f * (currentDistance / 10f);
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