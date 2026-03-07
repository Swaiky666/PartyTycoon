using UnityEngine;
using DG.Tweening;

/// <summary>
/// 附在已固化方块的 Rigidbody 上。
/// 功能：
///   1. 分轴限制加速度（水平截断冲量，向下不限，向上截断）
///   2. 吸附系统：
///      - 位置接近整数格 + 旋转接近 0/90/180/270° + 速度慢 → 打 isSnapped 标记并吸附到精确格点
///      - isSnapped 状态持续施加轻微吸引力保持对齐
///      - 偏离超过 UnsnapThreshold → 取消 isSnapped
/// </summary>
public class SettledBlockMonitor : MonoBehaviour
{
    // ── 加速度限制 ────────────────────────────────────────────────────────────
    const float MaxAcceleration = 15f;

    // ── 吸附参数 ─────────────────────────────────────────────────────────────
    const float SnapPosEnter   = 0.07f;  // 位置偏离整数格 < 此值时可进入吸附
    const float SnapRotEnter   = 3f;     // 旋转偏离最近 90° < 此度时可进入吸附
    const float SnapSpeedEnter = 0.12f;  // 速度 < 此值时才允许进入吸附（必须接近静止）
    const float UnsnapPos      = 0.38f;  // 位置偏离 > 此值时取消吸附
    const float SnapForce      = 28f;    // 吸附加速度（m/s²，保持强力）
    const float SnapTorque     = 120f;   // 旋转吸附加速度（deg/s²，保持强力）

    Rigidbody      _rb;
    Vector3        _prevVelocity;
    Transform[]    _visualTransforms;
    System.Action  _onSnapped;

    public bool IsSnapped { get; private set; }

    public void Trigger(Rigidbody rb, System.Action onSnapped = null)
    {
        _rb           = rb;
        _prevVelocity = rb != null ? rb.velocity : Vector3.zero;
        _onSnapped    = onSnapped;
        IsSnapped     = false;
        enabled       = true;

        // 收集直接子物体（block unit），而非所有 Renderer 后代。
        // GetComponentsInChildren 会同时拿到 Outline 包内部的二级 Renderer，
        // 导致父子同时被独立 DOPunchScale → 倍乘缩放 → 视觉分裂。
        // 只动画直接子层，Outline 子网格会随父自然跟随，不产生分裂效果。
        if (rb != null)
        {
            var children = new System.Collections.Generic.List<Transform>();
            foreach (Transform child in rb.transform)
                children.Add(child);
            _visualTransforms = children.ToArray();
        }
        else
        {
            _visualTransforms = new Transform[0];
        }
    }

    void FixedUpdate()
    {
        if (_rb == null) { Destroy(this); return; }

        LimitAcceleration();
        UpdateSnap();
    }

    // ── 加速度限制 ────────────────────────────────────────────────────────────

    void LimitAcceleration()
    {
        if (_rb.isKinematic) { _prevVelocity = Vector3.zero; return; }
        Vector3 delta = _rb.velocity - _prevVelocity;
        float   dt    = Time.fixedDeltaTime;

        float dx   = delta.x;
        float maxH = MaxAcceleration * dt;
        if (dx >  maxH) dx =  maxH;
        if (dx < -maxH) dx = -maxH;

        float dy    = delta.y;
        float maxUp = MaxAcceleration * dt;
        if (dy > maxUp) dy = maxUp;

        _rb.velocity  = _prevVelocity + new Vector3(dx, dy, 0f);
        _prevVelocity = _rb.velocity;
    }

    // ── 吸附系统 ─────────────────────────────────────────────────────────────

    void UpdateSnap()
    {
        if (_rb.isKinematic) { IsSnapped = true; return; }
        float posOffX = _rb.position.x - Mathf.Round(_rb.position.x);
        float posOffY = _rb.position.y - Mathf.Round(_rb.position.y);

        float rotZ   = _rb.rotation.eulerAngles.z;           // 0~360
        float rotMod = rotZ % 90f;
        float rotOff = Mathf.Min(rotMod, 90f - rotMod);      // 与最近 90° 整数的偏差（度）

        if (IsSnapped)
        {
            // 偏离过大 → 取消吸附
            if (Mathf.Abs(posOffX) > UnsnapPos || Mathf.Abs(posOffY) > UnsnapPos)
            {
                IsSnapped = false;
                return;
            }

            // 施加向整数格的吸引加速度
            float dt = Time.fixedDeltaTime;
            _rb.velocity += new Vector3(-posOffX * SnapForce, -posOffY * SnapForce, 0f) * dt;

            // 施加向最近 90° 的旋转加速度
            float targetZ = Mathf.Round(rotZ / 90f) * 90f;
            float rotDiff = Mathf.DeltaAngle(rotZ, targetZ);
            _rb.angularVelocity += new Vector3(0f, 0f, rotDiff * SnapTorque * Mathf.Deg2Rad) * dt;
        }
        else
        {
            // 满足条件 → 进入吸附：立即对齐到精确格点
            bool posOk = Mathf.Abs(posOffX) < SnapPosEnter && Mathf.Abs(posOffY) < SnapPosEnter;
            bool rotOk = rotOff < SnapRotEnter;
            bool slow  = _rb.velocity.magnitude < SnapSpeedEnter;

            if (posOk && rotOk && slow)
            {
                IsSnapped = true;

                _rb.position = new Vector3(
                    Mathf.Round(_rb.position.x),
                    Mathf.Round(_rb.position.y),
                    0f);

                float targetZ = Mathf.Round(rotZ / 90f) * 90f;
                _rb.rotation        = Quaternion.Euler(0f, 0f, targetZ);
                _rb.velocity        = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;

                PlaySnapAnim();
                _onSnapped?.Invoke();
            }
        }
    }

    // 吸附时视觉缩放脉冲（仅视觉，不影响碰撞体）
    void PlaySnapAnim()
    {
        if (_visualTransforms == null) return;
        foreach (var t in _visualTransforms)
        {
            if (t == null) continue;
            t.DOKill();
            t.localScale = Vector3.one;
            t.DOPunchScale(Vector3.one * 0.18f, 0.28f, 6, 0.4f);
        }
    }
}
