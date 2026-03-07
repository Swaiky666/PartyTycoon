using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class TetrisPiece : MonoBehaviour
{
    public enum TetrominoType { I, O, T, S, Z, L, J }

    // 7种形状的子块偏移（相对于 pivot，XY 平面，Y 负 = 向下）
    static readonly Dictionary<TetrominoType, Vector2Int[]> Shapes = new Dictionary<TetrominoType, Vector2Int[]>
    {
        { TetrominoType.I, new[] { new Vector2Int(-1,0), new Vector2Int(0,0), new Vector2Int(1,0), new Vector2Int(2,0) } },
        { TetrominoType.O, new[] { new Vector2Int(0,0),  new Vector2Int(1,0), new Vector2Int(0,-1), new Vector2Int(1,-1) } },
        { TetrominoType.T, new[] { new Vector2Int(-1,0), new Vector2Int(0,0), new Vector2Int(1,0),  new Vector2Int(0,-1) } },
        { TetrominoType.S, new[] { new Vector2Int(0,0),  new Vector2Int(1,0), new Vector2Int(-1,-1),new Vector2Int(0,-1) } },
        { TetrominoType.Z, new[] { new Vector2Int(-1,0), new Vector2Int(0,0), new Vector2Int(0,-1), new Vector2Int(1,-1) } },
        { TetrominoType.L, new[] { new Vector2Int(-1,0), new Vector2Int(0,0), new Vector2Int(1,0),  new Vector2Int(1,-1) } },
        { TetrominoType.J, new[] { new Vector2Int(-1,0), new Vector2Int(0,0), new Vector2Int(1,0),  new Vector2Int(-1,-1)} },
    };

    [Header("下落配置")]
    public float fallSpeed = 4.8f;          // 基础下落速度（原 4f × 1.2）
    public float softDropMultiplier = 10f;  // 快速下落倍率

    // 激光检测距离：距下方物体 < 此值时切换为 Rigidbody 自由接触
    const float TouchDetectDist   = 1.5f;
    // 物理落定判定：速度低于此值持续 PhysicsSettleTime 秒后固化
    const float PhysicsSettleSpeed = 0.2f;
    const float PhysicsSettleTime  = 0.25f;
    const float PhysicsMaxTime     = 4f;   // 超过此秒数强制固化，防止方块永远不落定
    // 掉出地图判定：世界 Y 低于此值时销毁并补生
    const float KillY             = -1.5f;

    [HideInInspector] public TetrisPlayerColumn ownerColumn;
    [HideInInspector] public int playerId;

    internal List<Transform> blockUnits = new List<Transform>();
    private List<Collider> myColliders = new List<Collider>();

    private bool      isSettled        = false;
    private bool      isSoftDropping   = false;
    private bool      isPhysicsFalling  = false;
    private Rigidbody pieceRb           = null;
    private float     physicsSettleTimer = 0f;
    private float     physicsElapsed    = 0f;

    // Ghost piece (drop indicator)
    private Color            _playerColor;
    private List<GameObject> _ghostUnits = new List<GameObject>();

    // ── 初始化 ───────────────────────────────────────────────────────────────

    public TetrominoType PieceType { get; private set; }

    public void Init(TetrominoType type, TetrisPlayerColumn column, int pid,
                     GameObject blockPrefab, Color color)
    {
        PieceType    = type;
        ownerColumn  = column;
        playerId     = pid;
        _playerColor = color;

        foreach (var offset in Shapes[type])
        {
            GameObject block = Instantiate(blockPrefab, transform);
            block.transform.localPosition = new Vector3(offset.x, offset.y, 0f);

            // 玩家颜色
            Renderer rend = block.GetComponentInChildren<Renderer>();
            if (rend != null)
            {
                rend.material = new Material(rend.material); // 实例化材质，避免共享
                rend.material.color = color;
            }

            // 勾边（Quick Outline package）
            Outline outline = block.GetComponentInChildren<Renderer>()?.gameObject.AddComponent<Outline>();
            if (outline != null)
            {
                outline.OutlineMode  = Outline.Mode.OutlineAll;
                outline.OutlineColor = Color.black;
                outline.OutlineWidth = 3f;
            }

            blockUnits.Add(block.transform);
            myColliders.AddRange(block.GetComponentsInChildren<Collider>());
        }

        CreateGhostUnits(blockPrefab, color);
    }

    // ── 帧更新：代码控制下落 ─────────────────────────────────────────────────

    void Update()
    {
        if (isSettled || isPhysicsFalling) return;

        // 掉出地图判定（代码控制阶段）
        foreach (var block in blockUnits)
        {
            if (block != null && block.position.y < KillY) { KillPiece(); return; }
        }

        if (ownerColumn != null && ownerColumn.softDropHeld)
            isSoftDropping = true;

        float speed = isSoftDropping ? fallSpeed * softDropMultiplier : fallSpeed;
        Vector3 step = Vector3.down * speed * Time.deltaTime;

        if (!WouldOverlap(step))
        {
            transform.position += step;
            // 激光检测：快要碰到下方物体了，切换到 Rigidbody 自由接触
            if (IsAboutToTouch()) SwitchToPhysicsFall();
        }
        else
        {
            // 已经紧贴：直接切换物理模式，让 rb 自然吸附
            SwitchToPhysicsFall();
        }

        isSoftDropping = false;
        UpdateGhost();
    }

    // ── 物理帧：监测 rb 落定后固化 ──────────────────────────────────────────

    void FixedUpdate()
    {
        if (!isPhysicsFalling || isSettled || pieceRb == null) return;

        // 掉出地图判定（物理阶段）
        if (pieceRb.position.y < KillY) { KillPiece(); return; }

        float dt = Time.fixedDeltaTime;
        physicsElapsed += dt;

        // 超时强制固化，防止方块持续弹跳永远不落定
        if (physicsElapsed >= PhysicsMaxTime) { Settle(); return; }

        if (pieceRb.velocity.magnitude < PhysicsSettleSpeed)
        {
            physicsSettleTimer += dt;
            if (physicsSettleTimer >= PhysicsSettleTime) Settle();
        }
        else
        {
            physicsSettleTimer = 0f;
        }
    }

    // 激光检测：任意子块正下方 TouchDetectDist 以内有非自身碰撞体
    bool IsAboutToTouch()
    {
        foreach (var block in blockUnits)
        {
            if (Physics.Raycast(block.position, Vector3.down, out RaycastHit hit, TouchDetectDist))
                if (!myColliders.Contains(hit.collider)) return true;
        }
        return false;
    }

    // 切换为 Rigidbody 自由接触模式
    void SwitchToPhysicsFall()
    {
        if (isPhysicsFalling) return;
        isPhysicsFalling = true;
        DestroyGhost();

        pieceRb = gameObject.AddComponent<Rigidbody>();
        pieceRb.mass                   = 0.5f;
        pieceRb.drag                   = 2f;
        pieceRb.angularDrag            = 10f;
        pieceRb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        pieceRb.constraints            = RigidbodyConstraints.FreezePositionZ
                                       | RigidbodyConstraints.FreezeRotationX
                                       | RigidbodyConstraints.FreezeRotationY
                                       | RigidbodyConstraints.FreezeRotationZ;
    }

    // 确保在任何销毁路径（包括外部 Destroy）下都清理 ghost
    void OnDestroy() { DestroyGhost(); }

    // 掉出地图：通知列重新生成，然后销毁自身
    void KillPiece()
    {
        if (isSettled) return;
        isSettled = true; // 复用标志防止重复处理
        DestroyGhost();
        ownerColumn?.OnPieceKilled();
        Destroy(gameObject);
    }

    // ── 外部输入接口（由 TetrisNetworkSync 调用）────────────────────────────

    public void MoveLeft()
    {
        if (isSettled || isPhysicsFalling) return;
        if (!WouldOverlap(Vector3.left) && !WouldExceedBounds(Vector3.left))
            transform.position += Vector3.left;
    }

    public void MoveRight()
    {
        if (isSettled || isPhysicsFalling) return;
        if (!WouldOverlap(Vector3.right) && !WouldExceedBounds(Vector3.right))
            transform.position += Vector3.right;
    }

    public void RotateCW()
    {
        if (isSettled || isPhysicsFalling) return;
        transform.Rotate(0f, 0f, -90f);
        if (WouldOverlapCurrent() || WouldExceedBoundsCurrent())
            transform.Rotate(0f, 0f, 90f);
        else
            TetrisGameController.Instance?.PlayRotateSound();
    }

    public void RotateCCW()
    {
        if (isSettled || isPhysicsFalling) return;
        transform.Rotate(0f, 0f, 90f);
        if (WouldOverlapCurrent() || WouldExceedBoundsCurrent())
            transform.Rotate(0f, 0f, -90f);
        else
            TetrisGameController.Instance?.PlayRotateSound();
    }

    public void SetSoftDrop(bool active) { isSoftDropping = active; }

    public void HardDrop()
    {
        if (isSettled || isPhysicsFalling) return;
        int safety = 300;
        while (safety-- > 0 && !WouldOverlap(Vector3.down * 0.1f))
            transform.position += Vector3.down * 0.1f;
        SwitchToPhysicsFall(); // 硬降：贴底后交给物理落定
    }

    // ── 碰撞检测 ─────────────────────────────────────────────────────────────

    // 检测移动 offset 后是否会与非本件物体重叠
    bool WouldOverlap(Vector3 offset)
    {
        foreach (var block in blockUnits)
        {
            Vector3 projected = block.position + offset;
            Collider[] hits = Physics.OverlapBox(projected, Vector3.one * 0.45f, block.rotation);
            foreach (var hit in hits)
                if (!myColliders.Contains(hit)) return true;

        }
        return false;
    }

    // 检测移动 offset 后是否超出所属列边界
    bool WouldExceedBounds(Vector3 offset)
    {
        if (ownerColumn == null) return false;
        float halfW   = ownerColumn.columnHalfWidth + 1.5f; // 方块中心允许超出地基 2 格
        float centerX = ownerColumn.transform.position.x;
        foreach (var block in blockUnits)
        {
            float nx = block.position.x + offset.x;
            if (nx < centerX - halfW || nx > centerX + halfW) return true;
        }
        return false;
    }

    // 检测当前位置是否超出边界（用于旋转后验证）
    bool WouldExceedBoundsCurrent()
    {
        if (ownerColumn == null) return false;
        float halfW   = ownerColumn.columnHalfWidth + 1.5f;
        float centerX = ownerColumn.transform.position.x;
        foreach (var block in blockUnits)
            if (block.position.x < centerX - halfW || block.position.x > centerX + halfW) return true;
        return false;
    }

    // 检测当前位置（用于旋转后验证）
    bool WouldOverlapCurrent()
    {
        foreach (var block in blockUnits)
        {
            Collider[] hits = Physics.OverlapBox(block.position, Vector3.one * 0.45f, block.rotation);
            foreach (var hit in hits)
                if (!myColliders.Contains(hit)) return true;

        }
        return false;
    }

    // ── 固化：compound Rigidbody（整件不拆散）+ 按行合并碰撞体 ────────────────
    // 1. snap XY → 消除浮点残差，避免与下方碰撞体微小重叠

    void Settle()
    {
        if (isSettled) return;
        isSettled = true;
        DestroyGhost();

        var mat = new PhysicMaterial("Settled")
        {
            bounciness      = 0f,
            dynamicFriction = 0.3f,
            staticFriction  = 1f,
            bounceCombine   = PhysicMaterialCombine.Minimum,
            frictionCombine = PhysicMaterialCombine.Maximum,
        };

        // 1. 同时 snap X 和 Y，消除旋转残差与下落浮点偏移
        foreach (var block in blockUnits)
        {
            Vector3 p = block.position;
            p.x = Mathf.Round(p.x);
            p.y = Mathf.Round(p.y);
            p.z = 0f;
            block.position = p;
            block.rotation = Quaternion.identity;
        }

        // 2. 创建固化根（compound collider 宿主，位于世界原点方便局部坐标计算）
        GameObject root = new GameObject($"Settled_{ownerColumn.playerId}");
        root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

        // 3. 移除子块原有碰撞体，挂到固化根，注册高度检测
        //    先 disable 再 Destroy：Destroy 延迟到帧末，disable 立即生效，
        //    防止 re-parent 后与行合并碰撞体形成双重叠造成物理爆炸
        foreach (var block in blockUnits)
        {
            foreach (var col in block.GetComponentsInChildren<Collider>())
            {
                col.enabled = false;
                Destroy(col);
            }
            block.SetParent(root.transform);
            ownerColumn.RegisterSettledBlock(block);
        }

        // 4. 按行合并碰撞体：同一 Y 行 → 一个横向 BoxCollider
        AddRowColliders(root, blockUnits, mat);

        // 5. 动态 Rigidbody，由 SettledBlockMonitor 限制加速度
        Rigidbody rb = root.AddComponent<Rigidbody>();
        rb.isKinematic            = false;
        rb.mass                   = 0.5f;
        rb.drag                   = 1f;
        rb.angularDrag            = 20f;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.constraints            = RigidbodyConstraints.FreezePositionZ
                                  | RigidbodyConstraints.FreezeRotationX
                                  | RigidbodyConstraints.FreezeRotationY;

        // 6. 新固化块：缓冲 3 帧后转为动态，避免与下方块初始穿透
        SettledBlockMonitor monitor = root.AddComponent<SettledBlockMonitor>();
        monitor.Trigger(rb, () => ownerColumn?.OnBlockSnapped());

        ownerColumn.OnPieceSettled(this);
        Destroy(gameObject); // 销毁 piece 根（子块已转移到 root，不受影响）
    }

    // ── Ghost piece（下坠落点预览）────────────────────────────────────────────

    void CreateGhostUnits(GameObject prefab, Color color)
    {
        Color ghostColor = new Color(color.r, color.g, color.b, 0.28f);
        foreach (var block in blockUnits)
        {
            GameObject ghost = Instantiate(prefab);
            ghost.name = "GhostUnit";

            // 去除所有碰撞体，不影响物理
            foreach (var col in ghost.GetComponentsInChildren<Collider>())
                Destroy(col);

            // 半透明材质
            Renderer rend = ghost.GetComponentInChildren<Renderer>();
            if (rend != null)
            {
                Material mat = new Material(rend.material);
                SetMaterialTransparent(mat);
                mat.color = ghostColor;
                rend.material = mat;
            }

            // Outline 用玩家颜色
            GameObject rendGo = rend != null ? rend.gameObject : ghost;
            Outline ol = rendGo.GetComponent<Outline>() ?? rendGo.AddComponent<Outline>();
            ol.OutlineMode  = Outline.Mode.OutlineAll;
            ol.OutlineColor = new Color(color.r, color.g, color.b, 0.85f);
            ol.OutlineWidth = 4f;

            _ghostUnits.Add(ghost);
        }
    }

    float CalcDropDistance()
    {
        float minDrop = 200f;
        foreach (var block in blockUnits)
        {
            if (block == null) continue;
            if (Physics.BoxCast(block.position, Vector3.one * 0.45f, Vector3.down,
                                out RaycastHit hit, block.rotation, 200f,
                                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (!myColliders.Contains(hit.collider))
                    minDrop = Mathf.Min(minDrop, hit.distance);
            }
        }
        return minDrop < 200f ? minDrop : 0f;
    }

    void UpdateGhost()
    {
        float drop = CalcDropDistance();
        for (int i = 0; i < blockUnits.Count && i < _ghostUnits.Count; i++)
        {
            if (blockUnits[i] == null || _ghostUnits[i] == null) continue;
            _ghostUnits[i].transform.position = blockUnits[i].position + Vector3.down * drop;
            _ghostUnits[i].transform.rotation = blockUnits[i].rotation;
        }
    }

    void DestroyGhost()
    {
        foreach (var g in _ghostUnits) if (g != null) Destroy(g);
        _ghostUnits.Clear();
    }

    static void SetMaterialTransparent(Material mat)
    {
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

    // 按行（Y 四舍五入）把同行子块按连续段分别建 BoxCollider，保留中间空格
    // root 坐标系 = 世界坐标系（root 在原点），所以 center 直接用世界坐标
    static void AddRowColliders(GameObject root, List<Transform> blocks, PhysicMaterial mat)
    {
        // 收集每行所有 X 坐标
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

            // 按连续段（相邻 X 间距 > 1.5 即断开）分别建碰撞体
            int segStart = 0;
            for (int i = 1; i <= xs.Count; i++)
            {
                bool isEnd = (i == xs.Count) || (xs[i] - xs[i - 1] > 1.5f);
                if (!isEnd) continue;

                float minX = xs[segStart], maxX = xs[i - 1];
                BoxCollider bc = root.AddComponent<BoxCollider>();
                bc.center         = new Vector3((minX + maxX) * 0.5f, kv.Key, 0f);
                bc.size           = new Vector3(maxX - minX + 1f, 1f, 1f);
                bc.sharedMaterial = mat;
                segStart = i;
            }
        }
    }
}

