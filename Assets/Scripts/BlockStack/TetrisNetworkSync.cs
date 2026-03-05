using UnityEngine;

/// <summary>
/// 模拟阶段：负责轮询所有玩家键盘输入并调用对应 TetrisPiece 方法。
/// 联网阶段替换此脚本：Host 模拟物理 + 广播刚体状态，Client 插值追赶。
/// </summary>
public class TetrisNetworkSync : MonoBehaviour
{
    [Header("模拟配置")]
    public bool isHost = true;              // 模拟阶段恒为 true

    [Header("长按移动配置")]
    public float moveRepeatInitialDelay = 0.18f;  // 首次长按重复前的等待（秒）
    public float moveRepeatInterval     = 0.07f;  // 连续重复间隔（秒）

    // 键位顺序：[左, 右, 旋转CW, 软落(持续), 硬落]
    static readonly KeyCode[][] KeyBindings = new KeyCode[][]
    {
        new[] { KeyCode.A,         KeyCode.D,          KeyCode.W,       KeyCode.S,         KeyCode.Space   }, // P1
        new[] { KeyCode.LeftArrow, KeyCode.RightArrow,  KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.Return  }, // P2
        new[] { KeyCode.J,         KeyCode.L,           KeyCode.I,       KeyCode.K,         KeyCode.U       }, // P3
        new[] { KeyCode.Keypad4,   KeyCode.Keypad6,     KeyCode.Keypad8, KeyCode.Keypad5,   KeyCode.Keypad0 }, // P4
        new[] { KeyCode.G,         KeyCode.H,           KeyCode.Y,       KeyCode.B,         KeyCode.N       }, // P5
        new[] { KeyCode.None,      KeyCode.None,        KeyCode.None,    KeyCode.None,      KeyCode.None    }, // P6（预留手柄/触屏）
    };

    private float[] leftTimers  = new float[6];
    private float[] rightTimers = new float[6];

    // TetrisGameController.StartGame() 通过 enabled = true 来激活此脚本
    void Awake() { enabled = false; }

    void Update()
    {
        if (!isHost) return;

        var ctrl = TetrisGameController.Instance;
        if (ctrl == null) return;

        for (int i = 0; i < ctrl.columns.Count; i++)
        {
            TetrisPiece piece = ctrl.columns[i].currentPiece;
            if (piece == null) continue;
            PollPlayer(i, piece);
        }
    }

    void PollPlayer(int playerIdx, TetrisPiece piece)
    {
        if (playerIdx >= KeyBindings.Length) return;
        KeyCode[] k = KeyBindings[playerIdx];

        HandleRepeatKey(k[0], piece.MoveLeft,  ref leftTimers[playerIdx]);
        HandleRepeatKey(k[1], piece.MoveRight, ref rightTimers[playerIdx]);
        if (k[2] != KeyCode.None && Input.GetKeyDown(k[2])) piece.RotateCW();
        if (k[3] != KeyCode.None) piece.SetSoftDrop(Input.GetKey(k[3]));
        if (k[4] != KeyCode.None && Input.GetKeyDown(k[4])) piece.HardDrop();
    }

    void HandleRepeatKey(KeyCode key, System.Action action, ref float timer)
    {
        if (key == KeyCode.None) return;

        if (Input.GetKeyDown(key))
        {
            action();
            timer = moveRepeatInitialDelay;
        }
        else if (Input.GetKey(key))
        {
            timer -= Time.deltaTime;
            if (timer <= 0f) { action(); timer = moveRepeatInterval; }
        }
        else
        {
            timer = 0f; // 松开后重置
        }
    }

    // ── 联网阶段：以下方法供真实网络层调用（模拟阶段不需要）─────────────────

    [System.Serializable]
    public struct PhysicsStatePacket
    {
        public int   blockInstanceId;
        public float posX, posY, rotZ;
        public float velX, velY, angVelZ;
    }

    /// <summary>Host 广播：遍历所有列的已落地刚体，打包状态（联网阶段实现）</summary>
    public void BroadcastPhysicsState()
    {
        // TODO 联网阶段：
        // var ctrl = TetrisGameController.Instance;
        // foreach (var col in ctrl.columns)
        //     foreach (var rb in col.GetSettledBlocks())
        //         if (rb != null) SendPacket(BuildPacket(rb));
    }

    /// <summary>Client 接收：插值追赶收到的刚体状态（联网阶段实现）</summary>
    public void ReceivePhysicsState(PhysicsStatePacket packet)
    {
        // TODO 联网阶段：
        // Rigidbody rb = FindBlockById(packet.blockInstanceId);
        // rb.MovePosition(Vector3.Lerp(rb.position, new Vector3(packet.posX, packet.posY, rb.position.z), lerpFactor));
        // rb.MoveRotation(Quaternion.Slerp(rb.rotation, Quaternion.Euler(0,0,packet.rotZ), lerpFactor));
    }
}
