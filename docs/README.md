# DevouringBeast — 项目入口与会话交接

> 最后更新：2026-07-24
>
> Unity：2022.3.62f3c1，URP 2D
>
> 目标平台：Android 横屏

本文是新会话的首要入口，只记录会影响后续开发的项目逻辑、关键决策、易错点、完成状态和待办。普通 Bug 修复过程不记录在这里；代码、场景和 Unity 运行结果始终是最终事实来源。

## 1. 文档导航

| 文档 | 用途 |
| --- | --- |
| [readme.md](readme.md) | 项目入口、核心规则与当前交接摘要 |
| [SESSION_CONTEXT.md](SESSION_CONTEXT.md) | 更详细的系统实现、资源映射和历史验证信息；部分旧结论可能过时，使用前应与本文及代码核对 |
| [DESIGN.md](DESIGN.md) | 原始游戏设计文档；存在编码和部分规则过时问题，不能直接视为当前实现 |

## 2. 项目介绍

`DevouringBeast` 是一款 2D 俯视角 Roguelike 动作游戏，核心循环是：

```text
移动躲避 -> 吸力伤害/牵引 -> 吸入口中 -> 吐出能量球或吞噬 -> 获得升级 -> 应对更高波次
```

- 吸入：存活敌人只能受到吸力伤害，死亡敌人和可吸入物才能进入口中。
- 吐出：口中物品转化为能量球，伤害与吐出质量、基础属性和肉鸽技能有关。
- 吞噬：消耗口中物品，累计质量与 `ItemTag`，触发肉鸽三选一。
- 波次：普通、精英和 Boss 敌人按波次生成；每十波为 Boss 波。
- Faith：神话系技能会改变玩家的基础操作和升级来源，是候选池规则的特殊分支。

场景流程为：

```text
LoadScene -> MenuScene -> GameScene
```

`GameManager` 负责正常游玩、肉鸽选择和 Game Over 等状态。暂停选择或阵亡时必须同时停止战斗逻辑与普通 SFX，但 BGM 保持播放。

## 3. 核心系统与关键决策

### 3.1 地图、边界与相机

- `GameScene` 使用 `ArenaGrid/Terrain` Tilemap，尺寸为 `56 x 32`，Grid 原点为 `(12, 24, 0)`。
- 实际活动边界由 `MapBounds` 管理：中心 `(40, 40)`，尺寸约 `53.333 x 32`，范围约 `(13.33, 24)` 到 `(66.67, 56)`。
- 玩家初始位置为地图中心 `(40, 40, 0)`。
- 空气墙由 `MapBounds` 运行时创建；玩家、敌人和环境物品必须使用同一边界，不应各自硬编码地图尺寸。
- 环境物品生成范围动态读取 `MapBounds.Min/Max`。
- 怪物使用 16 个固定 Spawn Point，它们已经按新边界分布在地图四周。以后修改地图尺寸时必须再次同步这些 Transform；它们不会自动跟随 `MapBounds`。
- Tilemap 只负责视觉，没有 TilemapCollider；`TilemapRenderer` 使用 Chunk 模式，Sorting Order 为 `-20`。
- 地图图集为 `Assets/Art/Tilesets/bg.png`，导入规范为 `128 PPU`、中心 Pivot、无 Mipmap。重建入口：`Tools/DevouringBeast/Rebuild Arena Tilemap`。
- 相机使用指数平滑跟随并按视口尺寸限制到地图内。当视口大于某个轴的地图范围时，该轴必须固定到地图中心，不能把反转的 Clamp 上下限直接传给 `Mathf.Clamp`，否则经过中心时会抖动。

### 3.2 吸入、尸体与吞噬

- `PlayerInhale` 使用预分配数组和 `Physics2D.OverlapCircleNonAlloc`，可吸入物 Collider 必须位于 Layer 6，并可找到 `InhaleableItem`。
- 存活敌人不能直接入口；吸力对其造成持续伤害。死亡敌人无需比较质量即可被牵引。
- 普通物品需要当前吸力严格大于物品质量才能移动。
- 尸体牵引使用独立速度倍率，约为普通物品的两倍，速度上限 `28`。
- 普通敌人死亡后保留 `20s`，最后阶段闪烁后回池。该值在 100 个敌人 Prefab 中有序列化覆盖，修改脚本默认值并不足以改变运行结果，必须同步 Prefab。
- 吸入口中的对象先进入 `SwallowContainer`；吸入未结束时不能吞噬，吐出与吞噬请求必须互斥。
- 血量掉落进入口中后，只有吞噬时才治疗；接触玩家不会自动治疗。

### 3.3 升级进度与肉鸽候选

- 前五次升级需求为 `35 / 50 / 65 / 80 / 100`，之后从 `110` 开始按 `1.1` 倍向上取整。
- 普通吞噬升级会累计各 `ItemTag` 的质量，占比最高的标签决定优先技能学派；无合法候选时使用 Normal 补位。
- 升级扣除当前需求但保留溢出质量，不能直接清空全部进度。
- 候选必须统一经过前置条件、最大等级、Faith 互斥和形态收益过滤，UI 不应自行复制规则。
- 通过击杀获得升级进度时，不使用 `ItemTag` 学派偏好，而是从所有当前合法候选中混合随机。

### 3.4 Faith 规则

Faith 技能彼此互斥，但不会普遍锁死所有非 Faith 学派。关键分支如下：

- 天使：吸入和吞噬操作替换为无消耗吐出；不能吸入物品；击杀获得升级进度。
- 天使后续仍可获得天使自身和非 Faith 技能，但排除吸力、吸入移动、延长吸入时间等无收益技能；不能出现其他 Faith。
- 当前选择天使时仍会清除既有非 Faith 技能，这是现有设计语义，若要改变必须同时调整技能描述和存档恢复逻辑。
- 恶魔：操作侧重持续吸力伤害，不能吸入物品；击杀获得升级进度。
- 恶魔候选排除吐出、能量球、毒、火、分裂和穿透等无收益技能，保留移动、吸力、吸入移动、延长吸入及恶魔自身。
- 天使或恶魔击杀敌人时不保留尸体；若本次按掉落概率应产生血量道具，则直接恢复玩家生命，不生成掉落物。
- 教皇和女巫仍使用各自已有规则；修改 Faith 公共逻辑时不能误伤这两个分支。

### 3.5 能量球与命中特效

- 能量球从对象池获取，每次发射必须重写完整快照，不能残留上一次的技能和伤害数据。
- 完整伤害为：`(基础伤害 + 吐出质量) * (1 + 额外伤害倍率) * 完整伤害倍率`。
- 分裂弹必须在碰撞目标后方生成，并忽略刚命中的原目标，避免立刻二次碰撞。
- 命中特效由 `EnergyBallHitVfxService` 统一池化播放，优先级不能依赖多个脚本各自判断：

```text
火系爆炸 + 任意毒系 -> poison_bomb
纯火系爆炸          -> bomb
致命毒素且无爆炸    -> poison_cloud
其他                -> 普通/火/毒/火毒粒子
```

- 毒雾使用 3–5 个粒子、Size/Color over Lifetime、噪声溶解和背景折射。背景扭曲依赖 Camera Opaque Texture，Android 真机仍需复核性能和强度。
- 特效资源集中在 `Resources/System/EnergyBallHitVfxCatalog.asset`；重建入口：`Tools/DevouringBeast/Rebuild Energy Ball Hit VFX`。

### 3.6 敌人、波次与死亡表现

- `WaveManager` 按来源 Prefab 池化敌人。回收和重新取出时必须重置血量、AI、Animator、Collider、状态效果、血条、图标、事件订阅和标准缩放。
- 跨波存活敌人保留当前生命，不重置生命值；只强化攻击、速度、范围、频率和吸入抗性。
- 普通/精英/Boss 固定质量为 `5 / 20 / 50`，跨波强化不改变质量。
- 玩家死亡只旋转视觉 Sprite `90°`，不能旋转玩家根对象，否则会改变 Rigidbody 和 Collider。
- 敌人倒地旋转与玩家死亡旋转是两套逻辑，不应共用角度。

## 4. 性能与架构注意点

已经采用的性能策略：

- 敌人、能量球、命中特效和大量环境物品均使用对象池。
- 吸力检测使用 NonAlloc API 和复用集合。
- Tilemap 使用 Chunk 渲染且不创建 TilemapCollider。
- `RogueSkillManager.Active` 缓存活动实例，敌人死亡路径不再反复执行全场 Find。
- 高频组件引用应在 `Awake` 缓存，禁止在 `Update/FixedUpdate` 中使用 Find 或重复 `GetComponent`。

仍值得优化和验证：

- `BloodDrop` 仍使用低概率 `Instantiate/Destroy`，若高波次出现 GC 峰值，应改为对象池。
- 对象池必须持续检查事件重复订阅、协程未停止和缩放未恢复问题。
- 不凭编辑器主观感受判断卡顿；需在目标 Android 设备使用 Unity Profiler、Memory Profiler 和 Frame Debugger，分别确认 CPU、GC、GPU 与批次数。
- 地图扩大后不应简单提高环境物品目标数量；先以空间密度和真机帧率决定容量。

## 5. 主要代码入口

| 模块 | 入口 |
| --- | --- |
| 游戏状态 | `Assets/_Project/Scripts/Managers/GameManager.cs` |
| 波次、生成点、敌人池 | `Assets/_Project/Scripts/Managers/WaveManager.cs` |
| 地图边界与相机 | `Assets/_Project/Scripts/Core/MapBounds.cs`、`CameraFollow.cs` |
| 地图构建器 | `Assets/Editor/ArenaTilemapBuilder.cs` |
| 玩家移动与生命 | `Assets/_Project/Scripts/Player/PlayerController.cs`、`PlayerHealth.cs` |
| 吸入、吐出、吞噬 | `Assets/_Project/Scripts/Player/PlayerInhale.cs`、`PlayerSpit.cs`、`Core/SwallowContainer.cs` |
| 肉鸽技能 | `Assets/_Project/Scripts/Skills/RogueSkillManager.cs`、`Core/RogueSkillCatalog.cs` |
| 能量球与特效 | `Assets/_Project/Scripts/Player/EnergyBall.cs`、`EnergyBallShotSnapshot.cs`、`EnergyBallHitVfxService.cs` |
| 敌人死亡与状态 | `Assets/_Project/Scripts/Enemy/EnemyBase.cs`、`EnemyAI.cs`、`EnemyStatusEffects.cs` |
| 环境物品与掉落 | `Assets/_Project/Scripts/Core/EnvironmentItemSpawner.cs`、`BloodDrop.cs` |
| 存档 | `Assets/_Project/Scripts/Save/SaveGameService.cs` |
| 编辑器资源构建 | `Assets/Editor/RogueSystemBuilder.cs`、`EnergyBallHitVfxBuilder.cs` |

## 6. 已完成部分

- Load、Menu、Game 三场景流程，存档选择、删除、继续和重新开始。
- 移动、吸入、吐出、吞噬以及触摸/鼠标输入。
- 波次系统、敌人对象池、跨波存活强化、攻击窗口和状态效果。
- 数据驱动肉鸽目录、三选一、前置/满级过滤及四种 Faith 基础规则。
- 女巫野兽形态、滚动动画、减伤、加速、接触伤害和音效。
- 能量球快照、爆炸、灼烧、毒、侵蚀、眩晕、减速、分裂和穿透。
- 七类池化命中特效，包括火爆、毒爆和致命毒雾。
- 心形生命 UI、血量掉落、环境物品池、动态操作按钮和音频系统。
- 扩大后的多材质 Tilemap、同步空气墙/怪物生成点/环境生成范围、中心出生点和平滑相机限制。
- 前期升级曲线、20 秒尸体、更快尸体吸入和玩家倒地表现。
- Angel/Demon 击杀进度、直接治疗、尸体即时回收和收益型候选过滤。

## 7. 待办事项

按优先级建议：

1. 使用全新临时存档分别完成 Angel、Demon、Pope、Witch 的多次升级回归；验证候选池、操作按钮、击杀进度和存档恢复，不能覆盖用户存档。
2. 长时间运行高波次，确认第一波敌人不会拖到第三波、对象池容量稳定、事件无重复订阅、尸体确实在 20 秒后回收。
3. 在 Android 真机采集 CPU/GPU/GC/内存数据，定位剩余卡顿；重点观察大量敌人 AI、状态 UI、粒子和血量掉落实例化。
4. 把 `BloodDrop` 改为对象池，并验证半心/整心概率、生命周期和直接治疗分支。
5. 对不同敌人 Animator 逐个目测校准攻击有效窗口。
6. 复核扩大地图后的环境物品密度、生成补足数量和长时间分布，不要沿用旧小地图的数量结论。
7. Android 真机复核毒雾折射、溶解边缘和粒子并发开销。
8. 统一整理 `DESIGN.md` 编码，并删除与当前实现冲突的旧规则。

## 8. 最近验证基线

2026-07-24 已确认：

- Unity MCP 与本地工程编译均为 `0 Error`；只有既有未使用字段及 MCP WebSocket 警告。
- 地图边界为约 `(13.33, 24)` 到 `(66.67, 56)`，Tilemap 为 `56 x 32`，玩家位于 `(40, 40)`。
- 16 个怪物生成点全部位于新边界内，四面空气墙按新边界创建。
- 100 个敌人 Prefab 的 `corpseDuration` 均为 `20`。
- 升级需求回读为 `35, 50, 65, 80, 100, 110, 121`。
- 玩家死亡时视觉 Sprite 旋转为 `90°`。
- Angel 击杀升级池包含 Angel 和有效非 Faith 技能，不含吸入类技能；Demon 击杀升级池只保留移动、吸力、吸入强化和 Demon 自身。
- 地图、死亡表现和 Faith 候选已通过 Unity 编辑器内运行/反射检查；仍需按第 7 节进行完整长局与真机回归。

## 9. 下次会话加载与维护规则

1. 先完整读取本文件，再按任务读取对应核心脚本。
2. 查看 `git status --short`；当前工作区包含大量既有未提交修改和新资源，禁止 `git reset --hard`、`git checkout --` 或覆盖无关文件。
3. 场景、Prefab、ScriptableObject 的序列化值可能覆盖脚本默认值，修改规则时必须同时检查资产。
4. 使用 Unity MCP 修改场景和资源，脚本修改后等待域重载并检查 Console。
5. 高风险玩法修改至少执行一次 Play Mode 回归；性能结论必须来自 Profiler 或真机数据。
6. 更新本文时把信息融入对应章节，删除过时结论，不在末尾追加会话流水账。

---

_项目：DevouringBeast_
