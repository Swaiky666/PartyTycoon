using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using DG.Tweening;

public class TetrisGameController : MonoBehaviour
{
    public static TetrisGameController Instance;

    // ── Inspector 只需配置这几项 ────────────────────────────────────────────

    [Header("必填：方块单元预制体（1×1×1 Cube，有 BoxCollider，无 Rigidbody）")]
    public GameObject blockUnitPrefab;

    [Header("UI 引用")]
    public Canvas          gameCanvas;            // 主 Canvas（按钮生成到此处）
    public TextMeshProUGUI countdownText;
    [Header("游戏配置")]
    public float countdownDuration = 5f;
    public float gameTimeLimit     = 200f;    // 游戏时限（秒）

    [Header("大风配置")]
    public float windInterval = 30f;   // 大风间隔（秒）
    public float windForce    = 12f;   // 横向冲量大小

    [Header("列布局")]
    public float columnSpacing  = 16f;        // 列中心间距
    public float columnWidth    = 5f;         // 列宽（单位）
    public float finishLineY    = 18f;        // 完成高度线
    public float spawnY         = 19f;        // 方块生成 Y

    [Header("音效（Inspector 拖入）")]
    public AudioClip sfxRotate;   // 旋转方块       建议时长：0.15–0.25s  短促 click/swish
    public AudioClip sfxSnap;     // 方块吸附锁定   建议时长：0.25–0.40s  清脆 thud/lock
    public AudioClip sfxSteel;    // 五连锁定消行   建议时长：0.50–0.80s  厚重金属 clank
    public AudioClip sfxFinish;   // 胜利结束       建议时长：1.50–3.00s  短小 fanfare
    public AudioClip sfxWind;     // 吹风           建议时长：2.00–4.00s  持续 whoosh（会循环）

    [Header("玩家颜色（按玩家序号对应）")]
    public Color[] playerColors = new Color[]
    {
        Color.red,
        new Color(0.2f, 0.5f, 1f),   // 蓝
        new Color(0.2f, 0.9f, 0.3f), // 绿
        Color.yellow,
        new Color(1f, 0.4f, 1f),     // 粉紫
        Color.cyan
    };

    // ── 运行时 ───────────────────────────────────────────────────────────────

    [HideInInspector] public List<TetrisPlayerColumn> columns     = new List<TetrisPlayerColumn>();
    [HideInInspector] public TetrisPlayerColumn       localColumn = null; // 本机玩家控制的列

    private List<int>               finishOrder    = new List<int>();
    private List<TetrisPlayerColumn> _liveRankOrder = new List<TetrisPlayerColumn>();
    private float     gameTimer   = 0f;
    private float     windTimer   = 0f;
    private bool      isPlaying   = false;
    private int       activePlayers;

    const float OvertakeThreshold = 1.0f; // 必须领先这么多才能超越名次

    static readonly string[] RandomNames =
    {
        "Blaze", "Storm", "Nova", "Pixel",
        "Echo",  "Spark", "Frost", "Volt",
        "Drift", "Neon",  "Flux",  "Zap"
    };

    // ── 生命周期 ─────────────────────────────────────────────────────────────

    AudioSource _sfxSource;
    AudioSource _windSource;

    void Awake()
    {
        Instance = this;
        Screen.SetResolution(1334, 750, false);

        _sfxSource              = gameObject.AddComponent<AudioSource>();
        _sfxSource.playOnAwake  = false;

        _windSource             = gameObject.AddComponent<AudioSource>();
        _windSource.playOnAwake = false;
        _windSource.loop        = true;  // 风声循环直到风结束
    }

    // ── 音效接口（供其他脚本调用）────────────────────────────────────────────
    public void PlayRotateSound() => PlaySfx(sfxRotate);
    public void PlaySnapSound()   => PlaySfx(sfxSnap);
    public void PlaySteelSound()  => PlaySfx(sfxSteel);
    public void PlayFinishSound() => PlaySfx(sfxFinish);

    public void PlayWindSound()
    {
        if (sfxWind == null) return;
        _windSource.clip = sfxWind;
        _windSource.Play();
    }
    public void StopWindSound() => _windSource.Stop();

    void PlaySfx(AudioClip clip)
    {
        if (clip != null) _sfxSource.PlayOneShot(clip);
    }

    void Start()
    {
        var saved = GameDataManager.Instance?.savedPlayers;
        activePlayers = Mathf.Clamp(
            saved != null && saved.Count > 0 ? saved.Count : 6,
            1, 6);

        finishLineY *= 1.5f;
        spawnY = finishLineY + 8f;

        GenerateColumns(activePlayers, saved);
        ResolveLocalColumn(saved);

        // 方块雨装饰效果
        GameObject rainGo = new GameObject("TetrisRainEffect");
        TetrisRainEffect rain = rainGo.AddComponent<TetrisRainEffect>();
        rain.Init(blockUnitPrefab);
        StartCoroutine(CountdownRoutine());
    }

    void Update()
    {
        if (!isPlaying) return;
        gameTimer += Time.deltaTime;

        if (gameTimer >= gameTimeLimit)
        {
            OnTimeUp();
            return;
        }

        // 实时倒计时显示（向上取整，保持 "1" 显示到最后一帧）
        if (countdownText != null)
            countdownText.text = Mathf.CeilToInt(gameTimeLimit - gameTimer).ToString();

        // 大风计时
        windTimer += Time.deltaTime;
        if (windTimer >= windInterval)
        {
            windTimer = 0f;
            TriggerWind();
        }

        UpdateLiveRanking();
    }

    // ── 大风 ─────────────────────────────────────────────────────────────────

    void TriggerWind()
    {
        // 随机选一个横向方向，Host 通过网络层广播给所有客户端（真实联网时只有 Host 调用此方法）
        float dir = Random.value > 0.5f ? 1f : -1f;
        if (GameNetworkManager.Instance != null)
            GameNetworkManager.Instance.SendBSWindRequest(dir * windForce);
        else
            ExecuteNetBSWind(dir * windForce);
    }

    /// <summary>[网络广播] 执行大风（GameNetworkManager 收到服务端广播后调用）</summary>
    public void ExecuteNetBSWind(float horizontalForce)
    {
        foreach (var col in columns)
            col.ApplyWind(horizontalForce);

        PlayWindSound();
        TetrisRainEffect.Instance?.OnWindStart(Mathf.Sign(horizontalForce));
        StartCoroutine(EndWindRain());
    }

    IEnumerator EndWindRain()
    {
        yield return new WaitForSeconds(3.5f);
        StopWindSound();
        TetrisRainEffect.Instance?.OnWindEnd();
    }

    // ── 程序化生成所有列 ──────────────────────────────────────────────────────

    void GenerateColumns(int count, List<PlayerState> saved)
    {
        float totalWidth = (count - 1) * columnSpacing;
        float startX     = -totalWidth / 2f;

        // 随机打乱名字池，取前 count 个
        var namePool = new List<string>(RandomNames);
        for (int k = namePool.Count - 1; k > 0; k--)
        {
            int j = Random.Range(0, k + 1);
            (namePool[k], namePool[j]) = (namePool[j], namePool[k]);
        }

        for (int i = 0; i < count; i++)
        {
            float  colX  = startX + i * columnSpacing;
            int    pid   = (saved != null && i < saved.Count) ? saved[i].playerId : (i + 1);
            Color  color = i < playerColors.Length ? playerColors[i] : Color.white;
            string name  = i < namePool.Count ? namePool[i] : $"P{pid}";

            TetrisPlayerColumn column = BuildColumn(i, colX, pid, color, name);
            column.spawnWaitSeconds = 0.4f;
            columns.Add(column);
        }

        // 所有列共享一条完成高度线（用实际列位置算世界中心，避免 Controller 不在原点时偏移）
        float worldCenterX = (columns[0].transform.position.x + columns[count - 1].transform.position.x) / 2f;
        Transform sharedLine = BuildSharedFinishLine(count, worldCenterX);
        foreach (var col in columns) col.finishLineTransform = sharedLine;
    }

    TetrisPlayerColumn BuildColumn(int index, float worldX, int pid, Color color, string playerName)
    {
        // ── 父物体 ────────────────────────────────────────────────────────────
        GameObject colGo = new GameObject($"PlayerColumn_{pid}");
        colGo.transform.SetParent(transform);
        colGo.transform.position = new Vector3(worldX, 0f, 0f);

        TetrisPlayerColumn column = colGo.AddComponent<TetrisPlayerColumn>();

        // ── 地基：一排 BlockUnit 预制体（视觉 + 物理地板） ────────────────────
        int baseCount = Mathf.RoundToInt(columnWidth); // 5 个
        for (int x = 0; x < baseCount; x++)
        {
            float localX = x - (baseCount - 1) / 2f;  // -2, -1, 0, 1, 2
            GameObject baseBlock = Instantiate(blockUnitPrefab, colGo.transform);
            baseBlock.name = $"Base_{x}";
            baseBlock.transform.localPosition = new Vector3(localX, 0f, 0f);

            // 地基用深灰色区分
            Renderer rend = baseBlock.GetComponentInChildren<Renderer>();
            if (rend != null)
            {
                rend.material = new Material(rend.sharedMaterial);
                rend.material.color = new Color(0.25f, 0.25f, 0.25f);

                Outline ol = rend.gameObject.AddComponent<Outline>();
                ol.OutlineMode  = Outline.Mode.OutlineAll;
                ol.OutlineColor = new Color(0.8f, 0.88f, 1f);
                ol.OutlineWidth = 4f;
            }
            // 地基不需要 Rigidbody，BoxCollider 保留作为静态地板
        }

        // finishLineTransform 由 BuildSharedFinishLine() 统一设置，此处留空

        // ── SpawnPoint ────────────────────────────────────────────────────────
        GameObject spawnGo = new GameObject("SpawnPoint");
        spawnGo.transform.SetParent(colGo.transform);
        spawnGo.transform.localPosition = new Vector3(0f, spawnY, 0f);
        column.spawnPoint = spawnGo.transform;

        // ── 名次文字（TextMeshPro 3D，地基正下方） ───────────────────────────
        GameObject rankGo = new GameObject("RankText");
        rankGo.transform.SetParent(colGo.transform);
        rankGo.transform.localPosition = new Vector3(0f, -1.8f, -0.5f); // 稍微偏向摄像机
        TextMeshPro rankTmp = rankGo.AddComponent<TextMeshPro>();
        rankTmp.text      = playerName;   // 倒计时期间显示玩家名，开始后被 SetRank 替换
        rankTmp.fontSize  = 12.5f;
        rankTmp.alignment = TextAlignmentOptions.Center;
        rankTmp.color     = color;
        rankTmp.fontStyle = FontStyles.Bold;
        column.rankText   = rankTmp;

        // ── 初始化 Column ─────────────────────────────────────────────────────
        column.Init(pid, color, blockUnitPrefab, playerName);
        column.rowWidth        = Mathf.RoundToInt(columnWidth);
        column.columnHalfWidth = columnWidth / 2f;

        return column;
    }

    // ── 横跨所有列的共享完成高度线 ───────────────────────────────────────────

    Transform BuildSharedFinishLine(int count, float worldCenterX)
    {
        float lineWidth = (count - 1) * columnSpacing + columnWidth;

        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "FinishLine";
        go.transform.SetParent(transform);
        // 用世界坐标定位，保证居中不受 Controller 自身位置影响
        go.transform.position   = new Vector3(worldCenterX, finishLineY, 0f);
        go.transform.localScale = new Vector3(lineWidth, 0.22f, 0.01f);
        Destroy(go.GetComponent<BoxCollider>());

        Renderer rend = go.GetComponent<Renderer>();
        Material mat  = new Material(rend.sharedMaterial);
        // 开启透明模式（Standard Shader Transparent）
        mat.SetFloat("_Mode", 3);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = 3000;
        mat.color = new Color(1f, 1f, 1f, 0.35f);   // 白色，35% 不透明
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", Color.white * 1.5f);
        rend.material = mat;

        return go.transform;
    }

    // ── 本机玩家列识别 ────────────────────────────────────────────────────────

    void ResolveLocalColumn(List<PlayerState> saved)
    {
        // 优先从 GameDataManager 取本机玩家 ID（联网时由服务端分配）
        // 当前模拟阶段：取存档第一位，或直接用 columns[0]
        int localPid = -1;
        if (saved != null && saved.Count > 0)
            localPid = saved[0].playerId;

        localColumn = columns.Find(c => c.playerId == localPid);
        if (localColumn == null && columns.Count > 0)
            localColumn = columns[0];
    }

    // ── 倒计时 ───────────────────────────────────────────────────────────────

    IEnumerator CountdownRoutine()
    {
        if (countdownText != null) countdownText.gameObject.SetActive(true);

        for (int i = (int)countdownDuration; i > 0; i--)
        {
            if (countdownText != null) countdownText.text = i.ToString();
            yield return new WaitForSeconds(1f);
        }

        if (countdownText != null)
        {
            countdownText.text = "GO!";
            yield return new WaitForSeconds(0.5f);
            // 不隐藏，StartGame() 接管显示游戏倒计时
        }

        StartGame();
    }

    // ── 开始游戏 ─────────────────────────────────────────────────────────────

    void StartGame()
    {
        isPlaying = true;
        if (countdownText != null)
        {
            countdownText.gameObject.SetActive(true);
            countdownText.text = Mathf.CeilToInt(gameTimeLimit).ToString();
        }
        _liveRankOrder = new List<TetrisPlayerColumn>(columns);
        foreach (var col in columns) col.BeginSpawning();

        // 本机玩家：挂输入处理器
        if (localColumn != null)
        {
            var inputHandler = gameObject.AddComponent<TetrisInputHandler>();
            inputHandler.localColumn = localColumn;
        }

        // 非本机玩家列：挂 AI 模拟控制器
        foreach (var col in columns)
        {
            if (col == localColumn) continue;
            var ai = col.gameObject.AddComponent<TetrisAIController>();
            ai.StartAI(col);
        }
    }

    // ── 玩家完成回调 ─────────────────────────────────────────────────────────

    /// <summary>由 TetrisPlayerColumn 检测到到达终点后调用，通过网络层发送请求</summary>
    public void OnPlayerFinished(int playerId)
    {
        if (GameNetworkManager.Instance != null)
            GameNetworkManager.Instance.SendBSFinishRequest(playerId);
        else
            ExecuteNetBSFinish(playerId);
    }

    /// <summary>[网络广播] 确认玩家完成，记录名次，触发结束（GameNetworkManager 回调）</summary>
    public void ExecuteNetBSFinish(int playerId)
    {
        if (finishOrder.Contains(playerId)) return;
        finishOrder.Add(playerId);
        Debug.Log($"[BlockStack] 玩家 {playerId} 完成！名次: {finishOrder.Count}");
        UpdateRankingUI();
        FinishGame(); // 第一个到达终点即触发结束
    }

    /// <summary>[网络广播] 在指定玩家列生成对应类型方块（GameNetworkManager 回调）</summary>
    public void ExecuteNetBSSpawn(int playerId, int pieceTypeIndex)
    {
        var col = columns.Find(c => c.playerId == playerId);
        col?.DoSpawnPiece((TetrisPiece.TetrominoType)pieceTypeIndex);
    }

    // ── 超时 ─────────────────────────────────────────────────────────────────

    void OnTimeUp()
    {
        FinishGame();
    }

    // ── 结束 ─────────────────────────────────────────────────────────────────

    void FinishGame()
    {
        if (!isPlaying) return;
        isPlaying = false;

        // 未完成的列按当前高度降序排列后追加进 finishOrder
        var unfinished = new List<TetrisPlayerColumn>();
        foreach (var col in columns)
            if (!col.IsFinished()) unfinished.Add(col);
        unfinished.Sort((a, b) => b.GetCurrentMaxHeight().CompareTo(a.GetCurrentMaxHeight()));
        foreach (var col in unfinished)
            if (!finishOrder.Contains(col.playerId)) finishOrder.Add(col.playerId);
        int winnerId = finishOrder.Count > 0 ? finishOrder[0] : -1;
        foreach (var col in columns)
        {
            // 停止 AI
            var ai = col.GetComponent<TetrisAIController>();
            if (ai != null) ai.StopAI();

            col.Stop();
            if (col.playerId == winnerId)
                col.PlayVictoryAnim(); // 只有胜利玩家才触发特效
        }
        PlayFinishSound();
        Camera.main?.DOShakePosition(1.0f, new Vector3(0.55f, 0.35f, 0f), 28, 80f, false);

        TetrisNetworkSync sync = GetComponent<TetrisNetworkSync>();
        if (sync != null) sync.enabled = false;

        StartCoroutine(EndSequence());
    }

    IEnumerator EndSequence()
    {
        UpdateRankingUI();
        yield return new WaitForSeconds(3f);
        BackToMainGame();
    }

    // 实时排名（游戏中每帧更新，带滞后阈值防止名次抖动）
    void UpdateLiveRanking()
    {
        // ── 提取未完成玩家，保持 _liveRankOrder 中的稳定顺序 ─────────────────
        var unfinished = new List<TetrisPlayerColumn>();
        foreach (var col in _liveRankOrder)
            if (!col.IsFinished()) unfinished.Add(col);

        // ── 带阈值的冒泡排序：后者必须领先 OvertakeThreshold 才能超越 ─────────
        bool swapped;
        do
        {
            swapped = false;
            for (int i = 0; i < unfinished.Count - 1; i++)
            {
                if (unfinished[i + 1].GetCurrentMaxHeight() >
                    unfinished[i].GetCurrentMaxHeight() + OvertakeThreshold)
                {
                    (unfinished[i], unfinished[i + 1]) = (unfinished[i + 1], unfinished[i]);
                    swapped = true;
                }
            }
        } while (swapped);

        // ── 重建完整排名：已完成在前（按完成顺序），未完成在后 ─────────────────
        _liveRankOrder.Clear();
        foreach (var pid in finishOrder)
            foreach (var col in columns)
                if (col.playerId == pid) { _liveRankOrder.Add(col); break; }
        _liveRankOrder.AddRange(unfinished);

        // ── 更新各列名次文字 ──────────────────────────────────────────────────
        for (int i = 0; i < _liveRankOrder.Count; i++)
            _liveRankOrder[i].SetRank(i + 1);
    }

    // 最终名次（游戏结束后锁定各列名次文字）
    void UpdateRankingUI()
    {
        for (int i = 0; i < finishOrder.Count; i++)
            foreach (var col in columns)
                if (col.playerId == finishOrder[i]) { col.SetRank(i + 1); break; }
    }

    void BackToMainGame()
    {
        if (GameDataManager.Instance != null)
        {
            GameDataManager.Instance.minigameRanking.Clear();
            GameDataManager.Instance.minigameRanking.AddRange(finishOrder);
            Debug.Log($"[BlockStack] 排名写入: [{string.Join(",", finishOrder)}]");
        }
        SceneManager.LoadScene("MainGameScene");
    }
}
