# CLAUDE.md — 微信派对小游戏项目开发指南

> 放置位置：项目根目录（与 Assets/ ProjectSettings/ 同级）
> Claude Code 启动时自动读取此文件

---

## 项目概述

**平台**：微信小程序  
**游戏类型**：派对向联机桌游 + 回合制小游戏  
**联机方式**：线上匹配 / 好友房间  
**目标单局时长**：30–60 分钟（通过控制回合数或比资金大小来控制总时间）  
**商业模式**：看广告（玩家破产时"看广告复活"或"领救济金"）  
**目标用户**：微信生态用户、轻度玩家、中老年用户、派对/碎片时间玩家  
**灵感来源**：大富翁10 + Pummel Party 机制结合

### 核心玩法

玩家在大富翁逻辑下购买房产，其他玩家**停下时**（路过不收费）触发收费。手头资金为 0 时被淘汰，名下所有房产清空。受到伤害扣除 `资金 × 20%`（暂定，待测试调整）。

每轮所有玩家行动完毕后，进入 Pummel Party 式联机小游戏，根据小游戏排名决定奖励和下一轮投骰顺序。拥有道具后，玩家可在**投骰子之前**使用道具，投完骰子后不可使用。

**税收机制**：为控制游戏时长，每次购买资产和被收费时收取百分比税，每 3 回合税率上涨（数值待测试）。

---

## 技术栈

- **引擎**：Unity（微信小程序导出）
- **语言**：C#
- **UI**：Unity uGUI + TextMeshPro
- **动画**：DOTween
- **网络架构**：
  - 大地图：状态同步（State Sync，服务端权威判断）
  - 小游戏：帧同步（Frame Sync / Lockstep，66ms/帧，保证操作手感）
- **资源管理**：Addressables（按需远程下载，常驻资源除外）
- **网络（当前）**：本地模拟中间层（`GameNetworkManager.cs`）
- **网络（规划）**：微信 MGOBE 或自建服务器 SDK

---

## 项目目录结构

```
Assets/
├── Scripts/
│   ├── Network/
│   │   └── GameNetworkManager.cs    ← 游戏网络中间层（唯一联网切换点）
│   ├── CardSystem/
│   │   ├── CardBase.cs              ← 卡牌基类 ScriptableObject
│   │   ├── CardRangeFinder.cs       ← 卡牌范围高亮 + 点击检测
│   │   ├── CardUIController.cs      ← 手牌 UI（滑动翻页）
│   │   ├── CardDragHandler.cs       ← 卡牌拖拽/点击事件
│   │   ├── GridClicker.cs           ← 动态挂载的地块点击器
│   │   ├── ShopManager.cs           ← 商店 UI 与购买逻辑
│   │   └── Card/
│   │       ├── BarricadeCard.cs     ← 路障卡（已实现）
│   │       └── FreezeCard.cs        ← 冰冻卡（已实现）
│   ├── MiniGames/
│   │   └── MinigameController.cs    ← 小游戏场景返回主场景（旧测试用）
│   ├── BlockStack/                  ← Minigame_BlockStack 专用脚本
│   │   ├── TetrisGameController.cs  ← 场景主控、状态机、名次结算、返回主场景
│   │   ├── TetrisPlayerColumn.cs    ← 单玩家列区域（墙/地板/高度检测/spawn）
│   │   ├── TetrisPiece.cs           ← 当前活动方块（输入控制+落地物理切换）
│   │   └── TetrisNetworkSync.cs     ← 状态同步（Host 广播刚体状态，客户端插值）
│   ├── TurnManager.cs               ← 回合管理（已整合网络请求层）
│   ├── PlayerController.cs          ← 玩家移动、金钱、卡牌、冰冻状态
│   ├── GridNode.cs                  ← 地块节点（类型/归属/路径连接）
│   ├── GridDatabase.cs              ← 地块 ID 数据库（ScriptableObject）
│   ├── GridEventManager.cs          ← 落地事件（购地/租金/商店/银行等）
│   ├── GameDataManager.cs           ← 游戏状态存档/读档/场景切换
│   ├── GameStartManager.cs          ← 开局流程（掷骰决定顺序）
│   ├── CameraController.cs          ← 跟随相机（旋转/俯视/自由/震动）
│   ├── DiceAnimator.cs              ← 骰子动画（idle/滚动/锁定到点数）
│   ├── AudioManager.cs              ← BGM + SFX 统一管理
│   ├── UIManager.cs                 ← 全局 UI（状态文字/玩家信息/按钮）
│   ├── ArrowClicker.cs              ← 分叉路口方向箭头点击检测
│   └── SceneReturnTester.cs         ← 测试用：从小游戏返回主场景
└── Editor/
    └── GridAutoLinker.cs            ← 编辑器工具：地块自动排布/连接/站位
```

---

## 核心架构说明

### 回合流程

```
GameStartManager（掷骰决定顺序）
    ↓
TurnManager.BeginGame()
    ↓ 循环
TurnManager.StartTurn()
    → 玩家点击"投掷骰子"（投骰前可使用道具）
    → StartDiceRollRequest()
    → NetworkManager.SendRollDiceRequest(playerId)   ← 网络层入口
    → ExecuteNetRollDice(playerId, result)            ← 网络层出口（服务端广播回调）
    → ProcessTurnSequenceNet()：播动画 → 移动 → 地块事件
    → EndTurn() → 下一位玩家

所有玩家走完一轮
    → EnterMinigameFlow() → 保存状态 → 切换到小游戏场景
    → 小游戏（帧同步）→ 结算排名 → 下发奖励/道具/下轮顺序
    → MinigameController.BackToMainGame() → 加载主场景
    → GameStartManager 检测到存档 → LoadGameState() → BeginGameFromMinigame()
```

### 网络中间层设计（关键）

`Assets/Scripts/Network/GameNetworkManager.cs` 是**唯一的联网切换点**。

**当前（模拟阶段）**：
- `SendRollDiceRequest()` → 本地协程模拟延迟 → 直接回调 `TurnManager.ExecuteNetRollDice()`

**真联网阶段只需改这一个文件**：
- `SendRollDiceRequest()` → 发消息协议（如 `CMD_ROLL`）到服务端 → 服务端广播 → 所有客户端收到后调 `ExecuteNetRollDice()`
- `TurnManager` 及其他所有脚本**无需改动**

**消息协议命名规范**（待实现）：

| 命令 | 方向 | 用途 |
|------|------|------|
| `CMD_ROLL` | 客户端 → 服务端 → 广播 | 投骰子请求 |
| `CMD_MOVE` | 服务端广播 | 移动指令 |
| `CMD_BUY` | 客户端 → 服务端 → 广播 | 购地请求 |
| `CMD_END_TURN` | 客户端 → 服务端 → 广播 | 结束回合 |
| `CMD_MONEY` | 服务端广播 | 金钱变动同步 |
| `CMD_CARD_USE` | 客户端 → 服务端 → 广播 | 道具使用 |

### 大地图 vs 小游戏的同步策略

| 模式 | 同步方式 | 原因 |
|------|----------|------|
| 大地图（棋盘） | 状态同步（State Sync） | 回合制，延迟容忍度高，服务端权威判断结果 |
| 小游戏 | 帧同步（Lockstep，66ms/帧） | 即时对抗，需保证所有客户端操作手感完全一致 |

---

## 已完成功能（单机）

- [x] 棋盘地图大场景（GridNode 路径网络）
- [x] 多玩家回合制管理（TurnManager）
- [x] 骰子动画系统（DiceAnimator，含音效）
- [x] 玩家移动（含分叉路口箭头选择、路障停止、防回退）
- [x] 地块事件：购地、缴纳租金
- [x] 商店系统（ShopManager，随机上架5张卡牌）
- [x] 卡牌系统：手牌UI（滑动翻页）、范围高亮、路障卡、冰冻卡
- [x] 视角切换（跟随 / 自由俯视）
- [x] 存档/读档（GameDataManager，跨场景 DontDestroyOnLoad）
- [x] 小游戏场景切换流程（保存 → 切场景 → 恢复）
- [x] 相机震动（DOTween）
- [x] 音频系统（BGM + SFX 分离）
- [x] 编辑器工具（地块自动排布/路径连接/站位管理）
- [x] 模拟联网中间层（GameNetworkManager，骰子请求/广播已拆分）
- [x] 回合结束网络化（SendEndTurnRequest / ExecuteNetEndTurn）
- [x] 税收系统（每 N 轮上涨 taxRateStep，UIManager 渐显通知）
- [x] 玩家淘汰机制（资金 ≤ 0 → 清空房产 → 移出回合序列 → 检测游戏结束）
- [x] 小游戏触发流程（每轮所有玩家行动完 → EnterMinigameFlow → 保存状态 → 切换场景）

---

## 待开发功能（按阶段优先级）

### 当前重点：Minigame_BlockStack 实现 + 特殊格子事件

- [ ] **特殊格子事件**：
  - [ ] 银行：路过得钱
  - [ ] 医院：生命为 0 强制移动并停留一回合
  - [ ] 监狱：被逮捕卡/警察触发，停留一回合
  - [ ] 宝藏：随机获得一张道具卡
  - [ ] 公园：无效果
  - [ ] 陷阱：扣血
  - [ ] 火车站：按玩家名下火车站数量累计收费，被铲除后变无主火车站（非空地）

### 第二阶段：联网底层与消息分发

- [ ] 导入微信小游戏 Unity 转换面板
- [ ] 初始化 MGOBE（或自建服务器 SDK）：创建/加入房间、全员准备
- [ ] 实现消息分发器（Dispatcher）：收到广播后调用对应 `Execute` 方法
- [ ] 服务器权威计时：每人 15 秒操作时间，超时自动弃权

### 第三阶段：完整道具系统

所有道具均需含网络同步：

| 道具 | 效果 | 实现状态 |
|------|------|----------|
| 冰冻卡 | 目标失去行动一回合，若有房屋额外收费 | ✅ 已实现 |
| 路障卡 | 放置路障，强制途经玩家停下 | ✅ 已实现 |
| 乌龟卡 | 目标接下来 3 回合只能走 1 格（修改 `MoveModifier`） | ⬜ |
| 倒退卡 | 目标移动方向反转 | ⬜ |
| 跑车卡 | 自身走 10 格，途经玩家受到伤害 | ⬜ |
| 炸弹卡 | 埋下陷阱，任何玩家（含自身）踩到满血秒杀送医院，免该格其他效果 | ⬜ |
| 涨价卡 | 接下来 3 回合自身房产收费翻倍 | ⬜ |
| 逮捕卡 | 强制目标进入监狱 | ⬜ |
| 免狱卡 | 避免自己一次进监狱 | ⬜ |
| 拆除卡 | 铲除自己当前格房产 | ⬜ |
| 磁铁卡 | 吸取范围内目标 20% 资金和道具（暂定，待测试） | ⬜ |

### 第四阶段：小游戏实现

#### 场景命名约定

小游戏场景统一命名为 `Minigame_[GameName]`，每个小游戏独立场景。`GameDataManager.SwitchToRandomMinigame()` 维护一个场景名列表随机选取。

| 场景名 | 小游戏 | 状态 |
|--------|--------|------|
| `Minigame_BlockStack` | 物理方块竞速堆塔 | 设计完成，待实现 |
| `Minigame_LianLianKan` | 连连看 | 规划中 |
| `Minigame_FruitNinja` | 水果忍者 | 规划中 |
| `Minigame_Runner` | 横板跑酷 | 规划中 |
| `Minigame_Rhythm` | 节奏音游 | 规划中 |
| `Minigame_BubbleHall` | 泡泡堂 | 规划中 |

#### Minigame_BlockStack 设计规格

**玩法**：最多6名玩家同时操控方块往自己的竖列里堆叠，最先堆到高度线的顺序决定本局名次，名次影响下一轮大地图的投骰顺序。

**物理约束**（3D刚体，限制在2D平面）：
```
Rigidbody 约束:
  Freeze Position: Z
  Freeze Rotation: X, Y
  允许: Position X/Y, Rotation Z（左右倾倒）
```

**场景布局**：
- 6列并排，列宽5 unit，中心间距10 unit
- X坐标：-25 / -15 / -5 / +5 / +15 / +25
- 高度线 Y = 18，Spawn点 Y = 19（列顶中心）
- 相机从 Z+ 正面看，位置约 Z=45，FOV=60°

**方块**：标准7种Tetromino（I/O/T/S/Z/L/J），1×1×1 unit 单元，PhysicsMaterial（摩擦0.6/0.8，弹力0.05）

**控制键位**（本地6玩家）：

| 动作 | P1 | P2 | P3 | P4 | P5 | P6 |
|------|----|----|----|----|----|----|
| 左移 | A | ← | J | Num4 | G | - |
| 右移 | D | → | L | Num6 | H | - |
| 旋转 | W | ↑ | I | Num8 | Y | - |
| 软落 | S | ↓ | K | Num5 | B | - |
| 硬落 | Space | Enter | U | Num0 | N | - |

**同步方案**：状态同步（Host 模拟物理，每 FixedUpdate 广播所有已落地刚体的 pos.x/y, rot.z, vel.x/y, angVel.z，其他客户端插值追赶）

**结算**：完成顺序写入 `GameDataManager`，返回主场景后 `GameStartManager.ToTurnManagerFromLoad()` 按名次调用 `BeginGameFromMinigame(survivors)`

**脚本详细设计**：

---

##### `TetrisGameController.cs` — 场景主控单例

```
路径: Scripts/BlockStack/TetrisGameController.cs
```

关键字段：
```csharp
public static TetrisGameController Instance;
public List<TetrisPlayerColumn> columns;   // Inspector 拖入6个列
public float countdownDuration = 3f;       // 开始前倒计时
public float gameTimeLimit = 180f;         // 总时限（秒），超时强制结算
private List<int> finishOrder;             // 完成顺序，按 playerId 记录
private enum GameState { Countdown, Playing, Finished }
private GameState state;
```

关键方法：
```csharp
void StartCountdown()                         // 场景加载后调用，播放倒计时
void StartGame()                              // 倒计时结束后，通知所有 Column 开始 Spawn
public void OnPlayerFinished(int playerId)    // 由 TetrisPlayerColumn 在高度达标时回调
void OnTimeUp()                               // 超时强制结算剩余名次
void FinishGame()                             // 全员完成 or 超时 → 结算 → 延迟2秒返回
void BackToMainGame()                         // 写入 GameDataManager → 加载 MainGameScene
```

结算逻辑：`finishOrder` 写入 `GameDataManager.savedMinigameRanking`（新增字段 `List<int>`），`GameStartManager.ToTurnManagerFromLoad()` 读取该列表作为 `BeginGameFromMinigame` 的玩家顺序。

---

##### `TetrisPlayerColumn.cs` — 单玩家列区域

```
路径: Scripts/BlockStack/TetrisPlayerColumn.cs
```

关键字段：
```csharp
public int playerId;                      // 对应大地图的 playerId
public Transform spawnPoint;             // 新方块生成位置（列顶中心）
public Transform finishLineTransform;    // 高度线位置（Y=18）
public GameObject blockUnitPrefab;       // 1x1x1 单元预制体（无 Rigidbody）
public TextMeshPro playerLabel;          // 显示玩家编号/状态
private TetrisPiece currentPiece;        // 当前活动方块
private List<Rigidbody> settledBlocks;   // 所有已落地刚体（供 NetworkSync 遍历）
private bool isFinished;
```

关键方法：
```csharp
public void Init(int pid)                     // 设置 playerId，初始化 UI 标签
public void SpawnNextPiece()                  // 随机选形状 → 实例化 TetrisPiece → 设置 currentPiece
public void OnPieceSettled(TetrisPiece piece) // 接收 TetrisPiece 的落地回调 → 注册刚体 → 检测高度 → SpawnNextPiece
private bool CheckFinishCondition()           // 遍历 settledBlocks，任意块 Y >= finishLineTransform.Y
public void RegisterSettledBlock(Rigidbody rb)// 将落地刚体加入 settledBlocks 列表
public List<Rigidbody> GetSettledBlocks()     // 供 TetrisNetworkSync 遍历广播
```

形状随机：`TetrominoType type = (TetrominoType)Random.Range(0, 7)`，传入 TetrisPiece.Init。

---

##### `TetrisPiece.cs` — 当前活动方块

```
路径: Scripts/BlockStack/TetrisPiece.cs
```

关键字段：
```csharp
public enum TetrominoType { I, O, T, S, Z, L, J }

// 7种形状的子块偏移（相对于中心，XY平面）
static readonly Dictionary<TetrominoType, Vector2Int[]> Shapes;

public TetrisPlayerColumn ownerColumn;
public int playerId;                      // 控制此方块的玩家
public float fallSpeed = 2f;             // 自然下落速度（units/秒）
public float softDropMultiplier = 5f;
private List<Transform> blockUnits;      // 子块 Transform 列表
private bool isSettled = false;
private Rigidbody pieceRb;               // 整体 Kinematic Rigidbody（未落地时）
```

关键方法：
```csharp
public void Init(TetrominoType type, TetrisPlayerColumn column, int pid)
    // 根据 Shapes[type] 实例化子块，定位到 spawnPoint，设为 Kinematic

void Update()
    // 自然下落：transform.position += Vector3.down * fallSpeed * Time.deltaTime
    // 检测落地：任意子块触碰到其他 Collider → 调用 Settle()

public void MoveLeft()   // transform.position += Vector3.left * 1f（整块移动）
public void MoveRight()  // transform.position += Vector3.right * 1f
public void RotateCW()   // transform.Rotate(0, 0, -90)（绕Z轴顺时针）
public void SoftDrop()   // fallSpeed 临时 *= softDropMultiplier
public void HardDrop()   // Raycast 向下找最低合法位置 → 瞬移 → Settle()

void Settle()
    // isSettled = true → 禁用整体 Rigidbody
    // 遍历所有 blockUnits：Detach from parent → AddComponent<Rigidbody>()
    //   → 设置 constraints(FreezePositionZ | FreezeRotationX | FreezeRotationY)
    //   → ownerColumn.RegisterSettledBlock(rb)
    // → ownerColumn.OnPieceSettled(this)
```

输入由 `TetrisNetworkSync` 轮询键盘后调用对应方法，`TetrisPiece` 本身不直接读取 Input。

---

##### `TetrisNetworkSync.cs` — 输入收集 + 物理状态同步

```
路径: Scripts/BlockStack/TetrisNetworkSync.cs
```

关键字段：
```csharp
public bool isHost = true;               // 模拟阶段默认 true（单机），联网后由房间角色决定
public float syncInterval = 0.05f;       // 广播间隔（约 20Hz）
private float syncTimer;

// 各玩家的键位绑定
static readonly KeyCode[][] KeyBindings = {
    new[]{ KeyCode.A, KeyCode.D, KeyCode.W, KeyCode.S, KeyCode.Space },    // P1
    new[]{ KeyCode.LeftArrow, KeyCode.RightArrow, KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.Return }, // P2
    new[]{ KeyCode.J, KeyCode.L, KeyCode.I, KeyCode.K, KeyCode.U },        // P3
    new[]{ KeyCode.Keypad4, KeyCode.Keypad6, KeyCode.Keypad8, KeyCode.Keypad5, KeyCode.Keypad0 }, // P4
    new[]{ KeyCode.G, KeyCode.H, KeyCode.Y, KeyCode.B, KeyCode.N },        // P5
    // P6 暂缺（6人时需外接手柄或触屏）
};
// 顺序: [左, 右, 旋转CW, 软落, 硬落]
```

关键方法：
```csharp
void Update()
    // 遍历 KeyBindings → 检测按键 → 调用对应 column.currentPiece 的方法

void FixedUpdate()
    // if (isHost && syncTimer >= syncInterval)
    //   → 遍历所有 column.GetSettledBlocks()
    //   → 收集 (instanceID, pos.x, pos.y, rot.z, vel.x, vel.y, angVel.z)
    //   → BroadcastPhysicsState(data)（模拟阶段：空实现）

public void ReceivePhysicsState(PhysicsStatePacket packet)
    // 非 Host 客户端收到广播后：找到对应 Rigidbody → 插值位置/旋转

[System.Serializable]
public struct PhysicsStatePacket {
    public int blockInstanceId;
    public float posX, posY, rotZ;
    public float velX, velY, angVelZ;
}
```

**`GameDataManager` 需新增字段**：
```csharp
[Header("小游戏结算")]
public List<int> minigameRanking = new List<int>(); // 按名次存 playerId，由 TetrisGameController.BackToMainGame() 写入
public List<string> minigameScenes = new List<string> { "Minigame_BlockStack" }; // SwitchToRandomMinigame 从此列表随机
```

**`SwitchToRandomMinigame()` 修改**：
```csharp
// 改为从 minigameScenes 随机选取，而不是硬编码 "MinigameScene"
string scene = minigameScenes[Random.Range(0, minigameScenes.Count)];
SceneManager.LoadScene(scene);
```

**`GameStartManager.ToTurnManagerFromLoad()` 读取排名**：
```csharp
// 若 minigameRanking 不为空，按排名顺序重排 survivors 列表
if (GameDataManager.Instance.minigameRanking.Count > 0)
    survivors = survivors.OrderBy(p => GameDataManager.Instance.minigameRanking.IndexOf(p.playerId)).ToList();
GameDataManager.Instance.minigameRanking.Clear();
```

- [ ] 创建 `Minigame_BlockStack` 场景及 PlayerColumn Prefab（美术/场景搭建）
- [ ] 实现 `TetrisGameController`、`TetrisPlayerColumn`、`TetrisPiece`、`TetrisNetworkSync`
- [ ] 更新 `GameDataManager`（新增字段 + 修改 `SwitchToRandomMinigame`）
- [ ] 更新 `GameStartManager.ToTurnManagerFromLoad()` 读取 `minigameRanking`
- [ ] Addressables 异步预加载（最后一名玩家行动时静默下载）

### 第五阶段：特殊 NPC

- [ ] **警察**：每回合朝最近玩家移动，回合结束时距离过近则逮捕入狱
- [ ] **游商**（后续）：固定路线移动，回合结束时靠近的玩家触发商店

### 后续增加内容

- [ ] 房屋升级机制（再次踩到自己地块可花钱升级，提高租金）
- [ ] 语音系统（微信语音优先）
- [ ] AI 系统（与匹配机制二选一，暂定匹配方向）
- [ ] 外观/皮肤系统（运营后酌情添加）
- [ ] 广告接入（微信视频广告，破产时"看广告复活"）

---

## Addressables 资源分类规划

### UI 资源

| Label | 内容 | 加载时机 |
|-------|------|----------|
| `UI_Essential` | 加载条、确认/取消按钮、通用字体、金币图标 | 首包常驻，永不释放 |
| `UI_Lobby` | 登录界面、好友房间列表、匹配等待、角色选择 | 匹配阶段，成功后 Release |
| `UI_InGame_Board` | 骰子UI、税率提示、玩家资产看板、倒计时、伤害数字 | 大地图对局期间 |
| `UI_InGame_Minigame` | 小游戏排名结算框、比分板 | 小游戏阶段 |

### 大地图资源

| Label | 内容 |
|-------|------|
| `Map_Static_Environment` | 棋盘底盘、背景草地/海洋、路灯、装饰性建筑 |
| `Map_Grids_Logic` | 空地、医院、商店、陷阱格 Prefab |

### 实体资源

| Label | 内容 | 加载时机 |
|-------|------|----------|
| `Entity_Players` | 玩家角色模型、动画、特效 | 仅加载本局4个角色 |
| `Entity_Props` | 各道具卡的模型/贴图 | 玩家使用卡片时触发 |
| `Entity_NPC` | 警察、救护车、游商 | 触发特定事件时加载 |

### 音频特效

| Label | 内容 | 策略 |
|-------|------|------|
| `Audio_BGM_Board` | 大地图背景音乐 | 设为 Streaming |
| `Audio_SFX_Common` | 叮声、收钱声、投骰子声 | 整合进 UI_Essential |
| `VFX_Common` | 爆炸、升级金光、烟雾特效 | 全局加载，多处复用 |

> 挂载在 Prefab 上的声音不需要独立 Label，随 Prefab 一起打包即可。

---

## 重要规范

### 命名约定

| 类型 | 规范 | 示例 |
|------|------|------|
| 单例 | `public static XxxManager Instance` | `TurnManager.Instance` |
| 协程入口 | `Start` / `Begin` 前缀 | `StartTurn()`, `BeginGame()` |
| 网络请求 | `Send` + 动作名 | `SendRollDiceRequest()` |
| 网络广播回调 | `Execute` + Net + 动作名 | `ExecuteNetRollDice()` |
| 状态查询 | `Is` / `Has` / `Get` 前缀 | `IsHandFull()`, `HasBarricade()` |
| 小游戏场景名 | `Minigame_` 前缀 | `Minigame_BlockStack` |
| 消息协议 | `CMD_` 前缀全大写 | `CMD_ROLL`, `CMD_BUY` |

### 绝对不能动的东西

- `GameDataManager`：必须保持 `DontDestroyOnLoad`，是跨场景数据载体
- `GameNetworkManager`：必须保持 `DontDestroyOnLoad`，是联网状态持有者
- `AudioManager`：必须保持 `DontDestroyOnLoad`，切场景不断音乐
- `GridNode.connections[]`：固定长度 4（上下左右），不要改成 List
- `GridNode.slotPoints[]`：固定长度 6（最多6玩家站位）
- 道具逻辑必须在**投骰子之前**可用，`TurnManager` 投骰后需锁定道具按钮

### 场景说明

| 场景名 | 用途 |
|--------|------|
| `MainGameScene` | 主棋盘游戏场景 |
| `Minigame_[GameName]` | 小游戏命名约定，每个小游戏独立场景 |
| `Minigame_BlockStack` | 物理方块竞速堆塔小游戏（第一个实现） |
| `MinigameScene` | 旧测试场景，可保留测试用，后续删除 |

### 美术技术规范

| 类别 | 要求 |
|------|------|
| 模型面数 | 单地块 < 500 面；单角色 < 3000 面 |
| 贴图大小 | 最大 1024×1024，尽量 512×512 |
| 文件格式 | 模型用 `.fbx`，贴图用 `.png` 或 `.tga` |
| 动画骨架 | 角色动画尽量使用同一 Avatar 骨架 |
| 交付方式 | 所有资源必须做成 Prefab 后交付 |
| 材质优化 | 装饰物尽量合并纹理，减少材质球数量 |

### 音效技术规范

| 规范项 | 要求 |
|--------|------|
| 文件格式 | 短音效用 WAV，长音乐用 MP3 |
| 声道 | 单声道（Mono），省一半内存 |
| 采样率 | 22050Hz（微信端） |

---

## 当前开发焦点

**正在实现：Minigame_BlockStack 物理堆塔小游戏**

大地图核心回合流程已完成（联网模拟层、税收、淘汰、小游戏触发均已实现）。

**下一步（按顺序）**：
1. 用户在 Unity 中创建 `Minigame_BlockStack` 场景 + PlayerColumn 预制体 + BlockUnit 预制体
2. 实现 `TetrisPiece`（方块输入控制 + 落地物理切换）
3. 实现 `TetrisPlayerColumn`（列管理 + 高度检测）
4. 实现 `TetrisGameController`（游戏状态机 + 名次结算 + 返回主场景）
5. 实现 `TetrisNetworkSync`（Host 物理广播 + 客户端插值）
6. 更新 `GameDataManager.SwitchToRandomMinigame()` 改为加载 `Minigame_BlockStack`

---

## 常见问题

**Q：为什么地块点击不响应？**  
A：`GridClicker` 和 `BoxCollider` 是动态挂载的，只在卡牌范围选择模式下存在。`ClearHighlight()` 会销毁它们。平时地块不具备射线检测能力，这是故意设计的（性能优化）。

**Q：为什么有时骰子动画跳过？**  
A：检查 `DiceAnimator.diceModel` 是否为 null，以及 `fastRollDuration` 是否被设为 0。

**Q：存档读取后位置不对？**  
A：`LoadGameState` 依赖 `GridDatabase.RefreshCache()`，确保加载场景后所有 GridNode 全部激活后再调用。

**Q：新增联网功能的正确步骤？**  
A：① `GameNetworkManager` 加 `SendXxxRequest()` → ② 加私有协程模拟服务端处理 → ③ 回调对应 Manager 的 `ExecuteNetXxx()` → ④ 业务逻辑触发点改调 `GameNetworkManager.Instance.SendXxxRequest()`。

**Q：大地图状态同步和小游戏帧同步有什么区别？**  
A：大地图是回合制，延迟容忍高，服务端直接广播最终状态结果。小游戏是实时对抗，改用 Lockstep：每 66ms 收集本地输入发给服务端，服务端广播所有人的输入后，每个客户端用相同输入本地推算结果，保证帧一致性，不直接同步位置。