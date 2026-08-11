# 配置管理规范

本项目的运行时数值统一使用 Unity `ScriptableObject` 资产管理。配置资产必须纳入版本控制；逻辑脚本负责解释配置，不负责保存玩法数值。

## 目录约定

```text
Assets/_Project/Config/
├── Resources/GameBalanceConfig.asset  # 玩家、吸入、吐出、食物、敌人公共参数
├── Balance/WaveConfig.asset           # 楼层、房间计时和难度曲线
├── Enemies/<Enemy>.asset              # 每个怪物独立 EnemyData
├── Skills/                             # 独立技能配置
├── Animation/PlayerAnimData.asset      # 玩家逐帧动画配置
└── Resources/
    ├── Rogue/RogueSkillCatalog.asset
    └── System/EnergyBallHitVfxCatalog.asset
```

`Assets/_Project/Generated` 只存放构建产物，包括 Prefab、Animator、AnimationClip 和 Addressables 内容入口。禁止在该目录维护玩法配置，重新构建时其中内容可能被删除。

## 配置所有权

| 配置 | 负责内容 | 修改后操作 |
| --- | --- | --- |
| `GameBalanceConfig` | 玩家基础属性、移动状态、吸入、吐出、食物、敌人公共移动/接触参数，以及火球屏外起点、下落时长和粒子视觉倍率 | 运行配置校验；进入 Play Mode 回归 |
| `WaveConfig` | 房间计时、生成数量、楼层成长、危急状态、精英和 Boss 系数 | 运行配置校验；逐房回归 |
| `Enemies/<Enemy>.asset` | 生命、攻击、基础/特殊速度、质量、感知、攻击间隔、离地高度/时长、屏外留白、阶段切换/保持/冲刺时序、受伤阈值膨胀倍率和死亡效果 | 运行配置校验；死亡帧、阶段保持帧、动画时长或视觉层级变化后重建敌人内容 |
| `Skills` / `RogueSkillCatalog` | 技能名称、描述、等级、前置、图标和技能资源 | 运行配置校验；肉鸽选择回归 |

## 修改流程

1. 在 Inspector 中修改 `Assets/_Project/Config` 下对应资产，不修改运行时逻辑脚本。
2. 执行 `Tools > Devouring Beast > Validate Game Configuration`。
3. 修改怪物死亡帧、动画导入信息或新增怪物时，执行 `Tools > Devouring Beast > Build New Enemy Content`。
4. 在 Play Mode 验证修改涉及的玩家、房间或怪物行为。
5. 发布前重新构建 Addressables，并执行目标平台 Player Build。

`GameConfigBuildPreprocessor` 会在 Player Build 前自动执行同一套校验。缺失主配置、怪物枚举重复、非法生命/速度/攻击、关键行为参数为空或 Generated 中出现 `EnemyData` 时，构建会直接失败。

## 编码约束

- 新增可调数值时，先确定归属配置，再让逻辑读取该字段；禁止新增用于平衡调整的魔法数字。
- 公共规则放在 `GameBalanceConfig`，单个怪物差异放在对应 `EnemyData`，楼层成长只放在 `WaveConfig`。
- 配置引用使用强类型字段和枚举，不使用字符串键值表或运行时 JSON 反射。
- 配置资产移动必须通过 Unity `AssetDatabase` 或 Editor 完成，以保留 GUID 和现有引用。
- 不在运行时修改配置资产；动态强化使用 `EnemyData.ApplyScaling` 创建的运行时副本。
- 不为缺失配置提供另一套隐藏数值。缺失或非法配置应由校验器阻止构建。

## 怪物内容构建

`EnemyContentBuilder` 的代码只描述美术目录、动画帧范围和内容分组。生命、攻击、速度、质量、攻击冷却、行为与死亡配置全部读取 `Assets/_Project/Config/Enemies`，构建器不会删除或覆盖这些资产。

调整普通数值后无需重建 Prefab；编辑器运行时会直接读取 `EnemyData`。修改 `deathFrameIndex` 或死亡动画时长后需要重建，因为对应 Sprite 和 AnimationClip 属于生成内容。
