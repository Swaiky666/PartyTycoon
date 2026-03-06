using UnityEngine;

/// <summary>
/// BlockStack 输入处理器（触屏 + 键盘）
/// 触屏：
///   点击左半屏  → 向左移动
///   点击右半屏  → 向右移动
///   左划         → 逆时针旋转
///   右划         → 顺时针旋转
///   双指按住     → 快速下落
/// 键盘（PC 测试）：
///   ← / →  → 移动
///   ↑       → 顺时针旋转
///   ↓ 持续  → 快速下落
/// 只控制本机玩家列（localColumn）。
/// </summary>
public class TetrisInputHandler : MonoBehaviour
{
    [HideInInspector] public TetrisPlayerColumn localColumn;

    // 横向滑动超过此像素才算"划屏旋转"
    const float SwipeThreshold = 50f;
    // 小于此像素才算"点击移动"
    const float TapMaxMove = 25f;

    private struct TouchInfo
    {
        public int     id;
        public Vector2 startPos;
        public bool    consumed; // 已判定为划屏，不再触发点击
    }

    private TouchInfo[] _track      = new TouchInfo[10];
    private int         _trackCount = 0;
    private bool        _wasMultiTouch = false; // 本次手势中是否出现过双指

    void Update()
    {
        if (localColumn == null) return;
        HandleTouch();
        HandleKeyboard();
    }

    // ── 触屏 ─────────────────────────────────────────────────────────────────

    void HandleTouch()
    {
        // 统计当前非结束状态的触点数
        int active = 0;
        foreach (Touch t in Input.touches)
            if (t.phase != TouchPhase.Ended && t.phase != TouchPhase.Canceled)
                active++;

        // 一旦出现双指，标记为双指模式；所有触点离开后重置
        if (active >= 2)   _wasMultiTouch = true;
        if (Input.touchCount == 0) _wasMultiTouch = false;

        // 双指持续按住 → 快速下落
        localColumn.softDropHeld = (active >= 2);

        foreach (Touch t in Input.touches)
        {
            if (t.phase == TouchPhase.Began)
            {
                AddTouch(t.fingerId, t.position);
            }
            else if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
            {
                // 双指模式期间不响应单指的点击/划屏
                if (!_wasMultiTouch)
                    ResolveTouch(t.fingerId, t.position);
                RemoveTouch(t.fingerId);
            }
        }
    }

    void AddTouch(int id, Vector2 pos)
    {
        if (_trackCount >= _track.Length) return;
        _track[_trackCount++] = new TouchInfo { id = id, startPos = pos, consumed = false };
    }

    void RemoveTouch(int id)
    {
        for (int i = 0; i < _trackCount; i++)
        {
            if (_track[i].id != id) continue;
            _track[i] = _track[--_trackCount];
            return;
        }
    }

    void ResolveTouch(int id, Vector2 endPos)
    {
        for (int i = 0; i < _trackCount; i++)
        {
            if (_track[i].id != id || _track[i].consumed) continue;

            Vector2 delta = endPos - _track[i].startPos;
            float ax = Mathf.Abs(delta.x);
            float ay = Mathf.Abs(delta.y);

            if (ax >= SwipeThreshold && ax > ay)
            {
                // 横向划屏 → 旋转
                if (delta.x > 0) localColumn.currentPiece?.RotateCW();
                else             localColumn.currentPiece?.RotateCCW();
                _track[i].consumed = true;
            }
            else if (ax < TapMaxMove && ay < TapMaxMove)
            {
                // 点击 → 按屏幕左右半区移动
                if (_track[i].startPos.x < Screen.width * 0.5f)
                    localColumn.currentPiece?.MoveLeft();
                else
                    localColumn.currentPiece?.MoveRight();
            }
            return;
        }
    }

    // ── 键盘（PC 测试）────────────────────────────────────────────────────────

    void HandleKeyboard()
    {
        // 持续按住 ↓ → 快速下落
        if (Input.GetKey(KeyCode.DownArrow))
            localColumn.softDropHeld = true;

        if (Input.GetKeyDown(KeyCode.LeftArrow))
            localColumn.currentPiece?.MoveLeft();
        if (Input.GetKeyDown(KeyCode.RightArrow))
            localColumn.currentPiece?.MoveRight();
        if (Input.GetKeyDown(KeyCode.UpArrow))
            localColumn.currentPiece?.RotateCW();
    }
}
