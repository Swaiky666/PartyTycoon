using UnityEngine;
using System.Collections;

public class DiceAnimator : MonoBehaviour {
    [Header("速度配置")]
    public float idleSpinSpeed = 150f;   
    public float rollSpinSpeed = 600f;   
    public float transitionTime = 1.5f; // 增加时长，让偏移过程更自然

    [Header("6个点的角度(1-6)")]
    public Vector3[] faceRotations = new Vector3[6];

    [Header("引用的子物体模型")]
    public GameObject diceModel; 

    private bool isIdle = false;
    private bool isRolling = false;
    private bool isLocking = false; // 新增：正在锁定状态
    private float currentSpeed = 0f;
    private Quaternion targetQuaternion;

    void Awake() {
        if (diceModel == null && transform.childCount > 0) {
            diceModel = transform.GetChild(0).gameObject;
        }
        ShowDice(false);
    }

    void Update() {
        if (diceModel == null) return;

        if (isIdle || isRolling) {
            float targetSpeed = isRolling ? rollSpinSpeed : (isIdle ? idleSpinSpeed : 0f);
            currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed, Time.deltaTime * 3f);
            
            // 持续自由旋转
            diceModel.transform.Rotate(new Vector3(1f, 0.7f, 0.3f) * currentSpeed * Time.deltaTime);
        }
    }

    public void ShowAndIdle() {
        ShowDice(true);
        isIdle = true;
        isRolling = false;
        isLocking = false;
    }

    public void ShowDice(bool show) {
        if (diceModel != null) diceModel.SetActive(show);
        isIdle = show;
        if (!show) {
            isRolling = false;
            isLocking = false;
            currentSpeed = 0;
        }
    }

    public IEnumerator PlayRollSequence(int result, System.Action onFinished, bool autoHide = true) {
        // 1. 进入快转阶段
        isRolling = true;
        isIdle = false;
        isLocking = false;
        
        yield return new WaitForSeconds(0.8f); // 保持快转的时间

        // 2. 慢慢偏移锁定阶段
        isRolling = false; 
        isLocking = true;
        
        targetQuaternion = Quaternion.Euler(faceRotations[result - 1]);
        
        float elapsed = 0f;
        while (elapsed < transitionTime) {
            elapsed += Time.deltaTime;
            float t = elapsed / transitionTime;
            
            // 平滑曲线：前快后慢 (Cubic Out)
            float easeT = 1f - Mathf.Pow(1f - t, 3f);

            // 同时做两件事：
            // A. 让当前持续旋转的速度降为 0
            currentSpeed = Mathf.Lerp(currentSpeed, 0, easeT);
            diceModel.transform.Rotate(new Vector3(1f, 0.7f, 0.3f) * currentSpeed * Time.deltaTime);

            // B. 在旋转的同时，将旋转方向平滑拉向目标方向
            // 使用 Slerp 进行姿态引导
            diceModel.transform.localRotation = Quaternion.Slerp(diceModel.transform.localRotation, targetQuaternion, easeT * 0.1f);
            
            yield return null;
        }

        // 3. 最后强行校准，确保绝对精准
        diceModel.transform.localRotation = targetQuaternion;
        isLocking = false;

        yield return new WaitForSeconds(1.0f); 
        
        if (autoHide) ShowDice(false);
        onFinished?.Invoke();
    }
}