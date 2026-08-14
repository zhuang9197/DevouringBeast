# 配置文件字段说明

本文覆盖 `Assets/_Project/Config` 下当前会被项目读取的配置资源。数值修改后应在 Unity 中等待脚本/资源刷新，并在构建前执行项目配置校验。

## 常用数值速查

| 要调整的内容 | 配置路径 | 字段 |
| --- | --- | --- |
| 角色基础移动速度 | `Config/Resources/GameBalanceConfig.asset` | `player.baseMoveSpeed` |
| 野兽滚动速度倍率 | 同上 | `player.beastRollingSpeedMultiplier` |
| 女巫变身所需进度 | 同上 | `player.witchTransformProgressRequired` |
| 野兽形态进度上限 | 同上 | `player.witchBeastProgressMaximum` |
| 野兽形态每秒消耗 | 同上 | `player.witchBeastProgressDrainPerSecond` |
| 玩家能量球速度 | 同上 | `spit.speed` |
| 玩家能量球伤害 | 同上 | `player.baseEnergyBallDamage` |
| 敌人移动速度 | `Config/Enemies/<Enemy>.asset` | `moveSpeed` |
| 敌人瞄准子弹速度 | 同上 | `aimedProjectileSpeed`；为 `0` 时使用全局 `enemy.aimedProjectileSpeed` |
| 敌人环形子弹速度 | 同上 | `radialProjectileSpeed`；为 `0` 时使用全局 `enemy.radialProjectileSpeed` |
| 敌人冲刺速度 | 同上 | `behavior.dashSpeed` |
| 玩家/怪物命中范围 | `GameBalanceConfig.asset` | `visualColliderRadiusScale`、`minimumColliderRadius` |

## GameBalanceConfig.asset

资源：`Assets/_Project/Config/Resources/GameBalanceConfig.asset`。

### player

| 字段 | 含义 |
| --- | --- |
| `baseMoveSpeed` | 玩家基础世界移动速度，也是敌人归一化速度换算的玩家基准。 |
| `baseSuction` | 玩家基础吸力。 |
| `baseEnergyBallDamage` | 普通能量球基础伤害。 |
| `maxHealth` | 初始最大生命值。 |
| `invincibleDuration` | 普通受伤后的无敌时间（秒）。 |
| `visualColliderRadiusScale` | 以当前精灵较短边的一半为基准计算圆形碰撞半径时的倍率。 |
| `minimumColliderRadius` | 玩家圆形碰撞体允许的最小半径。 |
| `fullWalkSpeedMultiplier` | 嘴里有物品时的移动速度倍率。 |
| `inhaleWalkSpeedMultiplier` | 获得吸入行走能力后，吸入期间的移动倍率。 |
| `runStepInterval` | 正常奔跑脚步音间隔。 |
| `walkStepInterval` | 满嘴行走脚步音间隔。 |
| `idleSoundDelay` | 静止多久后首次播放待机音。 |
| `idleSoundRepeatInterval` | 持续静止时待机音的重复间隔。 |
| `knockbackDuration` | 玩家受击击退的位移动画时长。 |
| `witchTransformProgressRequired` | 女巫状态进入野兽形态前需要累计的吞噬进度。 |
| `witchBeastProgressMaximum` | 变身成功后进度条填充的总值，也是野兽形态可补充进度的上限。 |
| `witchBeastProgressDrainPerSecond` | 野兽形态维持期间每秒持续消耗的进度值。 |
| `beastRollingSpeedMultiplier` | 女巫野兽形态滚动时的速度倍率；当前为 `1.5`。 |
| `beastDamageReductionBase` | 野兽形态基础减伤；滚动时另有完全无敌。 |
| `beastDamageReductionPerLevel` | 女巫每级增加的野兽形态减伤。 |
| `beastDamageReductionLimit` | 野兽形态减伤上限。 |
| `beastHitRadius` | 野兽滚动撞击敌人的检测半径。 |
| `beastDamagePerSecond` | 野兽滚动每秒基础伤害。 |
| `beastDamagePerLevel` | 女巫每级对滚动伤害增加的比例。 |
| `beastHitSoundCooldown` | 连续撞击时音效的最小间隔。 |

### inhale

| 字段 | 含义 |
| --- | --- |
| `angle` | 吸入扇形角度。 |
| `radius` | 吸入检测半径。 |
| `maximumDuration` | 单次持续吸入上限。 |
| `maximumSuctionForce` | 吸力成长后的最大值。 |
| `suctionRampTime` | 吸力从基础值增长到最大值所需时间。 |
| `intakeDistance` | 物体进入嘴里的判定距离。 |
| `minimumPullSpeed` | 吸动物体的最低速度。 |
| `maximumPullSpeed` | 吸动物体的最高速度。 |
| `corpsePullSpeedMultiplier` | 尸体/掉落物相对普通拉动速度的倍率。 |
| `corpseMaximumPullSpeed` | 尸体/掉落物拉动速度上限。 |
| `suctionMassSpeedFactor` | 质量对拉动速度的衰减系数。 |
| `aliveEnemyMaximumSpeedBoost` | 活敌被吸时允许的最大额外速度倍率。 |

### spit

| 字段 | 含义 |
| --- | --- |
| `speed` | 玩家吐出能量球的基础速度。 |
| `maximumDistance` | 能量球最大飞行距离。 |
| `spawnForwardOffset` | 能量球相对玩家朝向的出生偏移。 |
| `poolInitialSize` | 能量球对象池初始数量。 |
| `poolMaximumSize` | 能量球对象池最大数量。 |
| `maximumChargeTime` | 蓄力达到满值所需时间。 |
| `bigMassThreshold` | 判定为大质量吐出的质量门槛。 |
| `spreadAngle` | 多发能量球之间的散射角。 |
| `angelShotCooldown` | 天使自动射击间隔。 |
| `popeDamageMultiplier` | 教皇教化球伤害倍率。 |
| `popeFollowerInitialProgress` | 教皇召唤第一名信徒所需的吞噬进度。 |
| `popeFollowerProgressIncrease` | 每召唤一名信徒后，下一名信徒所需进度的增加值。 |
| `chargeBonusPerLevel` | 蓄力技能每级增加的伤害比例。 |
| `multipleMouthPerBallMultiplier` | 多嘴模式每颗球的基础伤害倍率。 |
| `multipleMouthPowerPerLevel` | 多嘴强化每级增加的单球伤害比例。 |
| `maximumBallCount` | 同次吐出允许的最大能量球数量。 |

### food

| 字段 | 含义 |
| --- | --- |
| `initialFoodPerRoom` | 房间首次进入时的普通食物总额度。 |
| `refreshBatchSize` | 每次刷新生成的食物数量。 |
| `maxActiveFood` | 单房间同时存在的食物上限。 |
| `refreshSeconds` | 战斗中刷新间隔。 |
| `clearedRefreshSeconds` | 清房后的刷新间隔。 |
| `popeGuaranteeRefreshSeconds` | 未清房、额度耗尽且无食物时，教皇保底刷新间隔。 |
| `minimumSpacing` | 食物之间的最小生成间距。 |
| `boundsPadding` | 食物生成范围相对房间边界的内缩距离。 |
| `placementAttempts` | 每个食物寻找合法位置的最大尝试次数。 |
| `landingDuration` | 食物落地动画时间。 |
| `colliderRadius` | 食物圆形碰撞半径。 |
| `worldScale` | 食物显示缩放。 |
| `riceBallMass` / `baoziMass` | 饭团/包子的吞噬质量。 |
| `hotDogMass` / `sushiMass` | 热狗/寿司的吞噬质量。 |

### statues

| 字段 | 含义 |
| --- | --- |
| `healthCost` | 雕像单次献祭消耗生命。 |
| `angelBreakHits` | 天使雕像被打破需要的命中次数。 |
| `angelHeartDrops` | 打破天使雕像掉落的心数量。 |
| `popeFoodPerHealth` | 教皇每点生命可补充的食物额度。 |
| `popeFoodPerOffering` | 教皇每次献祭立即生成的食物数量。 |
| `visualHeight` | 雕像视觉高度。 |
| `frontContactDot` | 判定玩家位于雕像正面的方向点积门槛。 |

### enemy（所有敌人共用）

| 字段 | 含义 |
| --- | --- |
| `normalizedSpeedLimit` | 敌人最终归一化移动速度上限。 |
| `separationRadius` | 敌人群体分离的检测半径。 |
| `chaseWeight` | 直接追踪玩家方向权重。 |
| `irregularChaseWeight` | 不规则偏航追踪权重。 |
| `separationWeight` | 避免敌人重叠的分离权重。 |
| `horizontalFacingDeadZone` | 水平速度/相对位置低于该值时不更新左右朝向。 |
| `initialAttackDelayRange` | 敌人生成后首次攻击延迟的随机范围。 |
| `steeringSpeedRange` | 不规则追踪相位速度随机范围。 |
| `steeringRadiusRange` | 不规则追踪目标偏移半径随机范围。 |
| `colliderContactTolerance` | 两碰撞体距离小于该值时视为接触。 |
| `fallbackContactRadius` | 缺少可用碰撞体时的接触检测半径。 |
| `contactKnockbackDistance` | 普通接触对玩家的击退距离。 |
| `areaKnockbackDistance` | 范围攻击对玩家的击退距离。 |
| `contactCooldown` | 同一敌人连续接触伤害间隔。 |
| `visualColliderRadiusScale` | 怪物精灵较短边换算圆形命中碰撞半径的倍率。 |
| `minimumColliderRadius` | 怪物命中碰撞体最小半径。 |
| `aimedProjectileSpeed` | 敌人瞄准子弹的全局后备速度。 |
| `radialProjectileSpeed` | 敌人环形子弹的全局后备速度。 |
| `summonOffsetMinimum` / `summonOffsetMaximum` | 召唤物相对召唤者的随机距离范围。 |
| `corpseLifetime` | 尸体保留时间。 |
| `fireballFallHeight` | 火球出生点相对落点的最低高度。 |
| `fireballOffscreenPadding` | 火球出生点超出屏幕上边缘的距离。 |
| `fireballFallDuration` | 未单独配置时的火球下落时间。 |
| `fireballOrbitRadius` / `fireballOrbitTurns` | 火球下落轨迹的绕行半径和圈数。 |
| `fireballVisualScale` / `fireballParticleScale` | 火球精灵和粒子缩放。 |
| `fireballLandingMarkerScale` | 火球落点标记缩放。 |
| `fireballExplosionRadius` / `fireballExplosionDamage` | 火球落地爆炸半径与伤害。 |
| `fireballBurnRadius` / `fireballBurnDuration` / `fireballBurnDamage` | 地面燃烧区半径、持续时间与每次伤害。 |
| `fireballBurnVisualScale` | 地面燃烧视觉缩放。 |

## WaveConfig.asset

资源：`Assets/_Project/Config/Balance/WaveConfig.asset`。

| 字段 | 含义 |
| --- | --- |
| `normalWaveTimer` / `bossWaveTimer` | 普通/精英房和 Boss 房倒计时。 |
| `crisisEmpowerInterval` | 超时危急状态强化存活敌人的间隔。 |
| `crisisTimeScaleStart` / `crisisTimeScaleStep` / `crisisTimeScaleMax` | 危急状态初始倍速、每次增量和上限。 |
| `crisisTimeScaleIncreaseInterval` | 危急倍速增长的现实时间间隔。 |
| `crisisOverlayMinAlpha` / `crisisOverlayMaxAlpha` | 危急红光最低/最高透明度。 |
| `crisisOverlayPulseFrequency` / `crisisOverlayPulseFloor` / `crisisOverlayPulseSharpness` | 红光脉冲频率、低谷和波峰锐度。 |
| `baseEnemyCount` / `enemiesPerWaveIncrement` | 旧波次兼容的基础数量与递增数量。 |
| `elitePer5Waves` / `bossPer10Waves` | 旧波次兼容的精英/Boss 额外数量。 |
| `normalHealthScale` / `normalSpeedScale` | 按楼层/旧波次应用的普通血量与速度成长。 |
| `baseAttackDamage` | 敌人基础整数伤害。 |
| `damageIncreaseInterval` | 每经过多少层级区间增加 1 点基础伤害。 |
| `eliteDamageBonus` / `bossDamageBonus` | 精英/Boss 额外伤害。 |
| `survivorAttackRangeScale` / `survivorDetectRangeScale` | 每次危急强化的攻击/检测范围倍率。 |
| `survivorAttackSpeedScale` / `survivorInhaleResistanceScale` | 每次危急强化的攻速/吸入抗性倍率。 |
| `eliteHealthMul` / `eliteDamageMul` / `eliteSpeedMul` | 精英血量、伤害、速度倍率。 |
| `bossHealthMul` / `bossDamageMul` / `bossSpeedMul` | Boss 血量、伤害、速度倍率。 |
| `maxTier` | 敌人素材/等级封顶。 |

## Enemies/*.asset

20 个敌人都使用相同 `EnemyData` 结构，资源位于 `Assets/_Project/Config/Enemies`。

### 基础与战斗

| 字段 | 含义 |
| --- | --- |
| `displayName` | 显示名/调试名。 |
| `archetype` | 行为类型枚举，决定运行逻辑。 |
| `tier` | 敌人等级。 |
| `maxHealth` | 基础最大生命。 |
| `attackDamage` | 基础攻击伤害。 |
| `moveSpeed` | 相对玩家基准的归一化移动速度。 |
| `attackRange` | 攻击距离配置。 |
| `attackCooldown` | 两次攻击动作开始之间的间隔。 |
| `detectRange` | 发现玩家的范围。 |
| `aimedProjectileSpeed` / `radialProjectileSpeed` | 本敌人的瞄准/环形子弹速度；大于 0 时覆盖全局值。 |
| `massValue` | 吞噬后提供的质量。 |
| `aliveInhaleThreshold` / `deadInhaleThreshold` | 活体/尸体可被吸动需要的吸力门槛。 |

### behavior

| 字段 | 含义 |
| --- | --- |
| `wanderIntervalRange` | 每次重新选择游走方向的时间范围。 |
| `proximityRange` | 行为近距离半径；Fly 使用它作为本地绕圈半径。 |
| `specialMoveSpeed` | 特殊移动速度。 |
| `dashSpeed` / `jumpSpeed` | 冲刺/跳跃速度。 |
| `fireballFallDuration` | 本敌人的火球下落时间；为 0 时使用全局值。 |
| `dashDuration` | 固定时长冲刺动作的持续时间。 |
| `movementCycleDuration` | 周期移动总时长。 |
| `movementActiveDuration` / `movementIdleDuration` | 周期中的移动/停顿时长。 |
| `actionsPerSpecial` | 每多少次普通行动触发一次特殊行为。 |
| `wanderMaximumTurnAngle` | 游走重新选方向时单次最大转角。 |
| `wanderTurnSpeed` / `evasiveTurnSpeed` | 普通游走/逃跑转向速度。 |
| `orbitAngularSpeed` | 圆周行为转向强度/角速度参数。 |
| `orbitPursuitWeight` / `orbitTangentWeight` / `orbitSeparationWeight` | 圆周追踪的追击、切向和分离权重。 |
| `specialAttackCooldown` | 特殊攻击冷却；为 0 时回退到 `attackCooldown`。 |
| `jumpHeight` | 视觉离地高度。 |
| `takeoffDuration` / `airborneDuration` / `landingDuration` | 起飞、滞空、落地时间。 |
| `offscreenPadding` | 飞出屏幕时超出可视边界的距离。 |
| `stateTransitionDuration` / `stateHoldDuration` | 阶段切换和阶段保持时间。 |
| `dashPreparationDuration` / `dashRecoveryDuration` | 冲刺前摇和后摇。 |
| `healthLossEffectInterval` | 每损失多少生命比例触发一次受伤阈值效果。 |
| `healthLossEffectMaximumTriggers` | 阈值效果最多触发次数。 |
| `healthLossEffectBulletCount` | 每次阈值效果发射的子弹数。 |
| `healthLossEffectSummonsMeatball` | 阈值效果是否召唤肉团。 |
| `healthLossEffectMaximumScale` | 受伤蓄积时的最大膨胀倍率。 |
| `healthLossEffectPulseDuration` | 阈值触发后缩放回弹时间。 |

### 动画、死亡与死亡效果

| 字段 | 含义 |
| --- | --- |
| `animatorController` | 运行时 Animator Controller。 |
| `fakeDeathHoldSprite` | 骷髅假死保持帧。 |
| `phaseTwoIdleSprite` | 小撒旦二阶段待机保持帧。 |
| `popInClip` | 敌人出生动画。 |
| `deathMode` | `0` 静态死亡图；`1` 死亡动画末帧尸体；`2` 死亡动画后宝箱；`3` 直接宝箱。 |
| `deathSprite` | 静态尸体或死亡动画结束后的精灵。 |
| `deathAnimationDuration` | 死亡动画总时长。 |
| `deathAnimationStartFrame` | 死亡动画起始源帧；`-1` 表示结束帧前 5 帧。 |
| `deathFrameIndex` | 死亡结束源帧；`-1` 表示素材最后一帧。 |
| `deathEffect.effect` | `0` 无；`1` 范围爆炸；`2` 召蜘蛛和卵巢；`3` 分裂白怪；`4` 掉整心。 |
| `deathEffect.delay` | 死亡后延迟多久执行效果。 |
| `deathEffect.radius` / `damage` / `knockback` | 死亡效果半径、伤害和是否击退。 |
| `deathEffect.summonCount` | 死亡效果召唤数量。 |
| `deathEffect.secondaryChance` | 次级死亡效果触发概率。 |

## RogueSkillCatalog.asset

资源：`Assets/_Project/Config/Resources/Rogue/RogueSkillCatalog.asset`。

| 字段 | 含义 |
| --- | --- |
| `skills[].id` | 技能的稳定枚举 ID，用于存档。 |
| `skills[].school` | 学派：普通、毒、火、进化、超能力、信仰；火与毒互斥。 |
| `skills[].displayName` / `description` | 选择界面名称和说明。 |
| `skills[].iconName` | 从 `skillIcons` 按名称查找的图标名。 |
| `skills[].maxLevel` | 最大等级；`0` 表示无上限。 |
| `skills[].prerequisites` | 前置技能 ID 列表。 |
| `skills[].requiresMaxPrerequisites` | 是否要求所有前置技能满级。 |
| `skills[].mythic` | 是否为只能选择一个的信仰/神话技能。 |
| `rogueSelectionBackground` / `buttonBackground` | 肉鸽选择界面背景资源。 |
| `poisoningIcon` / `burnIcon` / `slowdownIcon` / `dizzinessIcon` / `erosionIcon` | 状态图标。 |
| `skillIcons` | 肉鸽技能图标集合。 |
| `beastFront` / `beastBack` / `beastSide` | 野兽静止三方向精灵。 |
| `beastFrontRoll` / `beastBackRoll` / `beastSideRoll` | 野兽滚动三方向帧。 |
| `progressBar` / `progressFill` | 进度条底图/填充。 |
| `joystick` / `suckButton` / `spitButton` / `swallowButton` | 游戏操作 UI 精灵。 |
| `healthBar` / `healthFill` / `healthFull` / `healthHalf` / `healthEmpty` | 生命 UI 资源。 |

## Skills/*.asset（旧技能兼容资源）

这些资源仍保留 GUID 兼容，当前肉鸽主逻辑以 `RogueSkillCatalog.asset` 为准。

| 字段 | 含义 |
| --- | --- |
| `skillName` / `description` | 旧技能名称和描述。 |
| `maxLevel` / `currentLevel` | 最大等级和旧运行时等级。 |
| `prerequisite` | 旧前置技能引用。 |
| `levelValues` | 各等级的数值表，具体语义由旧技能类型决定。 |
| `tag` | 部分旧 YAML 中残留的已移除字段，当前脚本不读取。 |

## PlayerAnimData.asset

资源：`Assets/_Project/Config/Animation/PlayerAnimData.asset`。

| 字段 | 含义 |
| --- | --- |
| `idleFront` / `idleBack` / `idleSide` | 空嘴静止的正面、背面、侧面精灵。 |
| `fullFront` / `fullBack` / `fullSide` | 满嘴静止的三方向精灵。 |
| `frontRun` / `backRun` / `sideRun` | 三方向奔跑帧。 |
| `frontFullWalk` / `backFullWalk` / `sideFullWalk` | 三方向满嘴行走帧。 |
| `frontSuck` / `backSuck` / `sideSuck` | 三方向吸入帧。 |
| `frontSuckWindupEnd` / `backSuckWindupEnd` / `sideSuckWindupEnd` | 各方向吸入前摇结束帧索引，后续帧作为持续吸入循环。 |

## EnergyBallHitVfxCatalog.asset

资源：`Assets/_Project/Config/Resources/System/EnergyBallHitVfxCatalog.asset`。

| 字段 | 含义 |
| --- | --- |
| `normalParticle` / `fireParticle` / `poisonParticle` | 普通、火焰、毒命中粒子精灵。 |
| `particleMaterial` | 命中粒子材质。 |
| `fireExplosionFrames` / `poisonExplosionFrames` | 火/毒爆炸逐帧精灵。 |
| `explosionFramesPerSecond` | 爆炸动画帧率。 |
| `fireExplosionScale` / `poisonExplosionScale` | 火/毒爆炸显示缩放。 |
| `poisonCloud` / `poisonCloudMaterial` | 毒云精灵和材质。 |
| `poisonCloudLifetime` | 毒云粒子寿命。 |
| `poisonCloudStartSize` / `poisonCloudEndSize` | 毒云起始/结束尺寸。 |
