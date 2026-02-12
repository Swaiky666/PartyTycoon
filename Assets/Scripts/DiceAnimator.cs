using UnityEngine;
using System.Collections;
using DG.Tweening; // 确保引用了 DOTween

public class DiceAnimator : MonoBehaviour {
    [Header("速度配置")]
    public float idleSpinSpeed = 150f;   
    public float rollSpinSpeed = 600f;   
    public float transitionTime = 1.5f; 

    [Header("打磨配置")]
    public float punchScaleAmount = 1.2f; // 停止时放大的倍率（基于原始大小的比例）
    public float punchDuration = 0.3f;    // 缩放动画的时长

    [Header("6个点的角度(1-6)")]
    public Vector3[] faceRotations = new Vector3[6];

    [Header("引用的子物体模型")]
    public GameObject diceModel; 

    private bool isIdle = false;
    private bool isRolling = false;
    private bool isLocking = false; 
    private float currentSpeed = 0f;
    private Quaternion targetQuaternion;
    
    // 用于记录你在 Inspector 中设置的原始大小
    private Vector3 originalScale;

    void Awake() {
        if (diceModel == null && transform.childCount > 0) {
            diceModel = transform.GetChild(0).gameObject;
        }
        
        // 核心修改：在最开始就记住你的缩放设置
        if (diceModel != null) {
            originalScale = diceModel.transform.localScale;
        }
        
        ShowDice(false);
    }

    void Update() {
        if (diceModel == null) return;

        if (isIdle || isRolling) {
            float targetSpeed = isRolling ? rollSpinSpeed : (isIdle ? idleSpinSpeed : 0f);
            currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed, Time.deltaTime * 3f);
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
        if (diceModel != null) {
            diceModel.SetActive(show);
            // 确保显示/隐藏时恢复到你在 Inspector 设置的大小，不被动画残留干扰
            diceModel.transform.localScale = originalScale;
        }
        isIdle = show;
        if (!show) {
            isRolling = false;
            isLocking = false;
            currentSpeed = 0;
        }
    }

    public IEnumerator PlayRollSequence(int result, System.Action onFinished, bool autoHide = true) {
        isRolling = true;
        isIdle = false;
        isLocking = false;
        
        yield return new WaitForSeconds(0.8f); 

        isRolling = false; 
        isLocking = true;
        targetQuaternion = Quaternion.Euler(faceRotations[result - 1]);
        
        float elapsed = 0f;
        while (elapsed < transitionTime) {
            elapsed += Time.deltaTime;
            float t = elapsed / transitionTime;
            float easeT = 1f - Mathf.Pow(1f - t, 3f);

            currentSpeed = Mathf.Lerp(currentSpeed, 0, easeT);
            diceModel.transform.Rotate(new Vector3(1f, 0.7f, 0.3f) * currentSpeed * Time.deltaTime);
            diceModel.transform.localRotation = Quaternion.Slerp(diceModel.transform.localRotation, targetQuaternion, easeT * 0.1f);
            yield return null;
        }

        // 锁定结果
        diceModel.transform.localRotation = targetQuaternion;
        isLocking = false;

        // --- 核心打磨：使用原始大小进行 Punch 动画 ---
        // Vector3.Scale(originalScale, Vector3.one * (punchScaleAmount - 1f)) 确保增加的比例是基于原大小的
        Vector3 punchAmount = new Vector3(
            originalScale.x * (punchScaleAmount - 1f),
            originalScale.y * (punchScaleAmount - 1f),
            originalScale.z * (punchScaleAmount - 1f)
        );
        diceModel.transform.DOPunchScale(punchAmount, punchDuration, 10, 1f);

        // 等待 1 秒让玩家看清结果，同时也等待缩放动画播完
        yield return new WaitForSeconds(1.0f); 
        
        if (autoHide) ShowDice(false);
        onFinished?.Invoke();
    }
}