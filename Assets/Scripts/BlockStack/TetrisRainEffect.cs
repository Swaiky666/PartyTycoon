using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 场景背景/前景俄罗斯方块雨装饰效果。
/// 正常状态：从屏幕顶部随机位置生成小方块形状，缓慢落下。
/// 风状态：从来风方向侧面快速生成，横向飞过屏幕。
/// 所有雨块无碰撞体，不干扰游戏物理。
/// </summary>
public class TetrisRainEffect : MonoBehaviour
{
    public static TetrisRainEffect Instance;

    // ── 形状定义（7种Tetromino） ─────────────────────────────────────────────
    static readonly Vector2Int[][] Shapes = {
        new[] { new Vector2Int(-1,0), new Vector2Int(0,0), new Vector2Int(1,0), new Vector2Int(2,0) }, // I
        new[] { new Vector2Int(0,0),  new Vector2Int(1,0), new Vector2Int(0,-1), new Vector2Int(1,-1) }, // O
        new[] { new Vector2Int(-1,0), new Vector2Int(0,0), new Vector2Int(1,0),  new Vector2Int(0,-1) }, // T
        new[] { new Vector2Int(0,0),  new Vector2Int(1,0), new Vector2Int(-1,-1),new Vector2Int(0,-1) }, // S
        new[] { new Vector2Int(-1,0), new Vector2Int(0,0), new Vector2Int(0,-1), new Vector2Int(1,-1) }, // Z
        new[] { new Vector2Int(-1,0), new Vector2Int(0,0), new Vector2Int(1,0),  new Vector2Int(1,-1) }, // L
        new[] { new Vector2Int(-1,0), new Vector2Int(0,0), new Vector2Int(1,0),  new Vector2Int(-1,-1) }, // J
    };

    // ── 配置常量 ─────────────────────────────────────────────────────────────
    const float PieceScale        = 0.42f;  // 整体方块缩放（缩放 root，保持 unit 间无缝拼合）
    const float FallSpeed         = 7f;     // 正常下落速度（units/s）
    const float WindHSpeed        = 20f;    // 风中水平速度（units/s）
    const float SpawnInterval     = 0.055f; // 正常生成间隔（s，约原来 1/4）
    const float WindSpawnInterval = 0.055f; // 风中生成间隔（s）
    const float SpawnY            = 42f;    // 正常生成高度
    const float SpawnXHalf        = 58f;    // 横向生成范围半宽
    const float DestroyY          = -12f;   // 低于此Y值销毁
    const float DestroyXBound     = 85f;    // 超出此X绝对值销毁（风块专用）
    const int   MaxPieces         = 350;    // 最大同屏雨块数（性能保护）

    // 背景层Z（游戏在Z=0）和前景层Z
    static readonly float[] ZLayers = { -8f, 6f };

    // ── 运行时状态 ────────────────────────────────────────────────────────────
    GameObject _prefab;
    bool       _windActive;
    float      _windDir;   // +1=风吹右方向，-1=风吹左方向

    class RainPiece
    {
        public GameObject root;
        public float vx, vy;
        public bool  isWind;
    }

    readonly List<RainPiece> _pieces = new List<RainPiece>();

    // ── 初始化 ────────────────────────────────────────────────────────────────

    void Awake() { Instance = this; }

    /// <summary>由 TetrisGameController 在 Start() 调用，传入方块预制体</summary>
    public void Init(GameObject blockPrefab)
    {
        _prefab = blockPrefab;
        StartCoroutine(SpawnRoutine());
    }

    // ── 帧更新：移动所有雨块 ─────────────────────────────────────────────────

    void Update()
    {
        float dt = Time.deltaTime;

        for (int i = _pieces.Count - 1; i >= 0; i--)
        {
            var p = _pieces[i];
            if (p.root == null) { _pieces.RemoveAt(i); continue; }

            // 正常雨块在风期间被吹向侧面，风停后慢慢漂回零
            if (!p.isWind)
            {
                float targetVx = _windActive ? _windDir * WindHSpeed * 0.88f : 0f;
                p.vx = Mathf.Lerp(p.vx, targetVx, dt * 5.5f);
            }

            p.root.transform.position += new Vector3(p.vx, p.vy, 0f) * dt;

            Vector3 pos = p.root.transform.position;
            bool outY = pos.y < DestroyY;
            bool outX = Mathf.Abs(pos.x) > DestroyXBound;

            if (outY || outX)
            {
                Destroy(p.root);
                _pieces.RemoveAt(i);
            }
        }
    }

    // ── 生成协程 ─────────────────────────────────────────────────────────────

    IEnumerator SpawnRoutine()
    {
        while (true)
        {
            if (_pieces.Count < MaxPieces)
            {
                if (_windActive)
                    SpawnWindPiece();
                else
                    SpawnNormalPiece();
            }
            yield return new WaitForSeconds(_windActive ? WindSpawnInterval : SpawnInterval);
        }
    }

    void SpawnNormalPiece()
    {
        if (_prefab == null) return;

        float x     = Random.Range(-SpawnXHalf, SpawnXHalf);
        float z     = ZLayers[Random.Range(0, ZLayers.Length)];
        float alpha = Random.Range(0.14f, 0.30f);
        Color color = RandomColor(alpha);

        var piece = CreatePiece(new Vector3(x, SpawnY, z), color);
        piece.vx     = Random.Range(-0.8f, 0.8f); // 轻微横向漂移
        piece.vy     = -FallSpeed * Random.Range(0.7f, 1.35f);
        piece.isWind = false;
        _pieces.Add(piece);
    }

    void SpawnWindPiece()
    {
        if (_prefab == null) return;

        // 来风方向（_windDir=+1风吹右，从左侧生成）
        float spawnX = -_windDir * (SpawnXHalf + 8f);
        float y      = Random.Range(-5f, SpawnY + 5f);
        float z      = ZLayers[Random.Range(0, ZLayers.Length)];
        float alpha  = Random.Range(0.18f, 0.38f);
        Color color  = RandomColor(alpha);

        var piece = CreatePiece(new Vector3(spawnX, y, z), color);
        piece.vx     = _windDir * WindHSpeed * Random.Range(0.75f, 1.25f);
        piece.vy     = -FallSpeed * Random.Range(0.2f, 0.6f);
        piece.isWind = true;
        _pieces.Add(piece);
    }

    // ── 创建单个雨块 ─────────────────────────────────────────────────────────

    RainPiece CreatePiece(Vector3 position, Color color)
    {
        int shapeIdx = Random.Range(0, Shapes.Length);

        GameObject root = new GameObject("RainPiece");
        root.transform.SetPositionAndRotation(
            position,
            Quaternion.Euler(0f, 0f, Random.Range(0, 4) * 90f));
        // 缩放整个 root，子 block 保持 localScale=1，间距由 root 缩放决定，无缝拼合
        root.transform.localScale = Vector3.one * PieceScale;

        foreach (var offset in Shapes[shapeIdx])
        {
            if (_prefab == null) break;
            GameObject block = Instantiate(_prefab, root.transform);
            block.transform.localPosition = new Vector3(offset.x, offset.y, 0f);
            // 不单独缩放 block，由 root 统一缩放

            // 移除碰撞体，不参与物理
            foreach (var col in block.GetComponentsInChildren<Collider>())
                Destroy(col);

            // 移除 Outline（节省性能）
            foreach (var ol in block.GetComponentsInChildren<Outline>())
                Destroy(ol);

            // 透明材质
            Renderer rend = block.GetComponentInChildren<Renderer>();
            if (rend != null)
            {
                Material mat = new Material(rend.sharedMaterial);
                ApplyTransparent(mat, color);
                rend.material = mat;
            }
        }

        return new RainPiece { root = root };
    }

    // ── 外部接口：风事件 ─────────────────────────────────────────────────────

    /// <summary>大风开始时调用。dir = +1（风吹右）或 -1（风吹左）</summary>
    public void OnWindStart(float dir)
    {
        _windActive = true;
        _windDir    = dir;
    }

    /// <summary>大风结束时调用</summary>
    public void OnWindEnd()
    {
        _windActive = false;
    }

    // ── 工具方法 ─────────────────────────────────────────────────────────────

    static Color RandomColor(float alpha)
    {
        Color c = Random.ColorHSV(0f, 1f, 0.55f, 1f, 0.75f, 1f);
        c.a = alpha;
        return c;
    }

    static void ApplyTransparent(Material mat, Color color)
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
        // URP
        if (mat.HasProperty("_Surface"))
        {
            mat.SetFloat("_Surface", 1);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
        mat.color = color;
    }
}
