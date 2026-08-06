# DevouringBeast - 项目入口与会话交接

> 最后更新：2026-07-31
>
> Unity：2022.3.62f3c1，URP 2D
>
> 目标平台：Android 横屏，固定 16:9 游戏视口

本文记录当前有效的玩法规则、关键实现入口和验证基线。代码、场景和 Unity 运行结果始终是最终事实来源。

## 1. 文档导航

| 文档 | 用途 |
| --- | --- |
| [readme.md](readme.md) | 当前项目入口与交接摘要 |
| [SESSION_CONTEXT.md](SESSION_CONTEXT.md) | 系统实现、资源与维护约束 |
| [DESIGN.md](DESIGN.md) | 当前玩法设计，不包含历史方案 |

## 2. 核心循环

```text
探索房间 -> 进门锁门 -> 清除敌人 -> 开门并拾取补给
          -> 吸入尸体/物品 -> 吐出或吞噬 -> 随机肉鸽三选一
          -> 击败 Boss -> 可继续探索或进入下一层
```

- 存活敌人只能受到吸力伤害，尸体和环境物品可被吸入口中。
- 吞噬只累计质量进度，不再记录物种、标签或学派占比。
- 升级候选从所有当前合法技能中等权随机，仍统一执行前置、满级、Faith 互斥和形态收益过滤。
- 玩家不会再根据吞噬内容改变颜色。

## 3. 楼层、房间与相机

- 每层生成恰好 10 个大小相同、上下左右相邻且连通的房间。
- 初始房固定为普通房；其余房间在生成时固定分配 1 个精英房和 1 个 Boss 房。
- 不生成隐藏门或隐藏房间。
- 房间世界尺寸为 `32 x 18`，相机正交尺寸为 `9`，固定 16:9 视口；相机不再跟随玩家在大地图中滚动。
- 进入未清理房间时立即封锁所有连通门并生成本房敌人；全部击杀后开门。
- 已清理房间不会再次生成敌人，可随时返回探索。
- Boss 房清理后生成下一层入口；入口不会强制传送，玩家仍可清理或探索本层其他房间。
- 进入下一层后重新生成一套全新的 10 房间布局。

地图视觉使用 `Assets/Art/Tilesets/new_map.png` 的手工切片：`new_map1` 为默认朝向左上的墙角、`new_map2` 为默认位于下墙的开启门、`new_map3` 为默认位于上墙的墙壁；地板使用从原图同一条连续区域切出的 `new_map4`、`new_map4_1` 至 `new_map4_4`。运行时由 `FloorMapManager` 创建 `FloorRooms/RoomTilemap`，以 16 x 9 网格绘制每个房间。地板条带先顺序铺设，再以水平往返镜像和逐行垂直镜像延展，使每条瓦片接缝两侧采样同一源像素边缘，避免非无缝手绘纹理产生方块拼接感。墙角、墙和门仍按四边方向旋转；关闭状态下连通门位显示 `new_map3`，清房后才替换为 `new_map2`。角色、敌人和环境生成范围从可视房间边缘内缩 1.5 世界单位。旧 `ArenaGrid` 不再参与运行。

## 4. 房间战斗与危急状态

- 普通房和精英房倒计时为 60 秒，Boss 房为 120 秒。
- 倒计时最后 3 秒使用红色填充；进入 Boss 房后，其下方固定显示复用同一底框和红色填充素材的 Boss 总血条，离开或清理 Boss 房后隐藏。
- 计时只属于当前房间，不再按时间自动生成下一波。
- 倒计时归零时房门仍保持关闭，屏幕进入红色危急状态。倍速由 1.15 倍开始连续增长至 1.75 倍；红光以低谷接近熄灭、峰值短促的警告节奏持续闪烁，峰值透明度随当前倍速从 0.08 连续增长至 0.26，达到上限后保持强度但继续闪烁。覆盖层位于 HUD 后方。
- 危急状态每 5 秒强化一次所有存活敌人的攻击、移动、范围、频率和吸入抗性；当前生命值不会重置。进入危急状态时全局速度从 1.15 倍开始，每 10 秒提高 0.05，最高 1.75 倍，敌我双方、动画和投射物同步加速。
- 只有消灭全部敌人才能结束危急状态并开门。
- 房间清理后停止补充新的可吸入环境物品，已经生成的物品保留至被使用或离开当前楼层。
- 天使和恶魔通过击杀获得升级质量，获得量固定为被击杀敌人质量的 2 倍；重复升级只增强各自其他成长项，不会继续叠加击杀质量倍率。
- 主操作键按住期间即使已有物品进入口中，按钮仍保持按下前的吸入图标；真正松开后才根据口中内容切换为吐出图标，即使吸入先达到时长上限也不会提前切换。
- 长按吸入或蓄力吐出时，主按钮显示红色脉冲；吸力或蓄力达到上限后保持红色，操作结束后恢复。蓄力音效每次蓄力只播放一遍，不循环。
- 教皇拥有蓄力技能时，教化按钮支持长按蓄力；松开后再吞噬并发射教化球，蓄力伤害增幅和大能量球音效正常生效。
- 肉鸽选择期间禁止产生新的战斗/移动音效，但不会截断已触发的吞噬、教化和升级音效；`rogue_select` 作为关键 UI 音效不受抑制。
- `WaveManager.CurrentWave` 仅为旧 UI/存档兼容入口，其运行语义已改为当前楼层。

## 5. 掉落物与影子层

- `Drops` 为血量掉落专用物理层。血量掉落不再拥有 `InhaleableItem`，不会被吸力检测。
- 玩家触碰血量掉落时自动治疗，且不会超过当前生命上限。
- 半心恢复 1 点；整心恢复 2 点。玩家只缺 1 点时触碰整心，只消耗 1 点并把掉落物切换为半心图标留在原地。
- 血量掉落不再计时闪烁或消失，在当前楼层内跨房间保留；只有进入下一层时统一回池。
- `Shadows` 为影子专用物理层。玩家、敌人、环境物品、血量掉落和下一层入口使用通用椭圆黑影。
- 敌人、物品或入口开始落地时先显示小影子，并随落地过程扩大到正常尺寸。

## 6. 主要代码入口

| 模块 | 入口 |
| --- | --- |
| 楼层布局、房间门、换层 | `Assets/_Project/Scripts/Managers/FloorMapManager.cs` |
| 房间战斗、计时、危急强化、敌人池 | `Assets/_Project/Scripts/Managers/WaveManager.cs` |
| 战斗配置 | `Assets/_Project/Scripts/Managers/WaveConfig.cs` |
| 地图边界与固定相机 | `Assets/_Project/Scripts/Core/MapBounds.cs`、`CameraFollow.cs` |
| 地图 Tile 资源构建 | `Assets/Editor/ArenaTilemapBuilder.cs` |
| 吞噬进度 | `Assets/_Project/Scripts/Core/SwallowContainer.cs` |
| 随机肉鸽候选 | `Assets/_Project/Scripts/Skills/RogueSkillManager.cs` |
| 敌人数值与死亡 | `Assets/_Project/Scripts/Enemy/EnemyData.cs`、`EnemyBase.cs` |
| 触碰回血掉落 | `Assets/_Project/Scripts/Core/BloodDrop.cs` |
| 通用影子 | `Assets/_Project/Scripts/Core/GroundShadow.cs` |
| 环境物品 | `Assets/_Project/Scripts/Core/EnvironmentItemSpawner.cs` |
| 存档 | `Assets/_Project/Scripts/Save/SaveGameService.cs` |

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
- 敌人继续采用分帧生成，默认每帧初始化 1 个，避免进房尖峰。
- 吸力检测使用预分配数组和 NonAlloc API。
- 高频组件引用在 `Awake`/`Start` 缓存；禁止在 `Update`/`FixedUpdate` 中反复 Find。
- 场景和 Prefab 序列化值可能覆盖脚本默认值，调整配置时必须同时检查资产。
- 性能结论必须来自目标 Android 真机的 Profiler、Memory Profiler 和 Frame Debugger。

## 9. 当前验证基线

2026-07-31 已确认：

- `dotnet build DevouringBeast.sln --no-restore`：0 Error，只有 2 个既有未使用字段警告。
- Unity 脚本导入和域重载成功，Console 为 0 Error。
- Play Mode 成功创建 `FloorMapManager`、`FloorRooms` 和 `ActiveRoomCollisions`，日志确认每层生成 10 房、初始普通、精英 1、Boss 1。
- `new_map.png` 的墙角、墙、开启门和地板切片由 16 x 9 Tilemap 连续拼成房间并覆盖固定游戏视口；角色、敌人和环境物品位于地图上方。
- 编辑器 Game 视图统计约 62 FPS；这不是 Android 真机性能结论。

仍需人工长局回归：逐房清理与往返、60/120 秒危急状态、Boss 后继续探索、整心部分消耗、跨房保留与换层清理、Android 真机性能。

## 10. 下次会话规则

1. 先读本文，再按任务读取对应核心脚本。
2. 先执行 `git status --short`；工作区可能包含用户未提交修改，禁止覆盖或回退无关内容。
3. 高风险玩法修改至少执行一次 Play Mode 回归。
4. 修改本文时直接更新对应章节，不追加会话流水账。

---

_项目：DevouringBeast_
