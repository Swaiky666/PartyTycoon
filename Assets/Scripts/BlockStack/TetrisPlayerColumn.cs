using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TMPro;

public class TetrisPlayerColumn : MonoBehaviour
{
    [Header("场景引用（Inspector 拖入）")]
    public Transform spawnPoint;             // 新方块生成位置（列顶中心）
    public Transform finishLineTransform;    // 高度线
    public GameObject blockUnitPrefab;       // 1×1×1 单元预制体（无 Rigidbody）
    public TextMeshPro playerLabel;          // 显示玩家编号/状态

    [Header("运行时（代码初始化，不需要手动填）")]
    public int playerId;
    public Color playerColor = Color.white;
    [HideInInspector] public float spawnWaitSeconds = 0.4f;

    public TetrisPiece currentPiece { get; private set; }

    // 按钮长按软降标志（由 UI 按钮设置，TetrisPiece.Update 每帧读取）
    [HideInInspector] public bool softDropHeld = false;

    private List<Transform> settledBlocks = new List<Transform>();
    private bool isFinished = false;
    private bool isActive   = false;

    // ── 初始化 ───────────────────────────────────────────────────────────────

    public void Init(int pid, Color color, GameObject blockPrefab)
    {
        playerId        = pid;
        playerColor     = color;
        blockUnitPrefab = blockPrefab;
        if (playerLabel != null) playerLabel.text = $"P{pid}";
    }

    // ── 游戏开始 ─────────────────────────────────────────────────────────────

    public void BeginSpawning()
    {
        isActive = true;
        SpawnNextPiece();
    }

    // ── Spawn ─────────────────────────────────────────────────────────────────

    public void SpawnNextPiece()
    {
        if (isFinished || !isActive || blockUnitPrefab == null) return;

        TetrisPiece.TetrominoType type = (TetrisPiece.TetrominoType)Random.Range(0, 7);
        GameObject pieceObj = new GameObject($"Piece_P{playerId}_{type}");
        pieceObj.transform.position = spawnPoint != null
            ? spawnPoint.position
            : transform.position + Vector3.up * 18f;
        pieceObj.transform.rotation = Quaternion.identity;

        TetrisPiece piece = pieceObj.AddComponent<TetrisPiece>();
        piece.Init(type, this, playerId, blockUnitPrefab, playerColor);
        currentPiece = piece;
    }

    // ── 回调：由 TetrisPiece.Settle() 调用 ──────────────────────────────────

    public void RegisterSettledBlock(Transform t)
    {
        if (t != null) settledBlocks.Add(t);
    }

    public void OnPieceSettled(TetrisPiece piece)
    {
        currentPiece = null;
        if (isFinished || !isActive) return;
        StartCoroutine(CheckAndSpawnNext());
    }

    IEnumerator CheckAndSpawnNext()
    {
        yield return new WaitForSeconds(spawnWaitSeconds);
        if (CheckFinishCondition())
        {
            isFinished = true;
            if (playerLabel != null) playerLabel.text = $"P{playerId}\n完成!";
            TetrisGameController.Instance.OnPlayerFinished(playerId);
        }
        else if (isActive)
        {
            SpawnNextPiece();
        }
    }

    // ── 高度检测 ─────────────────────────────────────────────────────────────

    bool CheckFinishCondition()
    {
        if (finishLineTransform == null) return false;
        float finishY = finishLineTransform.position.y;
        foreach (var t in settledBlocks)
        {
            if (t != null && t.position.y >= finishY) return true;
        }
        return false;
    }

    // ── 游戏结束 ─────────────────────────────────────────────────────────────

    public void Stop()
    {
        isActive = false;
        if (currentPiece != null) { Destroy(currentPiece.gameObject); currentPiece = null; }
    }

    // ── 当前最高高度 ──────────────────────────────────────────────────────────

    public float GetCurrentMaxHeight()
    {
        float maxY = 0f;
        foreach (var t in settledBlocks)
        {
            if (t != null) maxY = Mathf.Max(maxY, t.position.y);
        }
        return maxY;
    }

    // ── 游戏结束：解除约束，侧推触发倒塌动画 ────────────────────────────────

    public void ReleaseBlocks()
    {
        var released = new HashSet<Rigidbody>();
        foreach (var t in settledBlocks)
        {
            if (t == null) continue;
            Rigidbody rb = t.GetComponentInParent<Rigidbody>();
            if (rb == null || !released.Add(rb)) continue;

            rb.isKinematic = false;
            rb.constraints = RigidbodyConstraints.FreezePositionZ;
            rb.AddForce(new Vector3(Random.Range(-0.5f, 0.5f), 0f, 0f), ForceMode.Impulse);
        }
    }

    public List<Transform> GetSettledBlocks() => settledBlocks;
    public bool IsFinished() => isFinished;
}
