# DevouringBeast - 项目入口与会话交接

> 最后更新：2026-08-10
>
> Unity：2022.3.62f3c1，URP 2D
>
> 目标平台：Android 横屏，固定 16:9 游戏视口

本文记录当前有效的玩法规则、关键实现入口和验证基线。代码、场景和 Unity 运行结果始终是最终事实来源。

## 1. 文档导航

| 文档 | 用途 |
| --- | --- |
| [readme.md](readme.md) | 当前项目入口与交接摘要 |
| [CONFIGURATION.md](CONFIGURATION.md) | 配置目录、所有权、修改流程与构建校验规范 |
| [SESSION_CONTEXT.md](SESSION_CONTEXT.md) | 系统实现、资源与维护约束 |
| [DESIGN.md](DESIGN.md) | 当前玩法设计，不包含历史方案 |

## 2. 核心循环

```text
探索房间 -> 进门锁门 -> 清除敌人 -> 开门并拾取补给
          -> 吸入食物/奖励 -> 吐出或吞噬 -> 随机肉鸽三选一
          -> 击败 Boss -> 可继续探索或进入下一层
```

- 存活敌人只能受到吸力伤害；敌人死亡后按类型保留死亡图标、保留死亡动画末帧，或消失并掉落等质量宝箱。
- 吞噬只累计质量进度，不再记录物种、标签或学派占比。
- 升级候选从所有当前合法技能中等权随机，仍统一执行前置、满级、Faith 互斥和形态收益过滤。
- 玩家不会再根据吞噬内容改变颜色。

## 3. 楼层、房间与相机

- 每层生成恰好 10 个大小相同、上下左右相邻且连通的房间。
- 游戏共 5 层；每层固定生成 10 个房间。第 1-4 层各有 2 个精英房且不生成 Boss，第 5 层仅有 1 个 Boss 房。
- 不生成隐藏门或隐藏房间。
- 房间世界尺寸为 `32 x 18`，相机正交尺寸为 `9`，固定 16:9 视口；相机不再跟随玩家在大地图中滚动。
- 进入未清理房间时立即封锁所有连通门并生成本房敌人；全部击杀后开门。
- 已清理房间不会再次生成敌人，可随时返回探索。
- 前四层的两个精英房全部清理后，在最后清理的精英房生成下一层入口；第 5 层 Boss 清理后立即通关并归档存档。
- 进入下一层后重新生成一套全新的 10 房间布局。

地图视觉使用 `Assets/Art/Tilesets/new_map.png` 的手工切片：`new_map1` 为默认朝向左上的墙角、`new_map2` 为默认位于下墙的开启门、`new_map3` 为默认位于上墙的墙壁；地板使用从原图同一条连续区域切出的 `new_map4`、`new_map4_1` 至 `new_map4_4`。运行时由 `FloorMapManager` 创建 `FloorRooms/RoomTilemap`，以 16 x 9 网格绘制每个房间。地板条带先顺序铺设，再以水平往返镜像和逐行垂直镜像延展，使每条瓦片接缝两侧采样同一源像素边缘，避免非无缝手绘纹理产生方块拼接感。墙角、墙和门仍按四边方向旋转；关闭状态下连通门位显示 `new_map3`，清房后才替换为 `new_map2`。角色、敌人和环境生成范围从可视房间边缘内缩 1.5 世界单位。旧 `ArenaGrid` 不再参与运行。

## 4. 房间战斗与危急状态

- 普通房和精英房倒计时为 60 秒，Boss 房为 120 秒。
- 倒计时最后 3 秒使用红色填充；进入 Boss 房后，其下方固定显示复用同一底框和红色填充素材的 Boss 总血条，离开或清理 Boss 房后隐藏。
- 计时只属于当前房间，不再按时间自动生成下一波。
- 倒计时归零时房门仍保持关闭，屏幕进入红色危急状态。倍速由 1.15 倍开始连续增长至 1.75 倍；红光以低谷接近熄灭、峰值短促的警告节奏持续闪烁，峰值透明度随当前倍速从 0.08 连续增长至 0.26，达到上限后保持强度但继续闪烁。覆盖层位于 HUD 后方。
- 危急状态每 5 秒强化一次所有存活敌人的攻击、移动、范围、频率和吸入抗性；当前生命值不会重置。进入危急状态时全局速度从 1.15 倍开始，每 10 秒提高 0.05，最高 1.75 倍，敌我双方、动画和投射物同步加速。
- 只有消灭全部敌人才能结束危急状态并开门。
- 小怪进入房间时先在各自位置播放约 0.5 秒的蘑菇云烟雾，再显示实体；追踪型敌人使用独立偏航和分离 steering，避免聚成一团。敌人与玩家实际接触时会造成伤害、轻微击退，并中断吸入。
- 移动速度使用归一化单位：玩家配置的基础世界速度为 `1`，敌人最终归一化速度上限为 `2`；玩家的技能、食物和形态增益不受该上限限制。敌人数值与楼层/精英强化倍率相乘后，再统一执行上限裁剪。
- 蝙蝠的轨迹速度由持续旋转的切向分量和朝向玩家的平移分量叠加，形成整体向玩家推进的连续圆周轨迹；不是旋转 Sprite 本体。
- 血滴子以 2.5 至 4 秒的间隔选择有限转角的游走方向，接近玩家时平滑转向逃离；Sprite 只在水平移动方向明确时翻转，避免竖向移动引发左右抽搐。
- 每个房间独立维护有限食物额度、地面活跃数量和刷新计时。战斗中按 `GameBalanceConfig.food.refreshSeconds` 刷新，清理后按 `clearedRefreshSeconds` 加快，额度归零后停止普通刷新；教皇雕像可补充额度，未清理且归零时提供至多 1 个保底食物。
- 默认食物为饭团和包子，热狗与寿司由肉鸽技能解锁；所有食物无视当前吸力阈值。
- 天使和恶魔通过击杀获得升级质量，获得量固定为被击杀敌人质量的 2 倍；重复升级只增强各自其他成长项，不会继续叠加击杀质量倍率。
- 恶魔状态下吸入会扫描战斗层上的存活怪物并持续造成吸力伤害，同时免疫敌人接触造成的吸入中断；非恶魔状态仍保留原有接触中断规则。
- 主操作键按住期间即使已有物品进入口中，按钮仍保持按下前的吸入图标；真正松开后才根据口中内容切换为吐出图标，即使吸入先达到时长上限也不会提前切换。
- 左半屏任意位置都可持续控制移动；固定 16:9 视口产生黑边时，摇杆图标会夹在安全区与相机实际渲染区域的交集内，避免图标画入未清屏区域后残留。
- 食物、怪物遗体和质量宝箱在实际受到吸力拉动时，仅临时忽略自身与玩家的碰撞；停止拉动后立即恢复，因此平时仍可被角色推动且不会卡在入口距离之外。
- 主页设置提供吸入/吐出按钮与吞噬按钮的大小、水平偏移和垂直偏移调整，并在调整时实时显示位置预览；设置通过 `PlayerPrefs` 跨场景和重启保留。
- 长按吸入或蓄力吐出时，主按钮显示红色脉冲；吸力或蓄力达到上限后保持红色，操作结束后恢复。蓄力音效每次蓄力只播放一遍，不循环。
- 教皇拥有蓄力技能时，教化按钮支持长按蓄力；松开后再吞噬并发射教化球，蓄力伤害增幅和大能量球音效正常生效。
- 教皇的教化球与普通吐出共用当前技能弹幕构建流程，包括多嘴数量、单球倍率和能量球技能快照。
- 肉鸽选择期间禁止产生新的战斗/移动音效，但不会截断已触发的吞噬、教化和升级音效；`rogue_select` 作为关键 UI 音效不受抑制。
- `WaveManager.CurrentWave` 仅为旧 UI/存档兼容入口，其运行语义已改为当前楼层。

- 基础技能 `跑快快`、`大喇叭`、`吐吐吐` 不再进入升级随机池，只能从每层初始房的天使雕像三选一获得；每房有教皇雕像，每层有一个恶魔雕像。

## 5. 掉落物与影子层

- `Drops` 为血量掉落专用物理层。血量掉落不再拥有 `InhaleableItem`，不会被吸力检测。
- 玩家触碰血量掉落时自动治疗，且不会超过当前生命上限。
- 半心恢复 1 点；整心恢复 2 点。玩家只缺 1 点时触碰整心，只消耗 1 点并把掉落物切换为半心图标留在原地。
- 血量掉落不再计时闪烁或消失，在当前楼层内跨房间保留；只有进入下一层时统一回池。
- `Shadows` 为影子专用物理层。玩家、敌人、环境物品、血量掉落和下一层入口使用通用椭圆黑影。
- 敌人、物品或入口开始落地时先显示小影子，并随落地过程扩大到正常尺寸。
- 带阵亡图标或死亡动画末帧的尸体在死亡表现完成后切换到 Layer 6 `inhaleableLayer`，可像食物和宝箱一样被吸入；掉落宝箱型敌人仍在死亡表现结束时以宝箱替换尸体。

## 6. 主要代码入口

| 模块 | 入口 |
| --- | --- |
| 楼层布局、房间门、换层 | `Assets/_Project/Scripts/Managers/FloorMapManager.cs` |
| 房间战斗、计时、危急强化、敌人池 | `Assets/_Project/Scripts/Managers/WaveManager.cs` |
| 战斗配置 | `Assets/_Project/Scripts/Managers/WaveConfig.cs` |
| 全局数值配置 | `Assets/_Project/Config/Resources/GameBalanceConfig.asset` |
| 怪物独立配置 | `Assets/_Project/Config/Enemies/*.asset` |
| 配置结构与加载 | `Assets/_Project/Scripts/Config/GameBalanceConfig.cs` |
| 配置构建前校验 | `Assets/Editor/GameConfigValidator.cs` |
| 地图边界与固定相机 | `Assets/_Project/Scripts/Core/MapBounds.cs`、`CameraFollow.cs` |
| 地图 Tile 资源构建 | `Assets/Editor/ArenaTilemapBuilder.cs` |
| 吞噬进度 | `Assets/_Project/Scripts/Core/SwallowContainer.cs` |
| 随机肉鸽候选 | `Assets/_Project/Scripts/Skills/RogueSkillManager.cs` |
| 敌人数值与死亡 | `Assets/_Project/Scripts/Enemy/EnemyData.cs`、`EnemyBase.cs` |
| 敌人 Addressables 内容入口 | `Assets/_Project/Scripts/Enemy/EnemyContentDefinition.cs` |
| 敌人、动画、Atlas 与分组构建 | `Assets/Editor/EnemyContentBuilder.cs` |
| 触碰回血掉落 | `Assets/_Project/Scripts/Core/BloodDrop.cs` |
| 通用影子 | `Assets/_Project/Scripts/Core/GroundShadow.cs` |
| 环境物品 | `Assets/_Project/Scripts/Core/EnvironmentItemSpawner.cs` |
| 右上角小地图 | `Assets/_Project/Scripts/UI/FloorMinimapUI.cs` |
| 存档 | `Assets/_Project/Scripts/Save/SaveGameService.cs` |

通关会把当前存档写入历史记录（通关时间、消耗血量），随后清空活动槽；活动槽初始显示 3 个，前三个占满后扩展到第 4 个，再扩展到第 5 个并使用可滚动列表。

## 7. 资源与层级

| 名称 | 路径或编号 | 用途 |
| --- | --- | --- |
| 房间原图 | `Assets/Art/Tilesets/new_map.png` | 手工切分的墙角、开门、墙和连续地板条带 Sprite |
| 房间 Tile | `Assets/Resources/Map/Tiles/new_map*.asset` | 运行时按语义逐格 Resources 加载；地板共 5 个连续切片 |
| 可吸入层 | Layer 6 `inhaleableLayer` | 尸体与环境物品 |
| 掉落层 | Layer 7 `Drops` | 触碰回血，不参与吸入 |
| 影子层 | Layer 8 `Shadows` | 椭圆地面阴影 |

从 `new_map.png` 当前 Sprite 切片生成语义 Tile 并清理旧场景节点的菜单入口为 `Tools/DevouringBeast/Rebuild Room Tilemap and Clean Scene`。地板条带切片由 `new_map.png.meta` 维护，构建工具会生成对应 Tile 资源，但不会创建第二张地图纹理。

## 8. 性能与维护约束

- 敌人、能量球、命中特效、环境物品和血量掉落均使用对象池。
- 20 种敌人各自拥有一个 `SpriteAtlas`，位于对应 `Assets/Art/Sprites/Enemies/<Enemy>/Atlas`，并关闭 `Include in Build`。
- 每个敌人只有一个公开 Addressables 入口 `Enemy/<Enemy>/Content`。入口定义强引用 Prefab、EnemyData 和 SpriteAtlas，动画、材质及源 Sprite 作为依赖随同一个原子包加载。
- Addressables 使用 `Group_Minions`、`Group_Elites`、`Group_Bosses`，分别包含 8、7、5 个内容定义；三个组均为 `PackSeparately`，不向 `Default Local Group` 放入敌人条目。
- `WaveManager` 逐条持有内容定义 handle，场景销毁时按 handle 释放。生成资产不放入 `Resources`，避免 Player 内置资源与 Addressables 重复打包。
- 这里的合批来自运行时已加载的真实 Sprite Atlas 纹理；仅创建一个关闭 `Include in Build` 的 Atlas、却不通过 Addressables 依赖加载它，并不能让独立纹理自动合批。
- 敌人继续采用分帧生成，默认每帧初始化 1 个，避免进房尖峰。
- 吸力检测使用预分配数组和 NonAlloc API。
- 地虫钻地完成 0.5 秒钻入动画后隐藏 1 秒，在当前房间随机位置以 0.5 秒反向采样动画钻出，出土后抖动并立即发射子弹。
- 地图布局不等待敌人 Addressables 内容；房间与食物先构建，战斗生成协程在敌人内容就绪后开始。
- 游戏重新开始并重载场景时，只以新 `GameScene` 内的 `FloorMapManager` 作为已初始化依据，避免旧场景待销毁实例阻止地图重建。
- 小怪出场烟雾使用共享材质的粒子对象池，避免为一次性特效创建大量材质和渲染器。
- 怪物死亡效果由各自的 `EnemyData.deathEffect` 配置；肉团、大肉团和肉山每次受伤都会按当前阈值进度膨胀，跨过阈值立即触发对应效果并复原，单次伤害跨越多个阈值会逐次触发。
- 小撒旦、撒旦、肉山和蜘蛛使用独立 `Visual` 子节点完成离地运动，物理根节点不随视觉高度偏移；起飞缩小影子，下降时影子恢复。肉山会完整飞出屏幕，横向换位时保持屏外高度，只有落地完成且碰撞体接触玩家时才结算压扁伤害。
- 撒旦系火球从房间与相机上边界之外快速落下，使用放大的火球、拖尾、落点影子和地面火焰；命中后隐藏火球、在撒旦攻击中从落点发射四弹，并生成持续 2 秒、通过 Trigger 与 NonAlloc 重叠检测共同判定伤害的灼烧区。
- 骷髅和骷髅侠假死后禁用 Animator 并保持第 `013` 帧；小撒旦变身期间无敌，结束后保持二阶段第 `002` 帧，二阶段冲刺分别播放准备、循环和结束动画。变身与假死开始时统一清除中毒、燃烧、减速、眩晕和侵蚀。
- 敌人血条与状态图标使用精灵的逻辑启用状态控制，隐身或地虫遁地时同步隐藏、出现时同步恢复；冲刺朝向按本次位移方向立即刷新。Boss 生成坐标固定为房间中心。
- 阴暗每承受两次伤害会连续冲刺 3 至 7 次：冲刺前播放 `002-003` 准备帧，冲刺中循环播放 `004-009`；小撒旦二阶段素材默认朝右，向右滑行不翻转、向左滑行翻转。
- 天使不再删除已获得的非 Faith 技能，其无消耗能量球与普通吐出共用完整技能快照；天使状态下的候选技能仍继续执行前置校验。
- 右上角小地图按楼层实际空间位置显示全部 10 个房间；当前房间带玩家标记，已探索/已清理房间高亮，相邻未探索房间描边提示，其余房间置灰。
- 高频组件引用在 `Awake`/`Start` 缓存；禁止在 `Update`/`FixedUpdate` 中反复 Find。
- 玩家、吸入、吐出、食物、怪物和楼层的数值真源统一位于 `Assets/_Project/Config`；`Generated`、场景和 Prefab 不维护玩法数值。
- Player Build 和敌人内容重建前必须通过 `GameConfigValidator`，禁止缺失、重复或非法配置进入运行时。
- 性能结论必须来自目标 Android 真机的 Profiler、Memory Profiler 和 Frame Debugger。

## 9. 当前验证基线

2026-08-07 已确认：

- `dotnet build DevouringBeast.sln --no-restore`：0 Error，只有 1 个既有未使用字段警告。
- Unity 脚本导入和域重载成功，Console 为 0 Error。
- 敌人内容构建器可重复执行；20 个内容定义全部有效，20 个 Atlas 共包含 343 个源 Texture，且全部关闭 `Include in Build`。
- Addressables Windows 内容构建成功：普通、精英、Boss 分别输出 8、7、5 个独立内容 Bundle，默认组没有敌人条目。
- Play Mode 成功通过 `EnemyContent_All` 加载 20 个原子内容包；当前 7 个活动敌人的 Sprite 全部由已加载 Atlas 绑定，同类敌人共享同一 Atlas 纹理，Console 为 0 Error / 0 Warning。
- 编辑器当前整帧快照为 24 Draw Calls / 24 Batches；其中仍包含地图、UI、影子和其他渲染内容，不能当作 Android 真机结论。
- Play Mode 成功创建 `FloorMapManager`、`FloorRooms` 和 `ActiveRoomCollisions`，日志确认每层生成 10 房、初始普通、精英 1、Boss 1。
- `new_map.png` 的墙角、墙、开启门和地板切片由 16 x 9 Tilemap 连续拼成房间并覆盖固定游戏视口；角色、敌人和环境物品位于地图上方。
- 编辑器 Game 视图统计约 62 FPS；这不是 Android 真机性能结论。
- 2026-08-09 行为探针确认：肉团 20% 受伤时按进度放大、单次 60% 对大肉团触发两次、单次 30% 对肉山触发三次，触发后均复原；骷髅保持 `013`，小撒旦保持 `002` 并进入二阶段滑行动画；灼烧区可扣血，隐身血条可隐藏/恢复，Gloomy 冲刺朝向正确，Boss 中心误差为 0，肉山屏外高度和落地接触判定正确。
- 2026-08-10 行为探针确认：恶魔吸入不会被接触中断且可击杀存活怪物；阴暗冲刺循环实际播放 `GloomyDashLoop`；小撒旦向右/向左滑行分别为 `flipX=false/true`；小地图生成 10 个房间，切换相邻房间后记录 2 个已探索房间且只有 1 个当前玩家标记。

仍需人工长局回归：逐房清理与往返、60/120 秒危急状态、Boss 后继续探索、整心部分消耗、跨房保留与换层清理、Android 真机性能。

## 10. 下次会话规则

1. 先读本文，再按任务读取对应核心脚本。
2. 先执行 `git status --short`；工作区可能包含用户未提交修改，禁止覆盖或回退无关内容。
3. 高风险玩法修改至少执行一次 Play Mode 回归。
4. 修改本文时直接更新对应章节，不追加会话流水账。

---

_项目：DevouringBeast_
