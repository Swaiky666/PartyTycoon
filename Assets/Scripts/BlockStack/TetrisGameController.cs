using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class TetrisGameController : MonoBehaviour
{
    public static TetrisGameController Instance;

    // ── Inspector 只需配置这几项 ────────────────────────────────────────────

    [Header("必填：方块单元预制体（1×1×1 Cube，有 BoxCollider，无 Rigidbody）")]
    public GameObject blockUnitPrefab;

    [Header("UI 引用")]
    public Canvas          gameCanvas;            // 主 Canvas（按钮生成到此处）
    public TextMeshProUGUI countdownText;
    public TextMeshProUGUI statusText;
    public TextMeshProUGUI rankingText;
    [Header("游戏配置")]
    public float countdownDuration = 3f;
    public float gameTimeLimit     = 60f;     // 游戏时限（秒）

    [Header("列布局")]
    public float columnSpacing  = 15f;        // 列中心间距
    public float columnWidth    = 5f;         // 列宽（单位）
    public float finishLineY    = 18f;        // 完成高度线
    public float spawnY         = 19f;        // 方块生成 Y

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

    [HideInInspector] public List<TetrisPlayerColumn> columns = new List<TetrisPlayerColumn>();

    private List<int> finishOrder = new List<int>();
    private float     gameTimer   = 0f;
    private bool      isPlaying   = false;
    private int       activePlayers;

    // ── 生命周期 ─────────────────────────────────────────────────────────────

    void Awake() { Instance = this; }

    void Start()
    {
        var saved = GameDataManager.Instance?.savedPlayers;
        activePlayers = Mathf.Clamp(
            saved != null && saved.Count > 0 ? saved.Count : 6,
            1, 6);

        finishLineY *= 1.5f;
        spawnY = finishLineY + 1f;

        GenerateColumns(activePlayers, saved);
        GenerateButtonPanels();
        if (rankingText != null) rankingText.gameObject.SetActive(false);
        if (statusText != null) statusText.gameObject.SetActive(false);
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

        UpdateLiveRanking();
    }

    // ── 程序化生成所有列 ──────────────────────────────────────────────────────

    void GenerateColumns(int count, List<PlayerState> saved)
    {
        float totalWidth = (count - 1) * columnSpacing;
        float startX     = -totalWidth / 2f;

        for (int i = 0; i < count; i++)
        {
            float colX = startX + i * columnSpacing;
            int   pid  = (saved != null && i < saved.Count) ? saved[i].playerId : (i + 1);
            Color col  = i < playerColors.Length ? playerColors[i] : Color.white;

            TetrisPlayerColumn column = BuildColumn(i, colX, pid, col);
            column.spawnWaitSeconds = 0.4f;
            columns.Add(column);
        }
    }

    TetrisPlayerColumn BuildColumn(int index, float worldX, int pid, Color color)
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
            }
            // 地基不需要 Rigidbody，BoxCollider 保留作为静态地板
        }

        // ── 完成高度线（细长红色 Cube，不参与物理） ───────────────────────────
        GameObject finishGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        finishGo.name = "FinishLine";
        finishGo.transform.SetParent(colGo.transform);
        finishGo.transform.localPosition = new Vector3(0f, finishLineY, 0f);
        finishGo.transform.localScale    = new Vector3(columnWidth, 0.05f, 0.5f);
        finishGo.GetComponent<Renderer>().material.color = Color.red;
        Destroy(finishGo.GetComponent<BoxCollider>()); // 不参与物理
        column.finishLineTransform = finishGo.transform;

        // ── SpawnPoint ────────────────────────────────────────────────────────
        GameObject spawnGo = new GameObject("SpawnPoint");
        spawnGo.transform.SetParent(colGo.transform);
        spawnGo.transform.localPosition = new Vector3(0f, spawnY, 0f);
        column.spawnPoint = spawnGo.transform;

        // ── 玩家标签（需要 TextMeshPro 包，可选） ────────────────────────────
        // column.playerLabel = ... （如需 UI 标签，在子 Canvas 上添加 TMPro 组件后拖入）

        // ── 初始化 Column ─────────────────────────────────────────────────────
        column.Init(pid, color, blockUnitPrefab);

        return column;
    }

    // ── 底部操作按钮（每列三个：← → ↻）────────────────────────────────────

    void GenerateButtonPanels()
    {
        if (gameCanvas == null) return;

        int   count  = columns.Count;
        float panelW = 165f;
        float gap    = 10f;
        float totalW = count * panelW + (count - 1) * gap;
        float startX = -totalW / 2f + panelW / 2f;

        for (int i = 0; i < count; i++)
            CreateButtonPanel(columns[i], startX + i * (panelW + gap));
    }

    void CreateButtonPanel(TetrisPlayerColumn col, float posX)
    {
        GameObject panel = new GameObject($"BtnPanel_P{col.playerId}");
        panel.transform.SetParent(gameCanvas.transform, false);

        RectTransform rt = panel.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0f);
        rt.anchorMax        = new Vector2(0.5f, 0f);
        rt.pivot            = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(posX, 20f);
        rt.sizeDelta        = new Vector2(165f, 70f);

        float btnW = 35f, btnH = 65f, btnGap = 8.33f;
        float startBX = -1.5f * (btnW + btnGap);

        AddButton(panel.transform, "←", () => col.currentPiece?.MoveLeft(),  startBX + 0 * (btnW + btnGap), btnW, btnH);
        AddButton(panel.transform, "→", () => col.currentPiece?.MoveRight(), startBX + 1 * (btnW + btnGap), btnW, btnH);
        AddButton(panel.transform, "↻", () => col.currentPiece?.RotateCW(),  startBX + 2 * (btnW + btnGap), btnW, btnH);
        AddSoftDropButton(panel.transform, col,                               startBX + 3 * (btnW + btnGap), btnW, btnH);
    }

    void AddSoftDropButton(Transform parent, TetrisPlayerColumn col,
                           float posX, float width, float height)
    {
        GameObject go = new GameObject("↓");
        go.transform.SetParent(parent, false);

        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(posX, 0f);
        rt.sizeDelta        = new Vector2(width, height);

        Image img = go.AddComponent<Image>();
        img.color = new Color(0.1f, 0.1f, 0.1f, 0.75f);

        EventTrigger trigger = go.AddComponent<EventTrigger>();

        var downEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
        downEntry.callback.AddListener(_ => col.softDropHeld = true);
        trigger.triggers.Add(downEntry);

        var upEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
        upEntry.callback.AddListener(_ => col.softDropHeld = false);
        trigger.triggers.Add(upEntry);

        // 文字标签
        GameObject textGo = new GameObject("Text");
        textGo.transform.SetParent(go.transform, false);
        RectTransform trt = textGo.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;
        TextMeshProUGUI tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text      = "↓";
        tmp.fontSize  = 30f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color     = Color.white;
    }

    void AddButton(Transform parent, string label, System.Action onClick,
                   float posX, float width, float height)
    {
        GameObject go = new GameObject(label);
        go.transform.SetParent(parent, false);

        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(posX, 0f);
        rt.sizeDelta        = new Vector2(width, height);

        Image img = go.AddComponent<Image>();
        img.color = new Color(0.1f, 0.1f, 0.1f, 0.75f);

        Button btn = go.AddComponent<Button>();
        ColorBlock cb       = btn.colors;
        cb.highlightedColor = new Color(0.35f, 0.35f, 0.35f, 0.9f);
        cb.pressedColor     = new Color(0.6f, 0.6f, 0.6f, 1f);
        btn.colors          = cb;
        btn.onClick.AddListener(() => onClick());

        // TMP 文字标签
        GameObject textGo = new GameObject("Text");
        textGo.transform.SetParent(go.transform, false);

        RectTransform trt = textGo.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text      = label;
        tmp.fontSize  = 30f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color     = Color.white;
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
        if (statusText != null)
        {
            statusText.gameObject.SetActive(true);
            statusText.text = "把方块堆到红线！";
        }
        if (countdownText != null)
        {
            countdownText.gameObject.SetActive(true);
            countdownText.text = Mathf.CeilToInt(gameTimeLimit).ToString();
        }
        if (rankingText != null) rankingText.gameObject.SetActive(true);

        foreach (var col in columns) col.BeginSpawning();

        TetrisNetworkSync sync = GetComponent<TetrisNetworkSync>();
        if (sync != null) sync.enabled = true;
    }

    // ── 玩家完成回调 ─────────────────────────────────────────────────────────

    public void OnPlayerFinished(int playerId)
    {
        if (finishOrder.Contains(playerId)) return;
        finishOrder.Add(playerId);
        Debug.Log($"[BlockStack] 玩家 {playerId} 完成！名次: {finishOrder.Count}");
        UpdateRankingUI();
        if (finishOrder.Count >= activePlayers) FinishGame();
    }

    // ── 超时 ─────────────────────────────────────────────────────────────────

    void OnTimeUp()
    {
        if (!isPlaying) return;

        // 未完成的列按当前高度降序排列后追加进 finishOrder
        var unfinished = new List<TetrisPlayerColumn>();
        foreach (var col in columns)
            if (!col.IsFinished()) unfinished.Add(col);

        unfinished.Sort((a, b) => b.GetCurrentMaxHeight().CompareTo(a.GetCurrentMaxHeight()));

        foreach (var col in unfinished)
        {
            if (!finishOrder.Contains(col.playerId)) finishOrder.Add(col.playerId);
        }

        FinishGame();
    }

    // ── 结束 ─────────────────────────────────────────────────────────────────

    void FinishGame()
    {
        isPlaying = false;
        if (statusText != null) statusText.text = "游戏结束！";
        foreach (var col in columns)
        {
            col.Stop();
            col.ReleaseBlocks(); // 统一释放为 Rigidbody，触发倒塌动画
        }

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

    // 实时排名（游戏中每帧更新，按当前最高高度排序）
    void UpdateLiveRanking()
    {
        if (rankingText == null) return;

        // 完成的列按完成顺序钉在前面，未完成的列按当前高度在后面排
        var sorted = new List<TetrisPlayerColumn>(columns);
        sorted.Sort((a, b) =>
        {
            bool aFin = a.IsFinished(), bFin = b.IsFinished();
            if (aFin && bFin)
                return finishOrder.IndexOf(a.playerId).CompareTo(finishOrder.IndexOf(b.playerId));
            if (aFin) return -1;
            if (bFin) return  1;
            return b.GetCurrentMaxHeight().CompareTo(a.GetCurrentMaxHeight());
        });

        System.Text.StringBuilder sb = new System.Text.StringBuilder("<b>当前名次</b>\n");
        for (int i = 0; i < sorted.Count; i++)
        {
            var col = sorted[i];
            string label = $"P{col.playerId}";
            string mark = col.IsFinished() ? " <color=lime>完成</color>" : "";
            sb.Append($"{i + 1}. {label}{mark}\n");
        }
        rankingText.text = sb.ToString();
    }

    // 最终名次（游戏结束后覆盖显示）
    void UpdateRankingUI()
    {
        if (rankingText == null) return;
        string txt = "<b>最终名次</b>\n";
        for (int i = 0; i < finishOrder.Count; i++)
            txt += $"{i + 1}. 玩家 {finishOrder[i]}\n";
        rankingText.text = txt;
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
