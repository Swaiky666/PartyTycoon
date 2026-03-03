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
- **网络（当前）**：本地模拟中间层（`NetworkManager.cs`）
- **网络（规划）**：微信 MGOBE 或自建服务器 SDK

---

## 项目目录结构

```
Assets/
├── Scripts/
│   ├── Network/
│   │   └── NetworkManager.cs        ← 网络中间层（唯一联网切换点）
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
│   │   └── MinigameController.cs    ← 小游戏场景返回主场景
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

`Assets/Scripts/Network/NetworkManager.cs` 是**唯一的联网切换点**。

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
- [x] 模拟联网中间层（NetworkManager，骰子请求/广播已拆分）

---

## 待开发功能（按阶段优先级）

### 当前重点：完善单机核心规则 + 模拟联网

- [ ] **EndTurn 网络化**：`SendEndTurnRequest()` → 广播切换回合
- [ ] **金钱/房产同步**：`SyncMoneyChange()`、`SyncPropertyOwner()`
- [ ] **税收系统**：`TurnCounter` 每 3 回合增加全局 `TaxMultiplier`（含 UI 滚动提示动效）
- [ ] **伤害系统**：受伤扣 `资金 × 20%`（暂定，待测试）
- [ ] **淘汰机制**：资金 ≤ 0 时触发淘汰，清空名下所有房产
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

### 第四阶段：小游戏 + 帧同步

- [ ] Addressables 异步预加载（最后一名玩家行动时静默下载）
- [ ] SceneLoader + 四人 Sync Point（全部就绪后统一开始）
- [ ] Lockstep 帧同步控制器（66ms/帧收集输入 → 服务端广播 → 本地推算）
- [ ] 小游戏结算 → 服务端下发排名 → 大地图奖励/下轮顺序

小游戏列表（各自独立 Addressables Label，`Game_` 前缀）：

| Label | 小游戏 |
|-------|--------|
| `Game_Tetris` | 俄罗斯方块 |
| `Game_LianLianKan` | 连连看 |
| `Game_FruitNinja` | 水果忍者 |
| `Game_DoodleJump` | 涂鸦跳跳 |
| `Game_Runner` | 横板跑酷 |
| `Game_Rhythm` | 节奏音游 |
| `Game_BubbleHall` | 泡泡堂 |

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
| 小游戏 Label | `Game_` 前缀 | `Game_Tetris` |
| 消息协议 | `CMD_` 前缀全大写 | `CMD_ROLL`, `CMD_BUY` |

### 绝对不能动的东西

- `GameDataManager`：必须保持 `DontDestroyOnLoad`，是跨场景数据载体
- `NetworkManager`：必须保持 `DontDestroyOnLoad`，是联网状态持有者
- `AudioManager`：必须保持 `DontDestroyOnLoad`，切场景不断音乐
- `GridNode.connections[]`：固定长度 4（上下左右），不要改成 List
- `GridNode.slotPoints[]`：固定长度 6（最多6玩家站位）
- 道具逻辑必须在**投骰子之前**可用，`TurnManager` 投骰后需锁定道具按钮

### 场景说明

| 场景名 | 用途 |
|--------|------|
| `MainBoardScene` | 主棋盘游戏场景 |
| `MinigameScene` | 小游戏通用入口（按抽到的游戏动态加载对应 Label） |

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

**正在实现：模拟联网回合同步**

核心文件：
- `Assets/Scripts/Network/NetworkManager.cs`：模拟服务端，目前处理骰子请求和广播
- `Assets/Scripts/TurnManager.cs`：已拆分为「发请求 `SendRollDiceRequest`」和「收广播执行 `ExecuteNetRollDice`」两个阶段

**下一步（按顺序）**：
1. `NetworkManager` 新增 `SendEndTurnRequest()` 方法
2. `GridEventManager` 中金钱变动接入 `NetworkManager.SyncMoneyChange()`
3. 实现税收 `TaxMultiplier` 系统（`TurnManager` 内每 3 回合触发，含 UI 提示）
4. 实现淘汰判断（`PlayerController.ChangeMoney` 检测资金 ≤ 0 时触发）

---

## 常见问题

**Q：为什么地块点击不响应？**  
A：`GridClicker` 和 `BoxCollider` 是动态挂载的，只在卡牌范围选择模式下存在。`ClearHighlight()` 会销毁它们。平时地块不具备射线检测能力，这是故意设计的（性能优化）。

**Q：为什么有时骰子动画跳过？**  
A：检查 `DiceAnimator.diceModel` 是否为 null，以及 `fastRollDuration` 是否被设为 0。

**Q：存档读取后位置不对？**  
A：`LoadGameState` 依赖 `GridDatabase.RefreshCache()`，确保加载场景后所有 GridNode 全部激活后再调用。

**Q：新增联网功能的正确步骤？**  
A：① `NetworkManager` 加 `SendXxxRequest()` → ② 加私有协程模拟服务端处理 → ③ 回调对应 Manager 的 `ExecuteNetXxx()` → ④ 业务逻辑触发点改调 `NetworkManager.Instance.SendXxxRequest()`。

**Q：大地图状态同步和小游戏帧同步有什么区别？**  
A：大地图是回合制，延迟容忍高，服务端直接广播最终状态结果。小游戏是实时对抗，改用 Lockstep：每 66ms 收集本地输入发给服务端，服务端广播所有人的输入后，每个客户端用相同输入本地推算结果，保证帧一致性，不直接同步位置。