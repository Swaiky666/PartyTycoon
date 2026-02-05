using UnityEngine;
using UnityEngine.EventSystems;

public class CameraController : MonoBehaviour {
    public static CameraController Instance;

    [Header("追踪设置")]
    public Transform target;
    public float followSmoothTime = 0.2f;

    [Header("控制感度")]
    public float rotationSpeed = 0.15f;
    public float zoomSpeed = 0.5f;

    private float currentDistance = 12f;
    private float targetDistance = 12f;
    private float yaw = 0f;
    private float pitch = 40f;
    private Vector3 currentVelocity;
    private Vector3 smoothPivotPoint;
    private bool isOrbiting = false;

    void Awake() { Instance = this; }

    void LateUpdate() {
        if (target == null) return;
        
        HandleZoom();
        HandleRotation();
        UpdatePosition();
    }

    void HandleZoom() {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        targetDistance = Mathf.Clamp(targetDistance - scroll * 10f, 5f, 30f);
        currentDistance = Mathf.Lerp(currentDistance, targetDistance, Time.deltaTime * 5f);
    }

    void HandleRotation() {
        // 关键点：只要不点在 UI 上，点击屏幕任何地方（包括空白和箭头）都能触发旋转判定
        if (Input.GetMouseButtonDown(0) || (Input.touchCount == 1 && Input.GetTouch(0).phase == TouchPhase.Began)) {
            if (!IsPointerOverUI()) {
                isOrbiting = true;
            }
        }

        if (Input.GetMouseButtonUp(0) || (Input.touchCount == 1 && Input.GetTouch(0).phase == TouchPhase.Ended)) {
            isOrbiting = false;
        }

        if (isOrbiting) {
            float mouseX = (Input.touchCount > 0) ? Input.GetTouch(0).deltaPosition.x : Input.GetAxis("Mouse X") * 10f;
            float mouseY = (Input.touchCount > 0) ? Input.GetTouch(0).deltaPosition.y : Input.GetAxis("Mouse Y") * 10f;
            
            yaw += mouseX * rotationSpeed;
            pitch -= mouseY * rotationSpeed;
            pitch = Mathf.Clamp(pitch, 10f, 80f);
        }
    }

    void UpdatePosition() {
        smoothPivotPoint = Vector3.SmoothDamp(smoothPivotPoint, target.position, ref currentVelocity, followSmoothTime);
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0);
        transform.position = smoothPivotPoint + rotation * new Vector3(0, 0, -currentDistance);
        transform.LookAt(smoothPivotPoint + Vector3.up * 1.5f);
    }

    public void SetTarget(Transform newTarget) { target = newTarget; }

    private bool IsPointerOverUI() {
        if (EventSystem.current == null) return false;
        // 检测鼠标或手指是否在 UI 物体上
        return EventSystem.current.IsPointerOverGameObject() || 
               (Input.touchCount > 0 && EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId));
    }
}