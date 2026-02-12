using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening; // 必须引用

public class CameraController : MonoBehaviour {
    public static CameraController Instance;

    [Header("追踪设置")]
    public Transform target;
    public float followSmoothTime = 0.2f;

    [Header("感度设置")]
    public float rotationSpeed = 0.15f;
    public float zoomSpeed = 0.5f;
    public float panSensitivity = 0.02f; 

    [Header("自由模式限制")]
    public float panRange = 30f; 

    private float currentDistance = 12f;
    private float targetDistance = 12f;
    private float yaw = 0f;
    private float pitch = 40f; 

    private Vector3 currentVelocity;
    private Vector3 smoothPivotPoint;
    private bool isOrbiting = false;
    private bool isFreeMode = false;
    private Vector3 freePivotOffset; 
    private Vector2 lastInputPos;

    // --- 新增震动相关变量 ---
    private Vector3 shakeOffset = Vector3.zero;

    void Awake() { Instance = this; }

    public void SetTarget(Transform newTarget) { target = newTarget; }

    void LateUpdate() {
        if (target == null) return;
        
        HandleZoom();
        
        bool isInputStarted = Input.GetMouseButtonDown(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began);
        bool isInputEnded = Input.GetMouseButtonUp(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Ended);

        if (isInputStarted && !IsPointerOverUI()) {
            isOrbiting = true;
            lastInputPos = GetCurrentInputPos();
        }

        if (isInputEnded) isOrbiting = false;

        if (isOrbiting) {
            if (isFreeMode) HandleFreePan();
            else HandleRotation();
        }

        UpdatePosition();
    }

    // --- 供外部调用的震动接口 ---
    public void ApplyShake(float duration = 0.4f, float strength = 0.5f) {
        // 先停止之前的震动，防止叠加导致的坐标偏移无法归零
        DOTween.Kill("CameraShake");
        
        // 核心逻辑：震动的是变量偏移，而不是 Transform 坐标本身
        DOTween.Shake(() => shakeOffset, x => shakeOffset = x, duration, strength, 20, 90)
            .SetId("CameraShake")
            .OnComplete(() => shakeOffset = Vector3.zero);
    }

    public void SetFreeMode(bool free) {
        isFreeMode = free;
        if (free) {
            pitch = 90f; 
            yaw = 0f;
            freePivotOffset = Vector3.zero;
            if (target != null) smoothPivotPoint = target.position;
        } else {
            pitch = 40f;
            freePivotOffset = Vector3.zero;
        }
    }

    void HandleFreePan() {
        Vector2 currentPos = GetCurrentInputPos();
        Vector2 delta = currentPos - lastInputPos;
        lastInputPos = currentPos;

        Vector3 forward = transform.up; forward.y = 0;
        Vector3 right = transform.right; right.y = 0;

        Vector3 moveDir = (right.normalized * -delta.x + forward.normalized * -delta.y);
        freePivotOffset += moveDir * panSensitivity * (currentDistance / 10f);
        freePivotOffset = Vector3.ClampMagnitude(freePivotOffset, panRange);
    }

    void HandleRotation() {
        Vector2 currentPos = GetCurrentInputPos();
        Vector2 delta = currentPos - lastInputPos;
        lastInputPos = currentPos;
        yaw += delta.x * rotationSpeed * 10f;
        pitch -= delta.y * rotationSpeed * 10f;
        pitch = Mathf.Clamp(pitch, 10f, 85f);
    }

    void UpdatePosition() {
        Vector3 targetPoint = target.position + freePivotOffset;
        smoothPivotPoint = Vector3.SmoothDamp(smoothPivotPoint, targetPoint, ref currentVelocity, followSmoothTime);
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0);
        
        // 核心修改：在最终坐标上叠加上震动偏移量
        Vector3 basePosition = smoothPivotPoint + rotation * new Vector3(0, 0, -currentDistance);
        transform.position = basePosition + shakeOffset; 
        
        transform.LookAt(smoothPivotPoint + (isFreeMode ? Vector3.zero : Vector3.up * 1.5f));
    }

    void HandleZoom() {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        targetDistance = Mathf.Clamp(targetDistance - scroll * 10f, 5f, 30f);
        currentDistance = Mathf.Lerp(currentDistance, targetDistance, Time.deltaTime * 5f);
    }

    private Vector2 GetCurrentInputPos() {
        if (Input.touchCount > 0) return Input.GetTouch(0).position;
        return Input.mousePosition;
    }

    private bool IsPointerOverUI() {
        if (EventSystem.current == null) return false;
        if (Input.touchCount > 0) return EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId);
        return EventSystem.current.IsPointerOverGameObject();
    }
}