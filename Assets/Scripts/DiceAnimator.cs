using UnityEngine;
using System.Collections;
using DG.Tweening; 

public class DiceAnimator : MonoBehaviour {
    [Header("速度配置")]
    public float idleSpinSpeed = 150f;   
    public float rollSpinSpeed = 600f;   
    [Tooltip("快速旋转持续的总时长")]
    public float fastRollDuration = 1.0f; // 你需要的 Inspector 控制变量
    public float transitionTime = 1.5f; 

    [Header("音效配置")]
    public AudioClip rollingClip; // 骰子滚动时的循环音效

    [Header("打磨配置")]
    public float punchScaleAmount = 1.2f; 
    public float punchDuration = 0.3f;    

    [Header("6个点的角度(1-6)")]
    public Vector3[] faceRotations = new Vector3[6];

    [Header("引用的子物体模型")]
    public GameObject diceModel; 

    private bool isIdle = false;
    private bool isRolling = false;
    private bool isLocking = false; 
    private float currentSpeed = 0f;
    private Quaternion targetQuaternion;
    private Vector3 originalScale;

    void Awake() {
        if (diceModel == null && transform.childCount > 0) {
            diceModel = transform.GetChild(0).gameObject;
        }
        if (diceModel != null) {
            originalScale = diceModel.transform.localScale;
        }
        ShowDice(false);
    }

    void Update() {
        if (diceModel == null) return;

        if (isIdle || isRolling) {
            float speed = isRolling ? rollSpinSpeed : idleSpinSpeed;
            currentSpeed = Mathf.Lerp(currentSpeed, speed, Time.deltaTime * 5f);
            diceModel.transform.Rotate(new Vector3(1f, 0.7f, 0.3f) * currentSpeed * Time.deltaTime);
        }
    }

    public void ShowDice(bool show) {
        gameObject.SetActive(show);
        // 关闭显示时务必停止音效
        if (!show && AudioManager.Instance != null) AudioManager.Instance.StopSFX();
    }

    public void ShowAndIdle() {
        isIdle = true;
        isRolling = false;
        isLocking = false;
        currentSpeed = idleSpinSpeed;
        // Idle 状态不播放音效，所以确保停止
        if (AudioManager.Instance != null) AudioManager.Instance.StopSFX();
    }

    public IEnumerator PlayRollSequence(int result, System.Action onComplete) {
        isRolling = true;
        isIdle = false;
        isLocking = false;

        // --- 开始播放骰子音效 ---
        if (AudioManager.Instance != null && rollingClip != null) {
            AudioManager.Instance.PlayLoopingSFX(rollingClip);
        }
        
        // 快速旋转阶段
        yield return new WaitForSeconds(fastRollDuration); 

        // --- 准备锁定结果，停止音效 ---
        if (AudioManager.Instance != null) {
            AudioManager.Instance.StopSFX();
        }

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

        diceModel.transform.localRotation = targetQuaternion;
        isLocking = false;

        Vector3 punchAmount = new Vector3(
            originalScale.x * (punchScaleAmount - 1f),
            originalScale.y * (punchScaleAmount - 1f),
            originalScale.z * (punchScaleAmount - 1f)
        );
        diceModel.transform.DOPunchScale(punchAmount, punchDuration);

        yield return new WaitForSeconds(0.2f);
        onComplete?.Invoke();
    }
}