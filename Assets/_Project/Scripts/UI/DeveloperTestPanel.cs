using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DevouringBeast
{
    [DisallowMultipleComponent]
    public sealed class DeveloperTestPanel : MonoBehaviour
    {
        private EnemyArchetype[] _archetypes;
        private int _selectedIndex;
        private Text _selectionText;
        private Text _statusText;
        private Text _trackedText;
        private Button _spawnButton;
        private EnemyBase _trackedEnemy;
        private readonly Dictionary<string, InputField> _fields = new();
        private float _nextRefreshTime;

        public static DeveloperTestPanel EnsureFor(FloorMapManager map)
        {
            DeveloperTestPanel existing = map.GetComponent<DeveloperTestPanel>();
            return existing != null ? existing : map.gameObject.AddComponent<DeveloperTestPanel>();
        }

        private void Start()
        {
            _archetypes = (EnemyArchetype[])Enum.GetValues(typeof(EnemyArchetype));
            BuildUi();
            RefreshSelection();
            RefreshAllFields(true);
        }

        private void Update()
        {
            if (_spawnButton == null) return;
            bool ready = WaveManager.Instance != null && WaveManager.Instance.IsReady;
            _spawnButton.interactable = ready;
            if (_statusText != null)
                _statusText.text = ready ? "测试模式" : "正在加载怪物资源...";

            EnsureConfigurationBindings();

            if (Time.unscaledTime >= _nextRefreshTime && !HasFocusedField())
            {
                RefreshAllFields(false);
                _nextRefreshTime = Time.unscaledTime + 0.25f;
            }
        }

        private void BuildUi()
        {
            Canvas canvas = GameObject.Find("Canvas")?.GetComponent<Canvas>() ?? FindFirstObjectByType<Canvas>();
            if (canvas == null) return;

            GameObject picker = CreatePanel("DeveloperTestPanel", canvas.transform, new Vector2(18f, -45f),
                new Vector2(330f, 330f), false);
            Text title = CreateText("Title", picker.transform, 20, TextAnchor.MiddleCenter);
            SetRect(title.rectTransform, new Vector2(0.04f, 0.86f), new Vector2(0.96f, 0.98f));
            title.text = "怪物与技能测试";

            Button previous = CreateButton("Previous", picker.transform, new Vector2(0.05f, 0.67f),
                new Vector2(0.19f, 0.82f), "<");
            Button next = CreateButton("Next", picker.transform, new Vector2(0.81f, 0.67f),
                new Vector2(0.95f, 0.82f), ">");
            _selectionText = CreateText("Selection", picker.transform, 16, TextAnchor.MiddleCenter);
            SetRect(_selectionText.rectTransform, new Vector2(0.2f, 0.67f), new Vector2(0.8f, 0.82f));
            previous.onClick.AddListener(() => ChangeSelection(-1));
            next.onClick.AddListener(() => ChangeSelection(1));

            _spawnButton = CreateButton("Spawn", picker.transform, new Vector2(0.05f, 0.49f),
                new Vector2(0.95f, 0.64f), "生成所选怪物");
            Button levelUp = CreateButton("LevelUp", picker.transform, new Vector2(0.05f, 0.31f),
                new Vector2(0.47f, 0.45f), "升级");
            Button resetProgress = CreateButton("ResetProgress", picker.transform, new Vector2(0.53f, 0.31f),
                new Vector2(0.95f, 0.45f), "重置进度");
            _spawnButton.onClick.AddListener(SpawnSelected);
            levelUp.onClick.AddListener(TriggerLevelUp);
            resetProgress.onClick.AddListener(ResetProgress);

            Toggle damageToggle = CreateToggle("DamageToggle", picker.transform,
                new Vector2(0.05f, 0.18f), new Vector2(0.54f, 0.28f), "玩家受到伤害");
            damageToggle.isOn = GameManager.Existing == null || GameManager.Existing.TestDamageEnabled;
            damageToggle.onValueChanged.AddListener(value => GameManager.Instance.SetTestDamageEnabled(value));
            Button fullHealth = CreateButton("FullHealth", picker.transform, new Vector2(0.58f, 0.18f),
                new Vector2(0.95f, 0.28f), "重置血量");
            fullHealth.onClick.AddListener(RestoreFullHealth);

            _statusText = CreateText("Status", picker.transform, 13, TextAnchor.MiddleCenter);
            SetRect(_statusText.rectTransform, new Vector2(0.05f, 0.04f), new Vector2(0.95f, 0.14f));

            BuildConfigurationPanel(canvas.transform);
        }

        private void BuildConfigurationPanel(Transform canvas)
        {
            GameObject panel = CreatePanel("DeveloperConfigurationPanel", canvas, new Vector2(-18f, -15f),
                new Vector2(470f, 760f), true);
            Text title = CreateText("Title", panel.transform, 20, TextAnchor.MiddleCenter);
            SetRect(title.rectTransform, new Vector2(0.04f, 0.92f), new Vector2(0.96f, 0.99f));
            title.text = "运行时配置";

            _trackedText = CreateText("Tracked", panel.transform, 14, TextAnchor.MiddleCenter);
            SetRect(_trackedText.rectTransform, new Vector2(0.04f, 0.86f), new Vector2(0.96f, 0.92f));
            _trackedText.text = "最新生成怪物：无";

            GameObject viewport = new("ConfigurationViewport", typeof(RectTransform), typeof(RectMask2D), typeof(ScrollRect));
            viewport.transform.SetParent(panel.transform, false);
            SetRect(viewport.GetComponent<RectTransform>(), new Vector2(0.04f, 0.16f), new Vector2(0.93f, 0.85f));

            GameObject content = new("ConfigurationContent", typeof(RectTransform), typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            RectTransform contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.sizeDelta = Vector2.zero;
            VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 4f;
            layout.padding = new RectOffset(8, 8, 6, 8);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            ScrollRect scroll = viewport.GetComponent<ScrollRect>();
            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.content = contentRect;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 8f;

            GameObject scrollbarObject = new("ConfigurationScrollbar", typeof(RectTransform), typeof(Image),
                typeof(Scrollbar));
            scrollbarObject.transform.SetParent(panel.transform, false);
            SetRect(scrollbarObject.GetComponent<RectTransform>(), new Vector2(0.94f, 0.16f),
                new Vector2(0.965f, 0.85f));
            scrollbarObject.GetComponent<Image>().color = new Color(0.12f, 0.14f, 0.17f, 0.9f);
            GameObject handleObject = new("Handle", typeof(RectTransform), typeof(Image));
            handleObject.transform.SetParent(scrollbarObject.transform, false);
            SetRect(handleObject.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);
            Image handleImage = handleObject.GetComponent<Image>();
            handleImage.color = new Color(0.45f, 0.5f, 0.58f, 1f);
            Scrollbar scrollbar = scrollbarObject.GetComponent<Scrollbar>();
            scrollbar.handleRect = handleObject.GetComponent<RectTransform>();
            scrollbar.targetGraphic = handleImage;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.value = 1f;
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

            AddSection(content.transform, "玩家角色");
            AddField(content.transform, "player.maxHealth", "血量", 1f);
            AddField(content.transform, "player.moveSpeed", "移动速度", 0.01f);
            AddField(content.transform, "player.suction", "吸力", 0f);
            AddField(content.transform, "player.levelBaseMass", "升级初始质量", 1f);
            AddField(content.transform, "player.levelIncrement1To5", "1~5级递增质量", 0f);
            AddField(content.transform, "player.levelIncrement6To10", "6~10级递增质量", 0f);
            AddField(content.transform, "player.levelIncrement11To15", "11~15级递增质量", 0f);
            AddField(content.transform, "player.levelIncrement16To20", "16~20级递增质量", 0f);
            AddField(content.transform, "player.levelIncrement21Plus", "21级以上递增质量", 0f);
            AddField(content.transform, "player.energyDamage", "能量球伤害", 0f);
            AddField(content.transform, "player.suctionDamage", "吸力伤害倍率", 0f);
            AddField(content.transform, "player.inhaleRadius", "吸力范围", 0f);
            AddField(content.transform, "player.inhaleAngle", "吸力角度", 0f);

            AddSection(content.transform, "最新生成怪物");
            AddField(content.transform, "enemy.maxHealth", "血量", 1f);
            AddField(content.transform, "enemy.massValue", "吞噬质量", 0f);
            AddField(content.transform, "enemy.moveSpeed", "移动速度", 0f);
            AddField(content.transform, "enemy.attackDamage", "接触/攻击伤害", 0f);
            AddField(content.transform, "enemy.attackCooldown", "技能触发间隔", 0.01f);
            AddField(content.transform, "enemy.attackRange", "攻击范围", 0f);
            AddField(content.transform, "enemy.detectRange", "追踪范围", 0f);

            AddSection(content.transform, "投射物与特殊攻击");
            AddField(content.transform, "enemy.aimedSpeed", "追踪子弹速度", 0f);
            AddField(content.transform, "enemy.radialSpeed", "环形子弹速度", 0f);
            AddField(content.transform, "enemy.radialCount", "环形子弹数量", 0f);
            AddField(content.transform, "enemy.radialAngle", "环形子弹角度", 0f);
            AddField(content.transform, "enemy.specialCooldown", "特殊技能间隔", 0.01f);
            AddField(content.transform, "enemy.specialWaves", "特殊攻击波数", 0f);
            AddField(content.transform, "enemy.specialBulletCount", "每波子弹数量", 0f);
            AddField(content.transform, "enemy.specialBulletInterval", "每波子弹间隔", 0.01f);
            AddField(content.transform, "enemy.specialBulletAngle", "每波子弹角度", 0f);
            AddField(content.transform, "enemy.specialBulletAngleStep", "每波旋转角度", 0f);
            AddField(content.transform, "enemy.fireballCount", "火球数量", 0f);
            AddField(content.transform, "enemy.fireballInterval", "火球间隔", 0.01f);
            AddField(content.transform, "enemy.fireballRadialCount", "火球落地子弹数", 0f);

            AddSection(content.transform, "特殊移动与跳跃");
            AddField(content.transform, "enemy.proximityRange", "蜘蛛触发距离", 0f);
            AddField(content.transform, "enemy.jumpSpeed", "蜘蛛跳跃速度", 0f);
            AddField(content.transform, "enemy.spiderJumpInterval", "蜘蛛最短跳跃间隔", 3f);
            AddField(content.transform, "enemy.spiderPreparationMin", "蜘蛛前摇最短时长", 0.1f);
            AddField(content.transform, "enemy.spiderPreparationMax", "蜘蛛前摇最长时长", 0.1f);
            AddField(content.transform, "enemy.dashSpeed", "冲刺速度", 0f);
            AddField(content.transform, "enemy.specialMoveSpeed", "特殊移动速度", 0f);
            AddField(content.transform, "enemy.dashDuration", "跳跃/冲刺时长", 0.01f);
            AddField(content.transform, "enemy.movementCycle", "移动循环时长", 0.01f);
            AddField(content.transform, "enemy.movementActive", "移动激活时长", 0f);
            AddField(content.transform, "enemy.movementIdle", "移动停顿时长", 0f);
            AddField(content.transform, "enemy.actionsPerSpecial", "每几次触发特殊", 0f);
            AddField(content.transform, "enemy.jumpHeight", "跳跃高度", 0f);
            AddField(content.transform, "enemy.takeoffDuration", "起跳时长", 0.01f);
            AddField(content.transform, "enemy.airborneDuration", "空中时长", 0f);
            AddField(content.transform, "enemy.landingDuration", "落地时长", 0.01f);
            AddField(content.transform, "enemy.dashPreparation", "冲刺准备时长", 0f);
            AddField(content.transform, "enemy.dashRecovery", "冲刺恢复时长", 0f);
            AddField(content.transform, "enemy.fireballFallDuration", "火球下落时长", 0f);
            AddField(content.transform, "enemy.offscreenPadding", "飞天屏外距离", 0f);
            AddField(content.transform, "enemy.stateTransition", "阶段切换时长", 0f);
            AddField(content.transform, "enemy.stateHold", "阶段保持时长", 0f);

            AddSection(content.transform, "环绕、游走与受伤触发");
            AddField(content.transform, "enemy.orbitAngularSpeed", "环绕角速度", 0f);
            AddField(content.transform, "enemy.orbitPursuitWeight", "环绕追踪权重", 0f);
            AddField(content.transform, "enemy.orbitTangentWeight", "环绕切向权重", 0f);
            AddField(content.transform, "enemy.orbitSeparationWeight", "环绕避让权重", 0f);
            AddField(content.transform, "enemy.wanderMinInterval", "游走最短间隔", 0f);
            AddField(content.transform, "enemy.wanderMaxInterval", "游走最长间隔", 0f);
            AddField(content.transform, "enemy.wanderTurnAngle", "游走最大转角", 0f);
            AddField(content.transform, "enemy.wanderTurnSpeed", "游走转向速度", 0f);
            AddField(content.transform, "enemy.evasiveTurnSpeed", "规避转向速度", 0f);
            AddField(content.transform, "enemy.healthLossInterval", "受伤触发间隔", 0f);
            AddField(content.transform, "enemy.healthLossMaxTriggers", "受伤最多触发次数", 0f);
            AddField(content.transform, "enemy.healthLossBulletCount", "受伤触发子弹数", 0f);
            AddField(content.transform, "enemy.healthLossSummonMeatball", "受伤召唤肉球(0/1)", 0f);
            AddField(content.transform, "enemy.healthLossMaxScale", "受伤最大膨胀倍率", 1f);
            AddField(content.transform, "enemy.healthLossPulseDuration", "受伤回弹时长", 0.01f);

            Button applyPlayer = CreateButton("ApplyPlayer", panel.transform, new Vector2(0.04f, 0.08f),
                new Vector2(0.48f, 0.14f), "应用玩家配置");
            Button applyEnemy = CreateButton("ApplyEnemy", panel.transform, new Vector2(0.52f, 0.08f),
                new Vector2(0.96f, 0.14f), "应用怪物配置");
            applyPlayer.onClick.AddListener(ApplyPlayerConfiguration);
            applyEnemy.onClick.AddListener(ApplyEnemyConfiguration);
        }

        private void AddSection(Transform parent, string label)
        {
            Text text = CreateText("Section", parent, 15, TextAnchor.MiddleLeft);
            text.text = label;
            text.color = new Color(1f, 0.85f, 0.45f, 1f);
            LayoutElement element = text.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = 28f;
        }

        private void AddField(Transform parent, string key, string label, float minimum)
        {
            GameObject row = new(key, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            row.transform.SetParent(parent, false);
            LayoutElement rowElement = row.GetComponent<LayoutElement>();
            rowElement.preferredHeight = 31f;
            HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 6f;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = true;

            Text labelText = CreateText("Label", row.transform, 13, TextAnchor.MiddleLeft);
            labelText.text = label;
            LayoutElement labelElement = labelText.gameObject.AddComponent<LayoutElement>();
            labelElement.preferredWidth = 175f;

            InputField input = CreateInputField("Value", row.transform);
            input.contentType = InputField.ContentType.DecimalNumber;
            input.onEndEdit.AddListener(_ => ApplyFieldImmediately(key, minimum));
            LayoutElement inputElement = input.gameObject.AddComponent<LayoutElement>();
            inputElement.flexibleWidth = 1f;
            _fields.Add(key, input);
        }

        private void EnsureConfigurationBindings()
        {
            if (_fields.Count > 0) return;

            GameObject panel = GameObject.Find("DeveloperConfigurationPanel");
            if (panel == null)
            {
                Canvas canvas = GameObject.Find("Canvas")?.GetComponent<Canvas>();
                if (canvas != null) BuildConfigurationPanel(canvas.transform);
                return;
            }

            Transform content = panel.transform.Find("ConfigurationViewport/ConfigurationContent");
            if (content == null) return;
            for (int i = 0; i < content.childCount; i++)
            {
                Transform row = content.GetChild(i);
                InputField input = row.GetComponentInChildren<InputField>(true);
                if (input == null || row.name == "Section") continue;
                string key = row.name;
                _fields[key] = input;
                input.onEndEdit.AddListener(_ => ApplyFieldImmediately(key, GetMinimum(key)));
            }

            Button applyPlayer = panel.transform.Find("ApplyPlayer")?.GetComponent<Button>();
            Button applyEnemy = panel.transform.Find("ApplyEnemy")?.GetComponent<Button>();
            applyPlayer?.onClick.AddListener(ApplyPlayerConfiguration);
            applyEnemy?.onClick.AddListener(ApplyEnemyConfiguration);
        }

        private static float GetMinimum(string key)
        {
            return key switch
            {
                "player.maxHealth" or "player.levelBaseMass" or "enemy.maxHealth" or
                    "enemy.healthLossMaxScale" => 1f,
                "player.moveSpeed" or "enemy.attackCooldown" or "enemy.specialCooldown" or
                    "enemy.specialBulletInterval" or "enemy.fireballInterval" or
                    "enemy.dashDuration" or "enemy.movementCycle" or
                    "enemy.takeoffDuration" or "enemy.landingDuration" or
                    "enemy.healthLossPulseDuration" => 0.01f,
                _ => 0f
            };
        }

        private void ChangeSelection(int delta)
        {
            if (_archetypes == null || _archetypes.Length == 0) return;
            _selectedIndex = (_selectedIndex + delta + _archetypes.Length) % _archetypes.Length;
            RefreshSelection();
        }

        private void RefreshSelection()
        {
            if (_selectionText != null && _archetypes != null && _archetypes.Length > 0)
                _selectionText.text = _archetypes[_selectedIndex].ToString();
        }

        private void SpawnSelected()
        {
            WaveManager waves = WaveManager.Instance;
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (waves == null || !waves.IsReady || player == null) return;
            Vector2 position = (Vector2)player.transform.position + Vector2.right * 4f;
            if (MapBounds.Instance != null) position = MapBounds.Instance.ClampPosition(position);
            EnemyBase spawned = waves.SpawnSummoned(_archetypes[_selectedIndex], position);
            if (spawned != null)
            {
                _trackedEnemy = spawned;
                RefreshEnemyFields(true);
            }
        }

        private static void TriggerLevelUp()
        {
            RogueSkillManager skills = RogueSkillManager.Active;
            SwallowContainer container = skills != null ? skills.GetComponent<SwallowContainer>() : null;
            if (container == null || skills.IsChoiceOpen) return;
            container.AddProgress(Mathf.Max(0f, container.RequiredMass - container.CurrentMass));
        }

        private static void ResetProgress()
        {
            RogueSkillManager skills = RogueSkillManager.Active;
            SwallowContainer container = skills != null ? skills.GetComponent<SwallowContainer>() : null;
            skills?.ResetForTesting();
            container?.ResetForTesting();
        }

        private static void RestoreFullHealth()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            player?.GetComponent<PlayerHealth>()?.RestoreFullHealth();
        }

        private void RefreshAllFields(bool force)
        {
            RefreshPlayerFields(force);
            RefreshEnemyFields(force);
        }

        private void RefreshPlayerFields(bool force)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return;
            PlayerHealth health = player.GetComponent<PlayerHealth>();
            PlayerController controller = player.GetComponent<PlayerController>();
            PlayerInhale inhale = player.GetComponent<PlayerInhale>();
            PlayerSpit spit = player.GetComponent<PlayerSpit>();
            SwallowContainer container = player.GetComponent<SwallowContainer>();
            SetField("player.maxHealth", health != null ? health.MaxHealth : 0f, force);
            SetField("player.moveSpeed", controller != null ? controller.MoveSpeed : 0f, force);
            SetField("player.suction", inhale != null ? inhale.MaxSuctionForce : 0f, force);
            SetField("player.levelBaseMass", container != null ? container.LevelRequirementBase : 0f, force);
            SetField("player.levelIncrement1To5", container != null ? container.LevelRequirementIncrement1To5 : 0f, force);
            SetField("player.levelIncrement6To10", container != null ? container.LevelRequirementIncrement6To10 : 0f, force);
            SetField("player.levelIncrement11To15", container != null ? container.LevelRequirementIncrement11To15 : 0f, force);
            SetField("player.levelIncrement16To20", container != null ? container.LevelRequirementIncrement16To20 : 0f, force);
            SetField("player.levelIncrement21Plus", container != null ? container.LevelRequirementIncrement21Plus : 0f, force);
            SetField("player.energyDamage", spit != null ? spit.BaseDamage : 0f, force);
            SetField("player.suctionDamage", inhale != null ? inhale.BaseDamageMultiplier : 0f, force);
            SetField("player.inhaleRadius", inhale != null ? inhale.InhaleRadius : 0f, force);
            SetField("player.inhaleAngle", inhale != null ? inhale.InhaleAngle : 0f, force);
        }

        private void RefreshEnemyFields(bool force)
        {
            if (_trackedEnemy == null)
            {
                if (_trackedText != null) _trackedText.text = "最新生成怪物：无";
                return;
            }
            EnemyData data = _trackedEnemy.Data;
            if (data == null) return;
            if (_trackedText != null)
                _trackedText.text = $"最新生成怪物：{data.displayName}　{(_trackedEnemy.IsDead ? "已阵亡" : "存活")}";
            SetField("enemy.maxHealth", data.maxHealth, force);
            SetField("enemy.massValue", data.massValue, force);
            SetField("enemy.moveSpeed", data.moveSpeed, force);
            SetField("enemy.attackDamage", data.attackDamage, force);
            SetField("enemy.attackCooldown", data.attackCooldown, force);
            SetField("enemy.attackRange", data.attackRange, force);
            SetField("enemy.detectRange", data.detectRange, force);
            SetField("enemy.aimedSpeed", data.aimedProjectileSpeed, force);
            SetField("enemy.radialSpeed", data.radialProjectileSpeed, force);
            SetField("enemy.radialCount", data.radialProjectileCount, force);
            SetField("enemy.radialAngle", data.radialProjectileAngle, force);
            SetField("enemy.specialCooldown", data.behavior != null ? data.behavior.specialAttackCooldown : 0f, force);
            SetField("enemy.proximityRange", data.behavior != null ? data.behavior.proximityRange : 0f, force);
            SetField("enemy.jumpSpeed", data.behavior != null ? data.behavior.jumpSpeed : 0f, force);
            SetField("enemy.spiderJumpInterval", data.behavior != null ? data.behavior.spiderJumpMinimumInterval : 3f, force);
            SetField("enemy.spiderPreparationMin", data.behavior != null ? data.behavior.spiderJumpPreparationRange.x : 0.1f, force);
            SetField("enemy.spiderPreparationMax", data.behavior != null ? data.behavior.spiderJumpPreparationRange.y : 0.5f, force);
            SetField("enemy.dashSpeed", data.behavior != null ? data.behavior.dashSpeed : 0f, force);
            RefreshBehaviorFields(data.behavior, force);
        }

        private void RefreshBehaviorFields(EnemyBehaviorSettings behavior, bool force)
        {
            if (behavior == null) return;
            SetField("enemy.specialWaves", behavior.specialProjectileWaves, force);
            SetField("enemy.specialBulletCount", behavior.specialProjectileCount, force);
            SetField("enemy.specialBulletInterval", behavior.specialProjectileInterval, force);
            SetField("enemy.specialBulletAngle", behavior.specialProjectileAngle, force);
            SetField("enemy.specialBulletAngleStep", behavior.specialProjectileAngleStep, force);
            SetField("enemy.fireballCount", behavior.fireballCount, force);
            SetField("enemy.fireballInterval", behavior.fireballInterval, force);
            SetField("enemy.fireballRadialCount", behavior.fireballRadialBulletCount, force);
            SetField("enemy.specialMoveSpeed", behavior.specialMoveSpeed, force);
            SetField("enemy.dashDuration", behavior.dashDuration, force);
            SetField("enemy.movementCycle", behavior.movementCycleDuration, force);
            SetField("enemy.movementActive", behavior.movementActiveDuration, force);
            SetField("enemy.movementIdle", behavior.movementIdleDuration, force);
            SetField("enemy.actionsPerSpecial", behavior.actionsPerSpecial, force);
            SetField("enemy.jumpHeight", behavior.jumpHeight, force);
            SetField("enemy.takeoffDuration", behavior.takeoffDuration, force);
            SetField("enemy.airborneDuration", behavior.airborneDuration, force);
            SetField("enemy.landingDuration", behavior.landingDuration, force);
            SetField("enemy.dashPreparation", behavior.dashPreparationDuration, force);
            SetField("enemy.dashRecovery", behavior.dashRecoveryDuration, force);
            SetField("enemy.fireballFallDuration", behavior.fireballFallDuration, force);
            SetField("enemy.offscreenPadding", behavior.offscreenPadding, force);
            SetField("enemy.stateTransition", behavior.stateTransitionDuration, force);
            SetField("enemy.stateHold", behavior.stateHoldDuration, force);
            SetField("enemy.orbitAngularSpeed", behavior.orbitAngularSpeed, force);
            SetField("enemy.orbitPursuitWeight", behavior.orbitPursuitWeight, force);
            SetField("enemy.orbitTangentWeight", behavior.orbitTangentWeight, force);
            SetField("enemy.orbitSeparationWeight", behavior.orbitSeparationWeight, force);
            SetField("enemy.wanderMinInterval", behavior.wanderIntervalRange.x, force);
            SetField("enemy.wanderMaxInterval", behavior.wanderIntervalRange.y, force);
            SetField("enemy.wanderTurnAngle", behavior.wanderMaximumTurnAngle, force);
            SetField("enemy.wanderTurnSpeed", behavior.wanderTurnSpeed, force);
            SetField("enemy.evasiveTurnSpeed", behavior.evasiveTurnSpeed, force);
            SetField("enemy.healthLossInterval", behavior.healthLossEffectInterval, force);
            SetField("enemy.healthLossMaxTriggers", behavior.healthLossEffectMaximumTriggers, force);
            SetField("enemy.healthLossBulletCount", behavior.healthLossEffectBulletCount, force);
            SetField("enemy.healthLossSummonMeatball", behavior.healthLossEffectSummonsMeatball ? 1f : 0f, force);
            SetField("enemy.healthLossMaxScale", behavior.healthLossEffectMaximumScale, force);
            SetField("enemy.healthLossPulseDuration", behavior.healthLossEffectPulseDuration, force);
        }

        private void ApplyFieldImmediately(string key, float minimum)
        {
            if (!TryReadFloat(key, minimum, out float value)) return;
            if (key.StartsWith("player.", StringComparison.Ordinal)) ApplyPlayerRuntimeValue(key, value);
            else ApplyEnemyRuntimeValue(key, value);
        }

        private void ApplyPlayerConfiguration()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return;
            PlayerHealth health = player.GetComponent<PlayerHealth>();
            PlayerController controller = player.GetComponent<PlayerController>();
            PlayerInhale inhale = player.GetComponent<PlayerInhale>();
            PlayerSpit spit = player.GetComponent<PlayerSpit>();
            SwallowContainer container = player.GetComponent<SwallowContainer>();
            GameBalanceConfig config = GameBalance.Current;
            if (config == null) return;

            ApplyPlayerRuntimeValue("player.maxHealth", ReadField("player.maxHealth", health != null ? health.MaxHealth : 1f));
            ApplyPlayerRuntimeValue("player.moveSpeed", ReadField("player.moveSpeed", controller != null ? controller.MoveSpeed : 0f));
            ApplyPlayerRuntimeValue("player.suction", ReadField("player.suction", inhale != null ? inhale.MaxSuctionForce : 0f));
            ApplyPlayerRuntimeValue("player.levelBaseMass", ReadField("player.levelBaseMass", container != null ? container.LevelRequirementBase : 1f));
            ApplyPlayerRuntimeValue("player.levelIncrement1To5", ReadField("player.levelIncrement1To5", container != null ? container.LevelRequirementIncrement1To5 : 0f));
            ApplyPlayerRuntimeValue("player.levelIncrement6To10", ReadField("player.levelIncrement6To10", container != null ? container.LevelRequirementIncrement6To10 : 0f));
            ApplyPlayerRuntimeValue("player.levelIncrement11To15", ReadField("player.levelIncrement11To15", container != null ? container.LevelRequirementIncrement11To15 : 0f));
            ApplyPlayerRuntimeValue("player.levelIncrement16To20", ReadField("player.levelIncrement16To20", container != null ? container.LevelRequirementIncrement16To20 : 0f));
            ApplyPlayerRuntimeValue("player.levelIncrement21Plus", ReadField("player.levelIncrement21Plus", container != null ? container.LevelRequirementIncrement21Plus : 0f));
            ApplyPlayerRuntimeValue("player.energyDamage", ReadField("player.energyDamage", spit != null ? spit.BaseDamage : 0f));
            ApplyPlayerRuntimeValue("player.suctionDamage", ReadField("player.suctionDamage", inhale != null ? inhale.BaseDamageMultiplier : 1f));
            ApplyPlayerRuntimeValue("player.inhaleRadius", ReadField("player.inhaleRadius", inhale != null ? inhale.InhaleRadius : 0f));
            ApplyPlayerRuntimeValue("player.inhaleAngle", ReadField("player.inhaleAngle", inhale != null ? inhale.InhaleAngle : 0f));

            config.Player.maxHealth = Mathf.Max(1, Mathf.RoundToInt(ReadField("player.maxHealth", config.Player.maxHealth)));
            config.Player.baseMoveSpeed = Mathf.Max(0.01f, ReadField("player.moveSpeed", config.Player.baseMoveSpeed));
            config.Player.baseSuction = Mathf.Max(0f, ReadField("player.suction", config.Player.baseSuction));
            config.Player.baseEnergyBallDamage = Mathf.Max(0f, ReadField("player.energyDamage", config.Player.baseEnergyBallDamage));
            config.Player.levelRequirementBase = Mathf.Max(1f, ReadField("player.levelBaseMass", config.Player.levelRequirementBase));
            config.Player.levelRequirementIncrement1To5 = Mathf.Max(0f, ReadField("player.levelIncrement1To5", config.Player.levelRequirementIncrement1To5));
            config.Player.levelRequirementIncrement6To10 = Mathf.Max(0f, ReadField("player.levelIncrement6To10", config.Player.levelRequirementIncrement6To10));
            config.Player.levelRequirementIncrement11To15 = Mathf.Max(0f, ReadField("player.levelIncrement11To15", config.Player.levelRequirementIncrement11To15));
            config.Player.levelRequirementIncrement16To20 = Mathf.Max(0f, ReadField("player.levelIncrement16To20", config.Player.levelRequirementIncrement16To20));
            config.Player.levelRequirementIncrement21Plus = Mathf.Max(0f, ReadField("player.levelIncrement21Plus", config.Player.levelRequirementIncrement21Plus));
            config.Inhale.maximumSuctionForce = Mathf.Max(0f, ReadField("player.suction", config.Inhale.maximumSuctionForce));
            config.Inhale.suctionDamageMultiplier = Mathf.Max(0f, ReadField("player.suctionDamage", config.Inhale.suctionDamageMultiplier));
            config.Inhale.radius = Mathf.Max(0f, ReadField("player.inhaleRadius", config.Inhale.radius));
            config.Inhale.angle = Mathf.Clamp(ReadField("player.inhaleAngle", config.Inhale.angle), 0f, 360f);
            SaveConfigAsset(config);
            RefreshPlayerFields(true);
        }

        private void ApplyEnemyConfiguration()
        {
            if (_trackedEnemy == null || _trackedEnemy.Data == null) return;
            EnemyData data = _trackedEnemy.Data;
            if (data.behavior == null) data.behavior = new EnemyBehaviorSettings();
            ApplyEnemyRuntimeValue("enemy.maxHealth", ReadField("enemy.maxHealth", data.maxHealth));
            ApplyEnemyRuntimeValue("enemy.massValue", ReadField("enemy.massValue", data.massValue));
            ApplyEnemyRuntimeValue("enemy.moveSpeed", ReadField("enemy.moveSpeed", data.moveSpeed));
            ApplyEnemyRuntimeValue("enemy.attackDamage", ReadField("enemy.attackDamage", data.attackDamage));
            ApplyEnemyRuntimeValue("enemy.attackCooldown", ReadField("enemy.attackCooldown", data.attackCooldown));
            ApplyEnemyRuntimeValue("enemy.attackRange", ReadField("enemy.attackRange", data.attackRange));
            ApplyEnemyRuntimeValue("enemy.detectRange", ReadField("enemy.detectRange", data.detectRange));
            ApplyEnemyRuntimeValue("enemy.aimedSpeed", ReadField("enemy.aimedSpeed", data.aimedProjectileSpeed));
            ApplyEnemyRuntimeValue("enemy.radialSpeed", ReadField("enemy.radialSpeed", data.radialProjectileSpeed));
            ApplyEnemyRuntimeValue("enemy.radialCount", ReadField("enemy.radialCount", data.radialProjectileCount));
            ApplyEnemyRuntimeValue("enemy.radialAngle", ReadField("enemy.radialAngle", data.radialProjectileAngle));
            ApplyEnemyRuntimeValue("enemy.specialCooldown", ReadField("enemy.specialCooldown", data.behavior.specialAttackCooldown));
            ApplyEnemyRuntimeValue("enemy.proximityRange", ReadField("enemy.proximityRange", data.behavior.proximityRange));
            ApplyEnemyRuntimeValue("enemy.jumpSpeed", ReadField("enemy.jumpSpeed", data.behavior.jumpSpeed));
            ApplyEnemyRuntimeValue("enemy.spiderJumpInterval", ReadField("enemy.spiderJumpInterval", data.behavior.spiderJumpMinimumInterval));
            ApplyEnemyRuntimeValue("enemy.spiderPreparationMin", ReadField("enemy.spiderPreparationMin", data.behavior.spiderJumpPreparationRange.x));
            ApplyEnemyRuntimeValue("enemy.spiderPreparationMax", ReadField("enemy.spiderPreparationMax", data.behavior.spiderJumpPreparationRange.y));
            ApplyEnemyRuntimeValue("enemy.dashSpeed", ReadField("enemy.dashSpeed", data.behavior.dashSpeed));
            ApplyBehaviorFields(data.behavior);

            EnemyData template = _trackedEnemy.TemplateData;
            if (template != null && template != data)
            {
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(data), template);
                SaveConfigAsset(template);
            }
            _trackedEnemy.GetComponent<EnemyActor>()?.RefreshTestConfiguration();
            RefreshEnemyFields(true);
        }

        private void ApplyBehaviorFields(EnemyBehaviorSettings behavior)
        {
            ApplyEnemyRuntimeValue("enemy.specialWaves", ReadField("enemy.specialWaves", behavior.specialProjectileWaves));
            ApplyEnemyRuntimeValue("enemy.specialBulletCount", ReadField("enemy.specialBulletCount", behavior.specialProjectileCount));
            ApplyEnemyRuntimeValue("enemy.specialBulletInterval", ReadField("enemy.specialBulletInterval", behavior.specialProjectileInterval));
            ApplyEnemyRuntimeValue("enemy.specialBulletAngle", ReadField("enemy.specialBulletAngle", behavior.specialProjectileAngle));
            ApplyEnemyRuntimeValue("enemy.specialBulletAngleStep", ReadField("enemy.specialBulletAngleStep", behavior.specialProjectileAngleStep));
            ApplyEnemyRuntimeValue("enemy.fireballCount", ReadField("enemy.fireballCount", behavior.fireballCount));
            ApplyEnemyRuntimeValue("enemy.fireballInterval", ReadField("enemy.fireballInterval", behavior.fireballInterval));
            ApplyEnemyRuntimeValue("enemy.fireballRadialCount", ReadField("enemy.fireballRadialCount", behavior.fireballRadialBulletCount));
            ApplyEnemyRuntimeValue("enemy.specialMoveSpeed", ReadField("enemy.specialMoveSpeed", behavior.specialMoveSpeed));
            ApplyEnemyRuntimeValue("enemy.dashDuration", ReadField("enemy.dashDuration", behavior.dashDuration));
            ApplyEnemyRuntimeValue("enemy.movementCycle", ReadField("enemy.movementCycle", behavior.movementCycleDuration));
            ApplyEnemyRuntimeValue("enemy.movementActive", ReadField("enemy.movementActive", behavior.movementActiveDuration));
            ApplyEnemyRuntimeValue("enemy.movementIdle", ReadField("enemy.movementIdle", behavior.movementIdleDuration));
            ApplyEnemyRuntimeValue("enemy.actionsPerSpecial", ReadField("enemy.actionsPerSpecial", behavior.actionsPerSpecial));
            ApplyEnemyRuntimeValue("enemy.jumpHeight", ReadField("enemy.jumpHeight", behavior.jumpHeight));
            ApplyEnemyRuntimeValue("enemy.takeoffDuration", ReadField("enemy.takeoffDuration", behavior.takeoffDuration));
            ApplyEnemyRuntimeValue("enemy.airborneDuration", ReadField("enemy.airborneDuration", behavior.airborneDuration));
            ApplyEnemyRuntimeValue("enemy.landingDuration", ReadField("enemy.landingDuration", behavior.landingDuration));
            ApplyEnemyRuntimeValue("enemy.dashPreparation", ReadField("enemy.dashPreparation", behavior.dashPreparationDuration));
            ApplyEnemyRuntimeValue("enemy.dashRecovery", ReadField("enemy.dashRecovery", behavior.dashRecoveryDuration));
            ApplyEnemyRuntimeValue("enemy.fireballFallDuration", ReadField("enemy.fireballFallDuration", behavior.fireballFallDuration));
            ApplyEnemyRuntimeValue("enemy.offscreenPadding", ReadField("enemy.offscreenPadding", behavior.offscreenPadding));
            ApplyEnemyRuntimeValue("enemy.stateTransition", ReadField("enemy.stateTransition", behavior.stateTransitionDuration));
            ApplyEnemyRuntimeValue("enemy.stateHold", ReadField("enemy.stateHold", behavior.stateHoldDuration));
            ApplyEnemyRuntimeValue("enemy.orbitAngularSpeed", ReadField("enemy.orbitAngularSpeed", behavior.orbitAngularSpeed));
            ApplyEnemyRuntimeValue("enemy.orbitPursuitWeight", ReadField("enemy.orbitPursuitWeight", behavior.orbitPursuitWeight));
            ApplyEnemyRuntimeValue("enemy.orbitTangentWeight", ReadField("enemy.orbitTangentWeight", behavior.orbitTangentWeight));
            ApplyEnemyRuntimeValue("enemy.orbitSeparationWeight", ReadField("enemy.orbitSeparationWeight", behavior.orbitSeparationWeight));
            ApplyEnemyRuntimeValue("enemy.wanderMinInterval", ReadField("enemy.wanderMinInterval", behavior.wanderIntervalRange.x));
            ApplyEnemyRuntimeValue("enemy.wanderMaxInterval", ReadField("enemy.wanderMaxInterval", behavior.wanderIntervalRange.y));
            ApplyEnemyRuntimeValue("enemy.wanderTurnAngle", ReadField("enemy.wanderTurnAngle", behavior.wanderMaximumTurnAngle));
            ApplyEnemyRuntimeValue("enemy.wanderTurnSpeed", ReadField("enemy.wanderTurnSpeed", behavior.wanderTurnSpeed));
            ApplyEnemyRuntimeValue("enemy.evasiveTurnSpeed", ReadField("enemy.evasiveTurnSpeed", behavior.evasiveTurnSpeed));
            ApplyEnemyRuntimeValue("enemy.healthLossInterval", ReadField("enemy.healthLossInterval", behavior.healthLossEffectInterval));
            ApplyEnemyRuntimeValue("enemy.healthLossMaxTriggers", ReadField("enemy.healthLossMaxTriggers", behavior.healthLossEffectMaximumTriggers));
            ApplyEnemyRuntimeValue("enemy.healthLossBulletCount", ReadField("enemy.healthLossBulletCount", behavior.healthLossEffectBulletCount));
            ApplyEnemyRuntimeValue("enemy.healthLossSummonMeatball", ReadField("enemy.healthLossSummonMeatball", behavior.healthLossEffectSummonsMeatball ? 1f : 0f));
            ApplyEnemyRuntimeValue("enemy.healthLossMaxScale", ReadField("enemy.healthLossMaxScale", behavior.healthLossEffectMaximumScale));
            ApplyEnemyRuntimeValue("enemy.healthLossPulseDuration", ReadField("enemy.healthLossPulseDuration", behavior.healthLossEffectPulseDuration));
        }

        private void ApplyPlayerRuntimeValue(string key, float value)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return;
            PlayerHealth health = player.GetComponent<PlayerHealth>();
            PlayerController controller = player.GetComponent<PlayerController>();
            PlayerInhale inhale = player.GetComponent<PlayerInhale>();
            PlayerSpit spit = player.GetComponent<PlayerSpit>();
            SwallowContainer container = player.GetComponent<SwallowContainer>();
            switch (key)
            {
                case "player.maxHealth": health?.SetMaxHealthForTesting(Mathf.Max(1, Mathf.RoundToInt(value))); break;
                case "player.moveSpeed": if (controller != null) controller.MoveSpeed = Mathf.Max(0.01f, value); break;
                case "player.suction": if (inhale != null) inhale.MaxSuctionForce = Mathf.Max(0f, value); break;
                case "player.levelBaseMass":
                    if (container != null)
                    {
                        container.LevelRequirementBase = value;
                        container.RefreshLevelRequirement();
                    }
                    break;
                case "player.levelIncrement1To5":
                    if (container != null)
                    {
                        container.LevelRequirementIncrement1To5 = value;
                        container.RefreshLevelRequirement();
                    }
                    break;
                case "player.levelIncrement6To10":
                    if (container != null)
                    {
                        container.LevelRequirementIncrement6To10 = value;
                        container.RefreshLevelRequirement();
                    }
                    break;
                case "player.levelIncrement11To15":
                    if (container != null)
                    {
                        container.LevelRequirementIncrement11To15 = value;
                        container.RefreshLevelRequirement();
                    }
                    break;
                case "player.levelIncrement16To20":
                    if (container != null)
                    {
                        container.LevelRequirementIncrement16To20 = value;
                        container.RefreshLevelRequirement();
                    }
                    break;
                case "player.levelIncrement21Plus":
                    if (container != null)
                    {
                        container.LevelRequirementIncrement21Plus = value;
                        container.RefreshLevelRequirement();
                    }
                    break;
                case "player.energyDamage": if (spit != null) spit.BaseDamage = Mathf.Max(0f, value); break;
                case "player.suctionDamage": if (inhale != null) inhale.BaseDamageMultiplier = Mathf.Max(0f, value); break;
                case "player.inhaleRadius": if (inhale != null) inhale.InhaleRadius = Mathf.Max(0f, value); break;
                case "player.inhaleAngle": if (inhale != null) inhale.InhaleAngle = Mathf.Clamp(value, 0f, 360f); break;
            }
        }

        private void ApplyEnemyRuntimeValue(string key, float value)
        {
            if (_trackedEnemy == null || _trackedEnemy.Data == null) return;
            EnemyData data = _trackedEnemy.Data;
            if (data.behavior == null) data.behavior = new EnemyBehaviorSettings();
            switch (key)
            {
                case "enemy.maxHealth":
                    data.maxHealth = Mathf.Max(1f, value);
                    _trackedEnemy.ReplaceHealth(_trackedEnemy.CurrentHealth, data.maxHealth);
                    break;
                case "enemy.massValue":
                {
                    data.massValue = Mathf.Max(0f, value);
                    InhaleableItem item = _trackedEnemy.GetComponent<InhaleableItem>();
                    if (item != null) item.Mass = data.massValue;
                    break;
                }
                case "enemy.moveSpeed": data.moveSpeed = Mathf.Max(0f, value); break;
                case "enemy.attackDamage": data.attackDamage = Mathf.Max(0f, value); break;
                case "enemy.attackCooldown": data.attackCooldown = Mathf.Max(0.01f, value); break;
                case "enemy.attackRange": data.attackRange = Mathf.Max(0f, value); break;
                case "enemy.detectRange": data.detectRange = Mathf.Max(0f, value); break;
                case "enemy.aimedSpeed": data.aimedProjectileSpeed = Mathf.Max(0f, value); break;
                case "enemy.radialSpeed": data.radialProjectileSpeed = Mathf.Max(0f, value); break;
                case "enemy.radialCount": data.radialProjectileCount = Mathf.Max(0, Mathf.RoundToInt(value)); break;
                case "enemy.radialAngle": data.radialProjectileAngle = Mathf.Clamp(value, 0f, 360f); break;
                case "enemy.specialCooldown": data.behavior.specialAttackCooldown = Mathf.Max(0.01f, value); break;
                case "enemy.proximityRange":
                    data.behavior.proximityRange = data.archetype == EnemyArchetype.Spider
                        ? Mathf.Clamp(value, 0f, 12f) : Mathf.Max(0f, value);
                    break;
                case "enemy.jumpSpeed": data.behavior.jumpSpeed = Mathf.Max(0f, value); break;
                case "enemy.spiderJumpInterval": data.behavior.spiderJumpMinimumInterval = Mathf.Max(3f, value); break;
                case "enemy.spiderPreparationMin":
                    data.behavior.spiderJumpPreparationRange.x = Mathf.Clamp(value, 0.1f, 0.5f);
                    break;
                case "enemy.spiderPreparationMax":
                    data.behavior.spiderJumpPreparationRange.y = Mathf.Clamp(value, 0.1f, 0.5f);
                    break;
                case "enemy.dashSpeed": data.behavior.dashSpeed = Mathf.Max(0f, value); break;
                case "enemy.specialWaves": data.behavior.specialProjectileWaves = Mathf.Max(0, Mathf.RoundToInt(value)); break;
                case "enemy.specialBulletCount": data.behavior.specialProjectileCount = Mathf.Max(0, Mathf.RoundToInt(value)); break;
                case "enemy.specialBulletInterval": data.behavior.specialProjectileInterval = Mathf.Max(0f, value); break;
                case "enemy.specialBulletAngle": data.behavior.specialProjectileAngle = Mathf.Clamp(value, 0f, 360f); break;
                case "enemy.specialBulletAngleStep": data.behavior.specialProjectileAngleStep = Mathf.Clamp(value, -360f, 360f); break;
                case "enemy.fireballCount": data.behavior.fireballCount = Mathf.Max(0, Mathf.RoundToInt(value)); break;
                case "enemy.fireballInterval": data.behavior.fireballInterval = Mathf.Max(0f, value); break;
                case "enemy.fireballRadialCount": data.behavior.fireballRadialBulletCount = Mathf.Max(0, Mathf.RoundToInt(value)); break;
                case "enemy.specialMoveSpeed": data.behavior.specialMoveSpeed = Mathf.Max(0f, value); break;
                case "enemy.dashDuration": data.behavior.dashDuration = Mathf.Max(0f, value); break;
                case "enemy.movementCycle": data.behavior.movementCycleDuration = Mathf.Max(0.01f, value); break;
                case "enemy.movementActive": data.behavior.movementActiveDuration = Mathf.Max(0f, value); break;
                case "enemy.movementIdle": data.behavior.movementIdleDuration = Mathf.Max(0f, value); break;
                case "enemy.actionsPerSpecial": data.behavior.actionsPerSpecial = Mathf.Max(0, Mathf.RoundToInt(value)); break;
                case "enemy.jumpHeight": data.behavior.jumpHeight = Mathf.Max(0f, value); break;
                case "enemy.takeoffDuration": data.behavior.takeoffDuration = Mathf.Max(0f, value); break;
                case "enemy.airborneDuration": data.behavior.airborneDuration = Mathf.Max(0f, value); break;
                case "enemy.landingDuration": data.behavior.landingDuration = Mathf.Max(0f, value); break;
                case "enemy.dashPreparation": data.behavior.dashPreparationDuration = Mathf.Max(0f, value); break;
                case "enemy.dashRecovery": data.behavior.dashRecoveryDuration = Mathf.Max(0f, value); break;
                case "enemy.fireballFallDuration": data.behavior.fireballFallDuration = Mathf.Max(0f, value); break;
                case "enemy.offscreenPadding": data.behavior.offscreenPadding = Mathf.Max(0f, value); break;
                case "enemy.stateTransition": data.behavior.stateTransitionDuration = Mathf.Max(0f, value); break;
                case "enemy.stateHold": data.behavior.stateHoldDuration = Mathf.Max(0f, value); break;
                case "enemy.orbitAngularSpeed": data.behavior.orbitAngularSpeed = Mathf.Max(0f, value); break;
                case "enemy.orbitPursuitWeight": data.behavior.orbitPursuitWeight = Mathf.Max(0f, value); break;
                case "enemy.orbitTangentWeight": data.behavior.orbitTangentWeight = Mathf.Max(0f, value); break;
                case "enemy.orbitSeparationWeight": data.behavior.orbitSeparationWeight = Mathf.Max(0f, value); break;
                case "enemy.wanderMinInterval": data.behavior.wanderIntervalRange.x = Mathf.Max(0f, value); break;
                case "enemy.wanderMaxInterval": data.behavior.wanderIntervalRange.y = Mathf.Max(0f, value); break;
                case "enemy.wanderTurnAngle": data.behavior.wanderMaximumTurnAngle = Mathf.Clamp(value, 0f, 180f); break;
                case "enemy.wanderTurnSpeed": data.behavior.wanderTurnSpeed = Mathf.Max(0f, value); break;
                case "enemy.evasiveTurnSpeed": data.behavior.evasiveTurnSpeed = Mathf.Max(0f, value); break;
                case "enemy.healthLossInterval": data.behavior.healthLossEffectInterval = Mathf.Clamp01(value); break;
                case "enemy.healthLossMaxTriggers": data.behavior.healthLossEffectMaximumTriggers = Mathf.Max(0, Mathf.RoundToInt(value)); break;
                case "enemy.healthLossBulletCount": data.behavior.healthLossEffectBulletCount = Mathf.Max(0, Mathf.RoundToInt(value)); break;
                case "enemy.healthLossSummonMeatball": data.behavior.healthLossEffectSummonsMeatball = value >= 0.5f; break;
                case "enemy.healthLossMaxScale": data.behavior.healthLossEffectMaximumScale = Mathf.Max(1f, value); break;
                case "enemy.healthLossPulseDuration": data.behavior.healthLossEffectPulseDuration = Mathf.Max(0.01f, value); break;
            }
            _trackedEnemy.GetComponent<EnemyActor>()?.RefreshTestConfiguration();
        }

        private float ReadField(string key, float fallback)
        {
            return _fields.TryGetValue(key, out InputField field) && float.TryParse(field.text,
                System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float value)
                ? value : fallback;
        }

        private bool TryReadFloat(string key, float minimum, out float value)
        {
            value = ReadField(key, minimum);
            if (value < minimum) value = minimum;
            if (_fields.TryGetValue(key, out InputField field)) field.text = value.ToString("0.###");
            return true;
        }

        private void SetField(string key, float value, bool force)
        {
            if (!_fields.TryGetValue(key, out InputField field) || (!force && field.isFocused)) return;
            field.SetTextWithoutNotify(value.ToString("0.###"));
        }

        private bool HasFocusedField()
        {
            foreach (InputField field in _fields.Values)
                if (field != null && field.isFocused) return true;
            return false;
        }

        private static GameObject CreatePanel(string name, Transform parent, Vector2 position, Vector2 size, bool rightAligned)
        {
            GameObject panel = new(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rightAligned ? new Vector2(1f, 0.5f) : new Vector2(0f, 0.5f);
            rect.pivot = rightAligned ? new Vector2(1f, 0.5f) : new Vector2(0f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            panel.GetComponent<Image>().color = new Color(0.04f, 0.05f, 0.07f, 0.94f);
            return panel;
        }

        private static Button CreateButton(string name, Transform parent, Vector2 min, Vector2 max, string label)
        {
            GameObject owner = new(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(UIButtonAudio));
            owner.transform.SetParent(parent, false);
            SetRect(owner.GetComponent<RectTransform>(), min, max);
            owner.GetComponent<Image>().color = new Color(0.2f, 0.24f, 0.3f, 1f);
            Text text = CreateText("Label", owner.transform, 15, TextAnchor.MiddleCenter);
            SetRect(text.rectTransform, Vector2.zero, Vector2.one);
            text.text = label;
            return owner.GetComponent<Button>();
        }

        private static Toggle CreateToggle(string name, Transform parent, Vector2 min, Vector2 max, string label)
        {
            GameObject owner = new(name, typeof(RectTransform), typeof(Toggle));
            owner.transform.SetParent(parent, false);
            SetRect(owner.GetComponent<RectTransform>(), min, max);
            Toggle toggle = owner.GetComponent<Toggle>();
            GameObject background = new("Background", typeof(RectTransform), typeof(Image));
            background.transform.SetParent(owner.transform, false);
            SetRect(background.GetComponent<RectTransform>(), new Vector2(0f, 0.1f), new Vector2(0.18f, 0.9f));
            background.GetComponent<Image>().color = new Color(0.2f, 0.24f, 0.3f, 1f);
            GameObject checkmark = new("Checkmark", typeof(RectTransform), typeof(Image));
            checkmark.transform.SetParent(background.transform, false);
            SetRect(checkmark.GetComponent<RectTransform>(), new Vector2(0.2f, 0.2f), new Vector2(0.8f, 0.8f));
            checkmark.GetComponent<Image>().color = new Color(0.2f, 0.85f, 0.55f, 1f);
            toggle.graphic = checkmark.GetComponent<Image>();
            toggle.targetGraphic = background.GetComponent<Image>();
            Text text = CreateText("Label", owner.transform, 13, TextAnchor.MiddleLeft);
            SetRect(text.rectTransform, new Vector2(0.23f, 0f), Vector2.one);
            text.text = label;
            return toggle;
        }

        private static InputField CreateInputField(string name, Transform parent)
        {
            GameObject owner = new(name, typeof(RectTransform), typeof(Image), typeof(InputField));
            owner.transform.SetParent(parent, false);
            owner.GetComponent<Image>().color = new Color(0.12f, 0.14f, 0.18f, 1f);
            InputField input = owner.GetComponent<InputField>();
            Text text = CreateText("Text", owner.transform, 13, TextAnchor.MiddleRight);
            SetRect(text.rectTransform, Vector2.zero, Vector2.one);
            text.color = Color.white;
            text.supportRichText = false;
            input.textComponent = text;
            return input;
        }

        private static Text CreateText(string name, Transform parent, int size, TextAnchor alignment)
        {
            GameObject owner = new(name, typeof(RectTransform), typeof(Text));
            owner.transform.SetParent(parent, false);
            Text text = owner.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.fontStyle = FontStyle.Bold;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        private static void SetRect(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static void SaveConfigAsset(UnityEngine.Object asset)
        {
#if UNITY_EDITOR
            if (asset == null) return;
            UnityEditor.EditorUtility.SetDirty(asset);
            UnityEditor.AssetDatabase.SaveAssets();
#endif
        }
    }
}
