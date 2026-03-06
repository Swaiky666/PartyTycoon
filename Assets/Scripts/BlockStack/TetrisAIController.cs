using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// BlockStack AI 玩家控制器
///
/// 决策优先级：
///   1. 凑 5 连（触发钢铁地基）——永远最高优先，有缺口就补
///   2. 堆高时重心纠偏：往质心偏离方向的反侧叠放，保持塔形稳定
///   3. 保底：最高点附近小随机
/// </summary>
public class TetrisAIController : MonoBehaviour
{
    [Header("AI 节奏")]
    public float minThinkTime = 0.45f;
    public float maxThinkTime = 0.88f;

    [Header("AI 策略")]
    [Tooltip("犯错概率 0–1")]
    [Range(0f, 1f)] public float mistakeChance = 0.10f;
    [Tooltip("凑行阈值：已有多少块才专门去补缺（建议 2–3）")]
    public int rowTargetThreshold = 2;
    [Tooltip("重心偏离多少格才纠偏（仅在无行目标时生效）")]
    public float leanCorrectionThreshold = 0.8f;

    // ── 运行时 ────────────────────────────────────────────────────────────────

    private TetrisPlayerColumn _col;
    private bool               _running;
    private TetrisPiece        _lastPiece;
    private float              _targetX;

    // ── 启动 / 停止 ───────────────────────────────────────────────────────────

    public void StartAI(TetrisPlayerColumn column)
    {
        _col     = column;
        _running = true;
        StartCoroutine(AILoop());
    }

    public void StopAI()
    {
        _running = false;
        StopAllCoroutines();
        if (_col != null) _col.softDropHeld = false;
    }

    // ── 主循环 ────────────────────────────────────────────────────────────────

    IEnumerator AILoop()
    {
        yield return new WaitForSeconds(Random.Range(0.1f, maxThinkTime));

        while (_running)
        {
            if (_col != null && !_col.IsFinished())
                Think();

            yield return new WaitForSeconds(Random.Range(minThinkTime, maxThinkTime));
        }
    }

    void Think()
    {
        TetrisPiece piece = _col.currentPiece;
        if (piece == null) { _col.softDropHeld = false; return; }

        // 新方块：重新选目标并随机旋转
        if (piece != _lastPiece)
        {
            _lastPiece = piece;
            _targetX   = ChooseTargetX();
            PickRotation(piece);
        }

        // 犯错：本次乱走一步
        if (Random.value < mistakeChance)
        {
            _col.softDropHeld = false;
            if (Random.value < 0.5f) piece.MoveLeft();
            else                     piece.MoveRight();
            return;
        }

        // 向目标移动；对齐后软落
        float diff = _targetX - piece.transform.position.x;
        if (Mathf.Abs(diff) > 0.55f)
        {
            _col.softDropHeld = false;
            if (diff > 0) piece.MoveRight();
            else          piece.MoveLeft();
        }
        else
        {
            _col.softDropHeld = true;
        }
    }

    // ── 目标 X 决策 ───────────────────────────────────────────────────────────

    float ChooseTargetX()
    {
        List<Transform> settled = _col.GetSettledBlocks();
        float colX = _col.transform.position.x;

        if (settled == null || settled.Count == 0)
            return colX;

        // ── 构建占位图 + 最高行 ────────────────────────────────────────────────
        var rowOccupancy = new Dictionary<int, HashSet<int>>();
        float maxY = float.MinValue;

        foreach (var t in settled)
        {
            if (t == null) continue;
            maxY = Mathf.Max(maxY, t.position.y);
            int rowY = Mathf.RoundToInt(t.position.y);
            int relX = Mathf.Clamp(Mathf.RoundToInt(t.position.x - colX), -2, 2);
            if (!rowOccupancy.ContainsKey(rowY)) rowOccupancy[rowY] = new HashSet<int>();
            rowOccupancy[rowY].Add(relX);
        }

        int topRowY = Mathf.RoundToInt(maxY);

        // ── 优先级 1：凑 5 连 ─────────────────────────────────────────────────
        // 找已有方块最多的不完整行（不管有没有支撑，只要缺格子就去补）
        {
            int bestCount = 0;
            int bestRelX  = 0;
            bool found    = false;

            foreach (var kv in rowOccupancy)
            {
                int count = kv.Value.Count;
                if (count >= 5 || count < rowTargetThreshold) continue;
                if (count < bestCount) continue;

                // 从中心向两侧找缺口（优先补中间，稳定性更好）
                for (int x = 0; x <= 2; x++)
                {
                    if (!kv.Value.Contains(x))
                    {
                        if (count > bestCount) { bestCount = count; bestRelX = x; found = true; }
                        break;
                    }
                    if (x != 0 && !kv.Value.Contains(-x))
                    {
                        if (count > bestCount) { bestCount = count; bestRelX = -x; found = true; }
                        break;
                    }
                }
            }

            if (found)
                return Mathf.Clamp(colX + bestRelX, colX - 2f, colX + 2f);
        }

        // ── 优先级 2：堆高 + 重心纠偏 ────────────────────────────────────────
        // 计算上半段质心偏移
        float leanSum   = 0f;
        int   leanCount = 0;
        float midY      = maxY * 0.55f;

        foreach (var t in settled)
        {
            if (t == null || t.position.y < midY) continue;
            leanSum += t.position.x - colX;
            leanCount++;
        }
        float lean = leanCount > 0 ? leanSum / leanCount : 0f;

        // 顶部行已有支撑的位置中，选最靠近纠偏目标的
        float leanCorrection = -Mathf.Clamp(lean * 0.7f, -2f, 2f);

        if (rowOccupancy.ContainsKey(topRowY) && rowOccupancy[topRowY].Count > 0)
        {
            int bestSupportX = 0;
            float bestScore  = float.MaxValue;

            foreach (int rx in rowOccupancy[topRowY])
            {
                float score = Mathf.Abs(rx - leanCorrection);
                if (score < bestScore) { bestScore = score; bestSupportX = rx; }
            }

            return Mathf.Clamp(colX + bestSupportX + Random.Range(-0.4f, 0.4f), colX - 2f, colX + 2f);
        }

        // ── 优先级 3：保底 ────────────────────────────────────────────────────
        return Mathf.Clamp(colX + leanCorrection + Random.Range(-0.6f, 0.6f), colX - 2f, colX + 2f);
    }

    // ── 旋转选型 ──────────────────────────────────────────────────────────────

    void PickRotation(TetrisPiece piece)
    {
        if (piece == null) return;
        int times = Random.Range(0, 3);
        for (int i = 0; i < times; i++)
        {
            if (Random.value < 0.5f) piece.RotateCW();
            else                     piece.RotateCCW();
        }
    }
}
