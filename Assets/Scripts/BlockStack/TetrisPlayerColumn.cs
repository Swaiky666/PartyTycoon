using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using DG.Tweening;

public class TetrisPlayerColumn : MonoBehaviour
{
    [Header("场景引用（Inspector 拖入）")]
    public Transform spawnPoint;             // 新方块生成位置（列顶中心）
    public Transform finishLineTransform;    // 高度线
    public GameObject blockUnitPrefab;       // 1×1×1 单元预制体（无 Rigidbody）
    public TextMeshPro playerLabel;          // 显示玩家编号/状态

    [Header("分屏摄像机（可选，不填则不震屏）")]
    public Camera playerCamera;

    [Header("运行时（代码初始化，不需要手动填）")]
    public int playerId;
    public Color playerColor = Color.white;
    [HideInInspector] public float spawnWaitSeconds = 0.4f;
    [HideInInspector] public int   rowWidth         = 5;     // 消行所需方块数，由 TetrisGameController 设置
    [HideInInspector] public float columnHalfWidth  = 2.5f;  // 列半宽（方块移动边界），由 TetrisGameController 设置

    public TetrisPiece currentPiece { get; private set; }

    // 按钮长按软降标志（由 UI 按钮设置，TetrisPiece.Update 每帧读取）
    [HideInInspector] public bool softDropHeld = false;

    private List<Transform>  settledBlocks = new List<Transform>();
    private List<GameObject> steelRoots   = new List<GameObject>(); // 钢铁地基根节点（用于胜利动画）
    private bool isFinished   = false;
    private bool isActive     = false;
    private bool isWindActive = false; // 风期间禁止生成新方块

    // 正常游戏只做极端兜底清理（不吸附的方块交由风来清除）
    const float SafetyKillY = -20f;

    // ── 帧更新：极端越界兜底清理 ─────────────────────────────────────────────

    void Update()
    {
        if (!isActive) return;
        CleanupOutOfBoundsBlocks();
    }

    void CleanupOutOfBoundsBlocks()
    {
        // 仅清理已落到极深位置的根节点（-20），正常的非吸附方块由风负责清除
        var deadRoots = new HashSet<Rigidbody>();
        foreach (var t in settledBlocks)
        {
            if (t == null) continue;
            Rigidbody rb = t.GetComponentInParent<Rigidbody>();
            if (rb != null && rb.position.y < SafetyKillY) deadRoots.Add(rb);
        }
        foreach (var rb in deadRoots)
        {
            settledBlocks.RemoveAll(t => t != null && t.IsChildOf(rb.transform));
            Destroy(rb.gameObject);
        }
    }

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
        if (isFinished || !isActive || blockUnitPrefab == null || isWindActive) return;

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

    // 活动方块掉出地图：立即补生新方块（风期间被摧毁的不走此路径补生）
    public void OnPieceKilled()
    {
        currentPiece = null;
        if (isWindActive || isFinished || !isActive) return;
        StartCoroutine(RespawnAfterKill());
    }

    IEnumerator RespawnAfterKill()
    {
        yield return new WaitForSeconds(spawnWaitSeconds);
        SpawnNextPiece();
    }

    // 大风：拆散所有非钢铁地基方块 → 逐渐透明 → 2.5s 后再等 0.5s 全部删除
    public void ApplyWind(float horizontalForce)
    {
        StartCoroutine(WindBlastRoutine(horizontalForce));
    }

    IEnumerator WindBlastRoutine(float horizontalForce)
    {
        const float windDuration  = 2.5f;
        const float cleanupDelay  = 0.5f;

        // ── 0. 风开始：禁止生成，摧毁当前下坠方块 ────────────────────────────
        isWindActive = true;
        if (currentPiece != null)
        {
            Destroy(currentPiece.gameObject);
            currentPiece = null;
        }

        // ── 1. 收集受影响的 settled 根节点（有 Rigidbody = 非钢铁）────────────
        var roots = new HashSet<Rigidbody>();
        foreach (var t in settledBlocks)
        {
            if (t == null) continue;
            Rigidbody rb = t.GetComponentInParent<Rigidbody>();
            if (rb != null) roots.Add(rb);
        }

        // ── 2. 将每个根拆散为独立单元 ────────────────────────────────────────
        var windBlocks = new List<(Rigidbody rb, Renderer rend, Outline ol)>();

        foreach (var rootRb in roots)
        {
            if (rootRb == null) continue;

            // 先记录属于该根的 settledBlocks 条目（重新归属前记录，防止 IsChildOf 失效）
            var toRemove = new List<Transform>();
            foreach (var t in settledBlocks)
                if (t != null && t.IsChildOf(rootRb.transform)) toRemove.Add(t);

            // 收集直接子块（Transforms），避免迭代时修改集合
            var blockChildren = new List<Transform>();
            foreach (Transform child in rootRb.transform) blockChildren.Add(child);

            // 立即禁用原根的碰撞体和吸附监测器
            foreach (var col in rootRb.GetComponents<BoxCollider>()) { col.enabled = false; Destroy(col); }
            var monitor = rootRb.GetComponent<SettledBlockMonitor>();
            if (monitor != null) monitor.enabled = false;

            foreach (var blockChild in blockChildren)
            {
                // 创建独立物理单元
                GameObject windGo = new GameObject("WindBlock");
                windGo.transform.SetPositionAndRotation(blockChild.position, blockChild.rotation);

                BoxCollider bc = windGo.AddComponent<BoxCollider>();
                bc.size = Vector3.one;

                Rigidbody windRb = windGo.AddComponent<Rigidbody>();
                windRb.mass       = 0.2f;
                windRb.drag       = 0.3f;
                windRb.angularDrag = 0.5f;
                windRb.collisionDetectionMode = CollisionDetectionMode.Continuous;
                windRb.constraints = RigidbodyConstraints.FreezePositionZ;

                // 将视觉子块迁移到独立单元
                blockChild.SetParent(windGo.transform);
                blockChild.localPosition = Vector3.zero;
                blockChild.localRotation = Quaternion.identity;

                // 初始化透明渲染模式
                Renderer rend = blockChild.GetComponentInChildren<Renderer>();
                if (rend != null) EnableMaterialFade(rend.material);

                // 收集 Outline（挂在 Renderer 同 GO 上）
                Outline ol = rend != null ? rend.GetComponent<Outline>() : null;

                // 初始冲量
                windRb.AddForce(new Vector3(horizontalForce, Random.Range(3f, 8f), 0f), ForceMode.Impulse);

                windBlocks.Add((windRb, rend, ol));
            }

            // 从 settledBlocks 移除，销毁原根（子块已迁走，不受影响）
            foreach (var t in toRemove) settledBlocks.Remove(t);
            Destroy(rootRb.gameObject);
        }

        // ── 3. 持续施力 + 逐渐透明 ───────────────────────────────────────────
        float elapsed = 0f;
        while (elapsed < windDuration)
        {
            yield return new WaitForFixedUpdate();
            elapsed += Time.fixedDeltaTime;
            float alpha = Mathf.Lerp(1f, 0.05f, elapsed / windDuration);

            foreach (var (windRb, rend, ol) in windBlocks)
            {
                if (windRb == null) continue;
                windRb.AddForce(new Vector3(horizontalForce * 2f, 0f, 0f), ForceMode.Force);
                if (rend != null)
                {
                    Color c = rend.material.color;
                    c.a = alpha;
                    rend.material.color = c;
                }
                if (ol != null)
                {
                    Color oc = ol.OutlineColor;
                    oc.a = alpha;
                    ol.OutlineColor = oc;
                }
            }
        }

        // ── 4. 等待 0.5s 宽限期，期间新落定的方块不受影响 ─────────────────────
        yield return new WaitForSeconds(cleanupDelay);

        foreach (var (windRb, _, _) in windBlocks)
        {
            if (windRb != null) Destroy(windRb.gameObject);
        }

        // ── 5. 风结束：恢复生成，若当前无方块则立即补生 ──────────────────────
        isWindActive = false;
        if (!isFinished && isActive && currentPiece == null)
            SpawnNextPiece();
    }

    // 开启材质透明渲染（兼容 Standard RP 和 URP）
    static void EnableMaterialFade(Material mat)
    {
        // Standard RP
        if (mat.HasProperty("_Mode"))
        {
            mat.SetFloat("_Mode", 2);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
        // URP Lit / Unlit
        if (mat.HasProperty("_Surface"))
        {
            mat.SetFloat("_Surface", 1);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
    }

    IEnumerator CheckAndSpawnNext()
    {
        yield return new WaitForSeconds(spawnWaitSeconds);
        CheckRowCompletion();
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

    // ── 消行 → 固化为钢铁地基 ────────────────────────────────────────────────

    void CheckRowCompletion()
    {
        settledBlocks.RemoveAll(t => t == null);

        // 按世界 Y 整数行分组
        var rowBlocks = new Dictionary<int, List<Transform>>();
        foreach (var t in settledBlocks)
        {
            int rowY = Mathf.RoundToInt(t.position.y);
            if (!rowBlocks.ContainsKey(rowY)) rowBlocks[rowY] = new List<Transform>();
            rowBlocks[rowY].Add(t);
        }

        foreach (var kv in rowBlocks)
        {
            if (kv.Value.Count < rowWidth) continue;
            // 必须有连续 rowWidth 个方块（X 间距 <= 1.5），有间隔不算
            List<Transform> segment = FindConsecutiveSegment(kv.Value, rowWidth);
            if (segment == null) continue;
            if (!AllSnapped(segment)) continue;
            FormSteelBase(kv.Key, segment);
        }
    }

    // 返回行内最长连续段（X 相邻间距 <= 1.5），长度 >= requiredCount 才返回，否则 null
    static List<Transform> FindConsecutiveSegment(List<Transform> rowBlocks, int requiredCount)
    {
        var sorted = new List<Transform>(rowBlocks);
        sorted.Sort((a, b) => a.position.x.CompareTo(b.position.x));

        int segStart = 0;
        for (int i = 1; i <= sorted.Count; i++)
        {
            bool isEnd = (i == sorted.Count) || (sorted[i].position.x - sorted[i - 1].position.x > 1.5f);
            if (!isEnd) continue;

            int segLen = i - segStart;
            if (segLen >= requiredCount)
                return sorted.GetRange(segStart, segLen);

            segStart = i;
        }
        return null;
    }

    bool AllSnapped(List<Transform> blocks)
    {
        foreach (var t in blocks)
        {
            var monitor = t.GetComponentInParent<SettledBlockMonitor>();
            if (monitor == null || !monitor.IsSnapped) return false;
        }
        return true;
    }

    void FormSteelBase(int rowY, List<Transform> blocks)
    {
        // 收集受影响的 settled 根节点
        var affectedRoots = new HashSet<Rigidbody>();
        foreach (var t in blocks)
        {
            Rigidbody rb = t.GetComponentInParent<Rigidbody>();
            if (rb != null) affectedRoots.Add(rb);
        }

        // 创建永久静态根（无 Rigidbody = 静态碰撞体）
        GameObject steelRoot = new GameObject($"SteelBase_Y{rowY}");
        steelRoot.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        steelRoots.Add(steelRoot);

        var steelMat = new PhysicMaterial("Steel")
        {
            bounciness      = 0f,
            dynamicFriction = 0.8f,
            staticFriction  = 1.5f,
            bounceCombine   = PhysicMaterialCombine.Minimum,
            frictionCombine = PhysicMaterialCombine.Maximum,
        };

        // 计算整行的碰撞体边界
        float minX = float.MaxValue, maxX = float.MinValue, sumY = 0f;
        foreach (var t in blocks)
        {
            float wx = Mathf.Round(t.position.x);
            minX = Mathf.Min(minX, wx);
            maxX = Mathf.Max(maxX, wx);
            sumY += Mathf.Round(t.position.y);
        }
        BoxCollider bc = steelRoot.AddComponent<BoxCollider>();
        bc.center         = new Vector3((minX + maxX) * 0.5f, sumY / blocks.Count, 0f);
        bc.size           = new Vector3(maxX - minX + 1f, 1f, 1f);
        bc.sharedMaterial = steelMat;

        // 重新归属、着色、加 Outline
        foreach (var t in blocks)
        {
            t.position = new Vector3(Mathf.Round(t.position.x), Mathf.Round(t.position.y), 0f);
            t.rotation = Quaternion.identity;
            t.SetParent(steelRoot.transform);

            Renderer rend = t.GetComponentInChildren<Renderer>();
            if (rend != null)
            {
                rend.material.color = new Color(0.65f, 0.72f, 0.82f); // 钢铁蓝灰
                if (rend.GetComponent<Outline>() == null)
                {
                    Outline ol = rend.gameObject.AddComponent<Outline>();
                    ol.OutlineMode  = Outline.Mode.OutlineAll;
                    ol.OutlineColor = new Color(0.8f, 0.88f, 1f);
                    ol.OutlineWidth = 4f;
                }
            }

            settledBlocks.Remove(t);
        }

        // 更新每个受影响的 settled 根
        var settledMat = new PhysicMaterial("Settled")
        {
            bounciness      = 0f,
            dynamicFriction = 0.3f,
            staticFriction  = 1f,
            bounceCombine   = PhysicMaterialCombine.Minimum,
            frictionCombine = PhysicMaterialCombine.Maximum,
        };

        foreach (var rb in affectedRoots)
        {
            if (rb == null) continue;

            // 找出该根节点中剩余的子块
            var remaining = new List<Transform>();
            foreach (var t in settledBlocks)
                if (t != null && t.IsChildOf(rb.transform)) remaining.Add(t);

            // 立即禁用旧碰撞体（Destroy 延迟，disable 立即生效）
            foreach (var col in rb.GetComponents<BoxCollider>())
            {
                col.enabled = false;
                Destroy(col);
            }

            if (remaining.Count == 0)
            {
                Destroy(rb.gameObject);
            }
            else
            {
                RebuildRowColliders(rb.gameObject, rb.transform, remaining, settledMat);
            }
        }
    }

    // 用剩余子块的世界坐标重建 settled 根的行碰撞体（连续段分割，保留中间空格）
    static void RebuildRowColliders(GameObject root, Transform rootTf,
                                    List<Transform> blocks, PhysicMaterial mat)
    {
        // 收集每行所有世界 X 坐标
        var rows = new Dictionary<int, List<float>>();
        foreach (var b in blocks)
        {
            int key = Mathf.RoundToInt(b.position.y);
            if (!rows.ContainsKey(key)) rows[key] = new List<float>();
            rows[key].Add(b.position.x);
        }

        foreach (var kv in rows)
        {
            var xs = kv.Value;
            xs.Sort();

            int segStart = 0;
            for (int i = 1; i <= xs.Count; i++)
            {
                bool isEnd = (i == xs.Count) || (xs[i] - xs[i - 1] > 1.5f);
                if (!isEnd) continue;

                float minX = xs[segStart], maxX = xs[i - 1];
                float wx = (minX + maxX) * 0.5f;
                float wy = kv.Key;
                BoxCollider newBc = root.AddComponent<BoxCollider>();
                newBc.center         = rootTf.InverseTransformPoint(wx, wy, 0f);
                newBc.size           = new Vector3(maxX - minX + 1f, 1f, 1f);
                newBc.sharedMaterial = mat;
                segStart = i;
            }
        }
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

    // ── 胜利动画：outline 变金色加粗，材质变玩家颜色，全部冻结，脉冲放大 ────────

    public void PlayVictoryAnim()
    {
        const float punchDur  = 0.55f;
        const float colorDur  = 0.35f;
        const float stepDelay = 0.06f;

        Color goldOutline = new Color(1f, 0.88f, 0f);
        float delay = 0f;

        // ── 动态 settled 根：全部冻结（含未吸附/角度不正的），再动画 ─────────────
        var roots = new HashSet<Rigidbody>();
        foreach (var t in settledBlocks)
        {
            if (t == null) continue;
            Rigidbody rb = t.GetComponentInParent<Rigidbody>();
            if (rb != null) roots.Add(rb);
        }
        foreach (var rb in roots)
        {
            rb.velocity        = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic     = true;
            VictoryAnimChildren(rb.transform, playerColor, goldOutline, delay, punchDur, colorDur);
            delay += stepDelay;
        }

        // ── 静态钢铁地基 ─────────────────────────────────────────────────────────
        foreach (var sr in steelRoots)
        {
            if (sr == null) continue;
            VictoryAnimChildren(sr.transform, playerColor, goldOutline, delay, punchDur, colorDur);
            delay += stepDelay;
        }

        // ── 初始地基（PlayerColumn 的直接子物体，名称以 Base_ 开头）────────────
        foreach (Transform child in transform)
        {
            if (!child.name.StartsWith("Base_")) continue;
            VictoryAnimSingle(child, playerColor, goldOutline, delay, punchDur, colorDur);
            delay += stepDelay;
        }

        // ── 摄像机震动 ───────────────────────────────────────────────────────────
        Camera cam = playerCamera != null ? playerCamera : Camera.main;
        cam?.DOShakePosition(0.65f, new Vector3(0.2f, 0.12f, 0f), 22, 70f, false);
    }

    // 对 root 的每个直接子块执行胜利动画
    static void VictoryAnimChildren(Transform root, Color playerColor, Color goldOutline,
                                    float delay, float punchDur, float colorDur)
    {
        foreach (Transform child in root)
            VictoryAnimSingle(child, playerColor, goldOutline, delay, punchDur, colorDur);
    }

    // 单个 block unit：材质改玩家色，outline 改金色并加粗 1.5×，DOPunchScale
    static void VictoryAnimSingle(Transform block, Color playerColor, Color goldOutline,
                                  float delay, float punchDur, float colorDur)
    {
        Renderer rend = block.GetComponentInChildren<Renderer>();
        if (rend != null)
        {
            rend.material.DOColor(playerColor, colorDur).SetDelay(delay);
            Outline ol = rend.GetComponent<Outline>();
            if (ol != null)
            {
                ol.OutlineColor = goldOutline;
                ol.OutlineWidth = ol.OutlineWidth * 1.5f;
            }
        }
        block.DOKill();
        block.localScale = Vector3.one;
        block.DOPunchScale(Vector3.one * 0.25f, punchDur, 5, 0.3f).SetDelay(delay);
    }

    // 吸附时震动该玩家的摄像机（需在 Inspector 或 BuildColumn 中赋值 playerCamera）
    public void OnBlockSnapped()
    {
        if (playerCamera != null)
            playerCamera.DOShakePosition(0.22f, new Vector3(0.07f, 0.04f, 0f), 18, 60f, false);
    }

    public List<Transform> GetSettledBlocks() => settledBlocks;
    public bool IsFinished() => isFinished;
}
