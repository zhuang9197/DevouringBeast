# DevouringBeast - 系统实现上下文

> 当前实现基线：2026-07-31

## 1. 场景与状态

场景流程为 `LoadScene -> MenuScene -> GameScene`。`GameManager` 管理 Playing、Paused、RogueChoosing 和 GameOver；肉鸽选择与阵亡会暂停战斗逻辑和普通 SFX，但 BGM 保持独立控制。

`FloorMapManager` 无需在场景中手工挂载：首次加载由 `RuntimeInitializeOnLoadMethod(AfterSceneLoad)` 注入，常驻的 `GameManager.OnSceneLoaded` 还会在每次重新加载 `GameScene` 时再次确保实例存在，因此阵亡后重新开始也会重建地图和首房敌人。它等待 `WaveManager.IsReady` 后生成第一层。

## 2. 楼层模型

- 楼层包含 10 个唯一网格坐标。
- 每个新房间都从已有房间的四邻域扩展，因此布局始终连通。
- 索引 0 是初始普通房。
- 距离初始房最远的房间固定为 Boss 房；另一个非初始、非 Boss 房固定为精英房。
- 房间状态保存 `Cell`、`RoomKind` 和 `Cleared`，在当前层内往返不会丢失清理状态。
- 房间中心为 `floorOrigin + cell * roomSize`，当前基准原点 `(40,40)`、尺寸 `(32,18)`。

当前层完整生成后，`FloorMapManager` 创建 `FloorRooms/RoomTilemap`，按每房间 16 x 9 网格绘制 `new_map1` 墙角、`new_map3` 墙和地板条带。地板条带由 `new_map4`、`new_map4_1` 至 `new_map4_4` 五个相邻源区域组成；横向采用“正序 + 反向镜像”的 ping-pong 铺法，纵向逐行镜像，因此任意相邻格共享同一源纹理边缘，不会暴露非无缝手绘切片的方块边界。关闭状态下连通方向的门位仍使用墙；清房后替换为按方向旋转的 `new_map2` 开启门。旧场景 `ArenaGrid` 不再参与运行。

## 3. 门、碰撞与移动

只有与另一个房间相邻的方向会生成门触发器。无邻居方向为完整墙；有邻居方向拆分为两段墙和一个门洞。

- 未清理房间：门洞的 `DoorBlocker` 启用。
- 已清理房间：门阻挡关闭，`RoomDoorTrigger` 允许切换房间。
- 门触发区只在玩家移动输入指向门外时切换房间；沿墙切向经过不会误触发，已站在门边时转向门外可通过 `OnTriggerStay2D` 立即进入。
- 换房后玩家放置在目标房间门触发区之外，且对侧门要求相反方向输入，避免已清理房间之间来回跳转。
- `MapBounds.ConfigureRoom` 将当前活动范围从房间四边各内缩 1.5 世界单位；玩家、敌人与环境生成复用该边界，避免进入有厚度的墙体瓦片。

## 4. 固定相机

`CameraFollow.SetRoom` 会停止跟随目标，将相机立即放到房间中心，并设置正交尺寸为房间高度的一半。Camera Rect 按 16:9 做 letterbox/pillarbox，避免不同屏幕比例扩大可见地图范围。

## 5. 房间战斗状态机

`WaveManager.Phase` 为 `Idle -> Spawning -> Fighting -> Cleared`。

1. `BeginRoom` 接收房间类型、楼层、中心、尺寸和清理回调。
2. 敌人继续按来源 Prefab 池化，并以 `spawnBatchSize` 分帧创建。
3. 普通房生成普通敌人；精英房生成普通敌人与精英敌人；Boss 房生成普通敌人与 Boss。
   `WaveManager` 单独跟踪本次遭遇中的 Boss 生命总值；`WaveUI` 只在 Boss 房生成/战斗阶段于倒计时条下显示红色总血条。
4. 计时归零只调用 `EnterCrisis`，不生成敌人、不结束房间、不打开门。
5. 危急状态每 `crisisEmpowerInterval` 秒调用 `EnemyBase.EmpowerForCrisis`；`Time.timeScale` 按原“每 10 秒增加 0.05”的速率连续提高，默认从 1.15 倍增长至 1.75 倍，清房或离开遭遇时恢复 1 倍。红色覆盖层使用高锐度脉冲，峰值透明度按当前倍速从 0.08 映射至 0.26；达到上限后只停止加重、不停止闪烁，Canvas 排在 HUD 后面。
6. 房间清理后 `EnvironmentItemSpawner` 停止补充新物品，但不会回收已经生成的可吸入物；进入未清理房间后恢复补充。
6. `_allSpawned && _enemiesRemaining <= 0` 时才执行清理回调。

怪物脚本不再保存 `EnemyType` 或物种标签。房间规则决定使用哪个 Prefab 池、伤害加值和吞噬质量，运行时 `EnemyData` 只保存最终数值。

## 6. 吞噬与升级

`SwallowContainer` 仅维护口中物品与 `CurrentMass`。吞噬时累加质量、回收口中物品并触发升级检查；升级扣除需求并保留溢出质量。

需求曲线为 `35 / 50 / 65 / 80 / 100 / 110 / 121 ...`。

`RogueSkillManager.GetRandomChoices` 先调用统一的 `CanOffer`，再打乱所有合法候选并取最多 3 个。技能自身的 `RogueSchool` 仍用于目录和 Faith 规则，但不接受吞噬内容偏好。

天使和恶魔监听敌人死亡事件，击杀时直接增加 `EnemyBase.MassValue * 2` 的升级质量。倍率为固定常量，不读取技能等级，因此重复升级不会继续翻倍。

`GameplayHudController` 在 `InputManager.IsPrimaryActionHeld` 为真时冻结主按钮的口中状态显示，松开后再同步 `SwallowContainer.HasItems`；因此吸入即使先自动达到时长上限也不会提前换图标。同时根据 `PlayerInhale.IsSuctionMaxed` 和 `PlayerSpit.IsChargeMaxed` 驱动长按红色脉冲/满值常亮。蓄力声音通过可停止的单次 AudioSource 播放，不使用循环模式。

教皇且拥有蓄力技能时，吞噬按钮按下只开始蓄力，松开后才执行延迟吞噬；`SpitTeachingBall` 在清空口中物品后继续读取本次蓄力进度。吞噬结算先播放吞噬/教化音效，再调用 `CheckAndNotify` 打开升级选择。选择期间只阻止新的普通 SFX，不停止已经播放的一次性音效，并单独放行 `RogueSelect`。

## 7. 掉落物

`BloodDrop` 有两个静态池和一个当前层活动集合：

- 半心：剩余治疗量 1。
- 整心：剩余治疗量 2。
- 触碰时按缺失生命与剩余治疗量的较小值治疗。
- 整心只消耗 1 点后改用半心 Sprite，并进入半心池归属。
- `ReleaseFloorDrops` 只在构建下一层时调用。

两个血量 Prefab 均位于 Layer 7，不再包含 `InhaleableItem`。Faith Angel/Demon 的直接治疗分支不生成掉落物。

## 8. 影子

`GroundShadow.Ensure` 为对象增加一个子 SpriteRenderer。椭圆 Sprite 只在首次请求时程序化生成并缓存，不会为每个对象创建纹理。

影子接入点：

- 玩家：楼层管理器初始化。
- 敌人：每次从池中生成时调用 `BeginLanding`。
- 环境物品：创建时挂载，复用生成时调用 `BeginLanding`。
- 血量掉落和下一层入口：生成时调用 `BeginLanding`。

## 9. 对象池与重置

- 敌人回池前解除 `OnDeath`，恢复标准缩放并停止活动状态。
- `EnemyBase.Initialize` 重置生命、AI、Animator、Collider、状态、血条和尸体表现。
- 血量掉落回池时从 `ActiveDrops` 移除，换层统一释放。
- 环境物品换层时先释放所有活动成员，再按单房间容量补充。

## 10. 存档兼容

`SaveSlotData.completedWave` 字段暂时保留，避免破坏已有 JSON 存档；它现在表示“已完成楼层”。进入下一层入口时写入当前层号。旧 `CurrentWave` API 同理仅用于兼容 UI 和掉落概率代码。

## 11. 验证清单

- 编译：程序集 0 Error；当前仅有 2 个既有未使用字段警告。
- 地图：Play Mode 已确认 `new_map` 语义 Tile 构成的 16 x 9 房间连续覆盖固定视口；五段连续地板切片的镜像延展不再出现逐格硬接缝，墙角和墙按边缘方向正确旋转。
- 结构：每层 10 房，初始普通，精英 1，Boss 1，全部连通。
- 战斗：未清理锁门，清敌开门，清理房不重刷。
- 计时：普通/精英 60 秒，Boss 120 秒，归零后仅进入危急状态。
- 掉落：不可吸入、触碰回血、不溢出、整心可部分消耗、跨房保留、换层清理。
- 影子：落地前出现并从小变大，对象池复用后尺寸正常。
- 性能：Android 真机验证 CPU、GC、GPU、内存和批次。
