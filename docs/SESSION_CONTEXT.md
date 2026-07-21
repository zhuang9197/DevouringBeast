# DevouringBeast 项目会话上下文

> 最后更新：2026-07-21  
> Unity：2022.3.62f3c1  
> 项目目录：`D:\WorkSpace\Unity\DevouringBeast`

## 文档用途与维护规则

本文件用于跨 Codex/AI 会话传递当前项目的有效上下文。新会话开始时，应优先读取本文件，再结合代码、Unity 场景和 Console 确认实际状态。

每次修改本文件时必须遵守：

1. 先完整分析已有内容，确认哪些信息仍然有效，禁止直接把新会话记录简单追加到末尾。
2. 只记录会影响后续开发决策的关键信息：架构、规则、资源映射、已完成能力、已知问题、验证结果和待办。
3. 已过时的信息应直接修正或删除，不同时保留互相冲突的新旧结论。
4. 不记录冗长调试过程、重复需求、临时测试对象、无关日志或大段源码。
5. “已完成”必须以当前代码或 Unity 实际验证为依据；没有验证的内容标记为“待验证”。
6. 修改代码后同步更新相关章节以及“最近验证状态”，但不要把本文件当作完整变更日志。
7. 尊重现有工作区修改。当前工作区长期处于大量未提交修改状态，不要重置、覆盖或回滚无关文件。

## 1. 项目概况

`DevouringBeast` 是一款横屏 2D 俯视角 Roguelike 动作游戏。玩家核心操作为移动、吸入、吐出和吞噬：

- 吸力可以伤害或牵引目标。
- 可吸入目标进入玩家口中后，可以吐出形成能量球，也可以吞噬以获得升级进度。
- 吞噬物品的 `ItemTag` 占比会影响升级时优先刷新的肉鸽技能学派。
- 能量球在发射时保存玩家伤害、速度和已拥有肉鸽技能的数据快照，命中后按快照结算。
- 游戏按波次推进，每十波为 Boss 波。

目标平台为 Android，使用 URP 2D。输入同时支持移动端触摸和编辑器鼠标测试。

## 2. 场景与游戏流程

当前场景流程：

```text
LoadScene -> MenuScene -> GameScene
```

- `LoadScene`：启动场景，播放 `normal` BGM；显示加载背景和 `load_anim` 循环动画，加载完成后显示 `load_done`，点击任意位置进入主页。
- `MenuScene`：主页及存档入口，播放 `normal` BGM；支持新游戏、继续游戏、选项、存档选择与删除确认。
- `GameScene`：实际游玩场景，由原 `SampleScene` 演变而来，播放战斗音乐。
- `SampleScene` 已删除，不应恢复为主场景。

Build Settings 应确保启动顺序为 `LoadScene`、`MenuScene`、`GameScene`。

## 3. 游戏状态

`GameManager` 负责核心状态切换。关键状态至少包括正常游玩、肉鸽选择和阵亡结算。

- 肉鸽三选一打开时暂停游戏，只保留 BGM，停止普通 SFX；选择后恢复。
- 玩家阵亡后进入明确的 Game Over 状态：玩家和敌人停止行动及攻击，普通音效停止，BGM 继续。
- 结算窗口支持返回主页和重新开始。
- 重新开始需要重置游戏流程并从头重播当前战斗 BGM。
- 不允许仅禁用按钮而仍让角色、怪物或波次继续运行。

## 4. 玩家、吸入和吞噬

### 4.1 吸入力学

- `PlayerInhale` 使用 `Physics2D.OverlapCircleAll` 和序列化的 `LayerMask` 进行第一层候选过滤；当前可吸入对象专用层为 `inhaleableLayer`（Layer 6，Mask 值 `64`）。
- LayerMask 作用于 Collider 所在的 GameObject。所有希望被吸力发现的掉落物、阵亡敌人或可吸入物体，其有效 Collider 必须位于 Layer 6，并能在同一对象或父级找到 `InhaleableItem`。
- 层级只决定“是否进入吸力检测”，不会直接决定能否入口。通过层级过滤后，还要依次满足前方锥形角度、吸力/质量与吸入阈值、入口距离等规则。
- 存活敌人不能直接吸入口中；吸力作用会伤害敌人，并给追逐移动一个轻微加速，而不是减速。
- 阵亡敌人和可吸入物品不会因为每帧吸力而叠加永久速度。
- 可吸入对象不会在任意距离瞬移入口中。
- 目标先根据吸力与质量/所需吸力的比例被牵引；比例越大，靠近速度越快。
- 只有进入配置的入口距离后才调用 `OnInhaled` 并加入 `SwallowContainer`。
- 修改 Layer、Collider、`PlayerInhale.inhaleableLayer` 或 Physics 2D Layer Collision Matrix 时，必须同时回归掉落物、阵亡敌人与普通可吸入物。

主要脚本：`PlayerInhale.cs`、`InhaleableItem.cs`、`SwallowContainer.cs`。

### 4.2 吞噬与升级

- `SwallowContainer` 分开维护口中物品和已经吞噬的累计质量。
- 吸入尚未结束时，即使已有物品进入口中也不能吞噬；只有 `PlayerInhale.IsInhaling == false` 且口中有物品时 `CanConsume` 才成立。
- 吸入/吐出主操作与吞噬操作使用按住状态互斥；一个操作尚未松开时，另一个操作请求必须被拒绝，防止同帧吞噬和吐出同一批物品。
- 吞噬时累计 `CurrentMass` 与各 `ItemTag` 质量，达到 `RequiredMass` 后触发肉鸽选择。
- 前三级升级需求分别为 `30`、`50`、`75`；之后按 `1.35` 倍向上取整递增，例如 `102`、`138`。
- 升级会扣除当前等级所需质量并保留溢出进度，再提高下一等级需求，保证每一级需求严格递增。
- UI 显示当前等级和升级进度。
- 吞噬按钮只有口中有东西时可用；不可用时应置灰。
- 当前项目没有被动随时间回血逻辑。回血只能来自明确治疗效果。

## 5. 能量球系统

能量球使用对象池，发射时必须为每一颗球重新写入玩家当前属性快照，禁止复用上一次发射的数据。

预制体主要结构：

```text
EnergyBall
├─ Sprite_Ring  -> ring
├─ Sprite_Star  -> star
├─ Sprite_Glow  -> glow
└─ ParticleSystem -> trail 小五角星拖尾
```

- `Sprite_Glow` 循环执行 Scale `0.8 -> 1.2`、Alpha `0.5 -> 1.0` 的波动动画。
- 飞行过程中检测敌人并造成伤害，按快照结算中毒、眩晕、侵蚀、减速、爆炸、灼烧、分裂和穿透等效果。
- 穿透时按技能规则衰减伤害；没有穿透或次数耗尽时回池。
- 分裂弹在命中目标沿母弹飞行方向的碰撞体后方生成，并把该目标写入子弹忽略集合，不能再次被同一怪物立即挡住。
- 分裂弹是否继承毒/火效果由对应“原子危机”技能决定。
- 达到最大飞行距离后必须回池/消失。

主要脚本：`EnergyBall.cs`、`EnergyBallShotSnapshot.cs`、`PlayerSpit.cs`。

## 6. 敌人、波次和对象池

- 敌人由 `WaveManager` 按来源预制体建立并复用对象池。
- 敌人回收时应解除事件订阅并重置 AI、血量、碰撞器、状态效果、状态图标和血条。
- 对象池每次取出和回收都必须恢复来源 Prefab 对应的标准缩放；`InhaleableItem` 的缩小协程被中断时也必须在 `ResetForReuse` 恢复缩放。
- 当前 100 个敌人 Prefab 根缩放均为 `1`，但素材视觉宽度约为 `0.81–3.14`，小怪尺寸差异部分来自素材本身。`WaveManager.minimumEnemyVisualSize` 默认 `1.25`，只放大低于下限的素材，不缩小正常或大型敌人。
- 新波次开始时，尚未消灭的存活敌人保留，不恢复生命值，并提升生命值以外的攻击、速度、探测、攻击范围、攻击频率和吸入抗性等属性。
- 新波次只补充应生成的新敌人，不能让池中实例数量无控制增长。

### 6.1 敌人攻击判定

- 玩家基础最大生命为 `10` 点。
- 普通怪基础伤害为 `1` 点，每 `10` 波只增加 `1` 点；精英和 Boss 在同波普通怪基础上固定增加 `1`、`2` 点，禁止使用百分比乘法产生小数伤害。
- 攻击动画周期存在可配置的有效接触窗口，默认标准化时间约为 `0.30–0.65`。
- 只有处于有效窗口且敌人 Collider 实际接触玩家 Collider 时，才造成一次伤害。
- 一个攻击周期最多伤害一次。
- AI 会继续追到 Collider 真正接触后才停下攻击，避免中心距离进入攻击范围、实际却打不到玩家时原地发呆。
- 伤害窗口按每个 AnimatorController 中 `attack` Clip 的真实长度换算，不再使用攻击冷却时间代替动画进度。
- `attack` 为非循环动画；连续贴身时每个攻击周期会显式从头重播。当前新生成敌人的默认攻击周期为 `0.9s`。
- 不同敌人动画若节奏不同，应在预制体 Inspector 调整 `attackWindowStart` 和 `attackWindowEnd`。

### 6.2 血条和状态图标

- 敌人头顶血条跟随移动并实时显示生命值。
- 玩家和敌人血条统一使用 `health_bar` 与 `health_fill`。
- 通用进度条使用 `UI_Fixed/progress_bar` 与 `UI_Fixed/progress_fill`。
- 中毒、灼烧、减速和眩晕图标在状态期间闪烁；多状态按固定顺序轮流显示。
- 侵蚀图标围绕敌人旋转，每层一个；默认三层后再次命中引爆并清空，层数保持可配置。
- 状态图标和侵蚀环绕图标已放大，但新增不同尺寸敌人时仍需目测比例。

## 7. 肉鸽技能系统

技能由 `RogueSkillCatalog`、`RogueSkillDefinition` 和 `RogueSkillManager` 驱动。

- 每次升级随机刷新三个不同技能。
- 优先保证最高 `ItemTag` 占比对应学派出现可选技能。
- 如果最高占比学派没有合法候选，使用 Normal 学派补位。
- 不满足前置条件的技能不能出现；达到最大等级的技能不能再次出现。
- 解锁条件和满级前置由目录数据判定，避免将规则散落在 UI。
- 技能选择界面打开时暂停游戏，选择后恢复。

学派包括 `Normal`、`Poison`、`Fire`、`Evolution`、`Superpower` 和 `Faith`。

### 7.1 Faith 互斥规则

这是后续修改时必须保留的关键决策：

- Faith 技能只与其他 Faith 技能互斥。
- 选择恶魔、教皇或女巫后，其他 Faith 不再出现，但其他学派仍可出现。
- 只有天使是特殊全局锁定：选择时清除其他已拥有技能，此后升级候选只出现天使。
- 不要把 `_faith.HasValue` 简单解释成“所有未来候选只能是当前 Faith”。

### 7.2 肉鸽选择 UI

- 面板约占屏幕 70%。
- 卡片显示图标、名称、描述和升级后的等级；新技能显示“新！”，已有技能显示 `当前 -> 下一级`。
- 短描述区域允许截断。
- 按住卡片约 `0.45s` 显示完整描述浮层，松手或移出后隐藏。
- 普通点击选择技能，并播放 `rogue_select`。

## 8. 女巫与野兽吞吞

- 只有拥有女巫技能时显示通灵/野兽吞吞进度条。
- 通灵进度满后进入野兽形态；期间不能吸入、吐出、吞噬，移动变为滚动并造成接触伤害。
- 野兽减伤基础 20%，每级增加 5%，最多 90%。
- 持续滚动提高移动速度，默认倍率约 `1.4`，可配置。

动画规则：

- W/背面：启动播放 `spritesheet_1 2_0–7`，持续循环 `8–12`。
- A/侧面：启动播放 `spritesheet_1_0–4`，持续循环 `5–12`；原素材缺少 `_11`，数组按实际导入顺序处理。
- D/侧面：复用侧面序列并镜像。
- S/正面：启动播放 `spritesheet_1 1_0–6`，持续循环 `7–10`。
- 持续滚动时转向，直接切换到新方向持续循环，不重新播放启动段。

音效规则：

- 开始滚动后循环播放 `Assets/Audio/SFX/Player/roll.wav`。
- 停止移动或退出野兽形态时停止循环音效。
- 野兽碰到存活敌人播放 `Assets/Audio/SFX/Env/hit.wav`。
- 碰撞命中音效约有 `0.25s` 冷却，避免每帧刷音效。
- 不需要也不应查找 `Assets/Audio/SFX/Player/hit.wav`。

## 9. 掉落系统

- 小血瓶：`Assets/Art/Sprites/Drop/blood.png`，吞噬恢复 1 点生命。
- 大血瓶：`Assets/Art/Sprites/Drop/big_blood.png`，吞噬恢复 2 点生命。
- 两者均为 `ItemTag.Normal`，质量分别为 1 和 2，因此同时增加对应升级进度。
- 血瓶必须先通过吸力牵引并吸入口中，再点击吞噬才治疗；接触玩家不直接治疗。
- 存在 20 秒，最后 5 秒闪烁，随后销毁。
- 当前敌人默认掉落概率：小血瓶 25%，大血瓶 8%，可在敌人预制体 Inspector 调整。
- 运行时预制体：`Assets/Resources/Drops/Blood.prefab`、`Assets/Resources/Drops/BigBlood.prefab`。
- 两个血瓶 Prefab 的根对象与 CircleCollider2D 均使用 `inhaleableLayer`；`BloodDrop.Awake` 也会在运行时校正 Layer，防止 Prefab 配置回退。
- 当前直接实例化和销毁；若后续大量增加掉落物，应改为独立对象池。

## 10. UI 与输入

- 虚拟摇杆不常驻显示；左半屏按下并拖动后，在鼠标/触摸起点动态唤醒。
- `joystick` 素材默认朝上，方向指针随拖动方向旋转。
- 编辑器鼠标拖动和移动端触摸应使用同一套逻辑。
- 吸入、吐出、吞噬按钮分别使用 `UI_Fixed` 的 `suck`、`spit`、`swallow`。
- 吸入/吐出与吞噬按钮使用不透明高亮显示，并提高选中、按下和禁用状态的可辨识度。
- 按钮功能因口中状态或 Faith 技能改变时，图标和可用状态必须实时刷新。
- 吸入期间吞噬按钮必须禁用；触摸与键盘输入都必须执行吸入/吐出和吞噬的互斥检查。
- 天使会将相关操作按钮改为吐出；其他 Faith 也可能改变基础操作，修改时同时检查 `InputManager`、`GameplayActionButton` 和玩家组件。
- 波次倒计时使用统一进度条资源，并从满向空减少。
- MenuScene 的选项面板是模态 UI：打开时置于最前、拦截射线并临时禁用后方菜单控件；关闭时恢复。BGM/SFX Slider 的轨道和滑块必须启用 Raycast Target 才能拖拽。

## 11. 音频系统

`AudioManager` 为跨场景常驻对象，区分 BGM、普通 SFX、循环 SFX 和关键阵亡音效。

BGM：

- Load/Menu：`normal`
- Game 普通波：`battle`
- 每十波 Boss 波：`boss`
- 普通波之间不重新播放 `battle`；Boss 波结束后切回 `battle`。

重要 SFX：

- 吐出：`split` / `big_split`
- 蓄力：循环 `charged`；蓄力吐出固定 `big_split`
- 玩家受伤/阵亡：`hurt` / `die`
- 移动：`run` / `walk`，间隔在 `PlayerController` 可调
- 吸入/吞噬：`suck` / `swallow`
- 野兽滚动：`Player/roll.wav`
- 野兽碰敌：`Env/hit.wav`
- 小怪/Boss 阵亡：`enemy_die` / `boss_die`
- 普通能量球命中：`Env/hit`
- 爆炸技能命中：`bomb`，替代普通命中音效
- 升级：`level_up`
- UI：`ui_click` / `rogue_select`
- `idle` 使用独立 AudioSource；玩家移动、使用操作按钮或受到攻击时会立即停止，并重新累计待机延迟。

肉鸽选择和 Game Over 状态只停止 SFX，不能停止 BGM。

## 12. 存档

- MenuScene 支持新游戏、继续游戏、选项及存档列表。
- 存档可继续或删除，删除前有二次确认。
- 使用存档进入 `GameScene`。
- 肉鸽技能等级会写入存档并在游戏启动时恢复。
- 调试时不要覆盖用户当前存档；测试技能只在 Play Mode 临时注入，并在退出前恢复。

## 13. 主要代码入口

| 模块 | 文件 |
| --- | --- |
| 游戏状态 | `Assets/_Project/Scripts/Managers/GameManager.cs` |
| 波次与敌人池 | `Assets/_Project/Scripts/Managers/WaveManager.cs` |
| 玩家移动/野兽 | `Assets/_Project/Scripts/Player/PlayerController.cs` |
| 吸入/吐出 | `Assets/_Project/Scripts/Player/PlayerInhale.cs`、`PlayerSpit.cs` |
| 能量球与快照 | `Assets/_Project/Scripts/Player/EnergyBall.cs`、`EnergyBallShotSnapshot.cs` |
| 吞噬进度 | `Assets/_Project/Scripts/Core/SwallowContainer.cs` |
| 肉鸽系统 | `Assets/_Project/Scripts/Skills/RogueSkillManager.cs`、`Core/RogueSkillCatalog.cs` |
| 肉鸽 UI | `Assets/_Project/Scripts/UI/RogueSelectionUI.cs` |
| 敌人 | `Assets/_Project/Scripts/Enemy/EnemyBase.cs`、`EnemyAI.cs`、`EnemyStatusEffects.cs` |
| 血瓶掉落 | `Assets/_Project/Scripts/Core/BloodDrop.cs` |
| 音频 | `Assets/_Project/Scripts/Audio/AudioManager.cs` |
| 动态 HUD | `Assets/_Project/Scripts/UI/GameplayHudController.cs` |
| 编辑器构建工具 | `Assets/Editor/SceneFlowBuilder.cs`、`RogueSystemBuilder.cs` |

## 14. 已完成部分

- Load、Menu、Game 三场景流程及 Build Settings。
- 主页菜单、存档列表、删除确认和进入游戏流程。
- 跨场景音乐系统及主要玩家、敌人、环境和 UI 音效。
- 能量球预制体、飞行、属性快照、对象池和主要肉鸽命中结算。
- 敌人对象池和跨波存活敌人强化。
- 玩家/敌人血条、升级进度、倒计时和动态操作按钮。
- 动态虚拟摇杆及鼠标支持。
- 敌人通用状态图标、侵蚀环绕图标和状态结算。
- 数据驱动肉鸽目录、三选一 UI、前置条件、满级过滤和 Faith 规则。
- 女巫通灵进度、野兽形态、滚动动画、减伤、加速、接触伤害和音效。
- 阵亡状态、结算 UI、返回菜单和重新开始。
- 大小血瓶掉落、吸入、吞噬治疗、Normal 进度和生命周期。

## 15. 待办与建议验证

后续应优先做真实 Gameplay 回归测试：

1. 针对不同敌人动画继续逐个目测校准攻击有效窗口；通用时序与连续重播逻辑已修复，但个别素材仍可能需要预制体级微调。
2. 在不同分辨率实际长按肉鸽卡片，确认描述浮层层级、字号、换行和松手隐藏。
3. 实际击杀敌人，验证血瓶概率、闪烁、消失、吸入、吞噬治疗和升级进度。
4. 验证满血吞噬血瓶时生命不溢出，但升级进度仍增加。
5. 长时间压力测试敌人池和能量球池，检查对象数量和事件订阅是否稳定。
6. 分别用四种 Faith 存档验证候选池：只有天使锁死升级，其他 Faith 仍刷新非 Faith 技能。
7. 验证滚动音效在暂停、肉鸽选择、阵亡、停止移动、变身结束和场景切换时正确停止。
8. 掉落物数量增加后实现掉落对象池。
9. `docs/DESIGN.md` 存在编码乱码且部分规则过时；继续维护前应备份、统一 UTF-8 并按当前代码修订。

## 16. 最近验证状态

最近一次 UnityMCP 验证：

- Unity 脚本刷新和编译完成。
- Console：`0 Error`。
- Play Mode 中按钮图标已加载，吸入/吐出与吞噬按钮 Alpha 均为 `1.0`。
- 贴身攻击运行超过多个 `0.9s` 周期后，attack Animator 标准化进度重新回到约 `0.31`，确认非循环攻击动画会连续重播。
- 独立播放 `idle` 后调用玩家活动中断，音源由播放中立即变为停止。
- Play Mode 生成的小血瓶位于 Layer 6，并被吸力成功牵引、收入 `SwallowContainer`。
- 升级需求验证为 `30 -> 50 -> 75 -> 102 -> 138`，升级后正确保留溢出进度。
- MenuScene 模拟 PointerDown/Drag 后 Slider 值和 AudioManager 音量同步变化；选项打开时后方可交互控件为 `0`，关闭后恢复。
- 分裂测试生成 2 颗子弹，最近生成点位于目标后边缘之外，并正确忽略原目标。
- 吸入期间吞噬测试保持口中物品与进度不变；停止吸入后正常吞噬；主操作按住时吞噬请求被拦截。
- 玩家最大生命验证为 `10`，普通怪第 `1/11/21` 波伤害验证为 `1/2/3`。
- 对象池缩放从测试值 `0.1` 恢复到标准值 `1.5`；Character (49) 自动放大约 `1.54` 倍，Character (90) 保持 `1.0`。
- 已退出 Play Mode。
- 未永久修改用户存档。

以上证明基础编译、掉落实例化、按钮亮度、攻击动画重播和待机音效中断通过，不能替代第 15 节的完整交互测试。

## 17. 工作区注意事项

- Git 工作区包含大量既有修改和未跟踪资源，许多属于此前功能开发或用户导入。
- 不要执行 `git reset --hard`、`git checkout --` 或批量删除。
- 修改前查看 `git status --short`，只处理当前需求涉及的文件。
- Unity `.meta` 文件必须与资源一起保留。
- 用户描述中的 `Assest`、`Autio` 是历史拼写错误，实际目录为 `Assets/Art/...`、`Assets/Audio/...`。
- 优先使用 UnityMCP 处理场景、预制体、序列化引用、Play Mode 和 Console 验证。
- 修改脚本后必须等待 Unity 编译并检查 Console Error。

## 18. 下次会话建议加载顺序

1. 完整读取本文件。
2. 查看 `git status --short`。
3. 根据任务读取第 13 节相关脚本；代码和场景是最终事实来源。
4. 使用 UnityMCP 确认活动实例、当前场景和 Console。
5. 完成修改后进行与风险相称的 Play Mode 验证。
6. 按顶部维护规则更新本文件，只保留仍有效的关键信息。
