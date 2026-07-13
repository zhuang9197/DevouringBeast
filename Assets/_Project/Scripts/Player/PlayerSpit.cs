using System.Collections.Generic;
using UnityEngine;

namespace DevouringBeast
{
    /// <summary>
    /// PlayerSpit — 玩家吐出逻辑
    /// 将口中物品打包为能量球射出
    /// </summary>
    public class PlayerSpit : MonoBehaviour
    {
        [Header("吐出参数")]
        [SerializeField] private GameObject energyBallPrefab;
        [SerializeField] private float spitSpeed = 15f;
        [SerializeField] private float maxFlyDistance = 20f;
        [SerializeField] private float baseDamage = 25f;
        [SerializeField] private float damagePerMass = 2f;
        [SerializeField, Min(0f), Tooltip("能量球从玩家朝向前方生成的距离")]
        private float spawnForwardOffset = 0.75f;

        [Header("对象池")]
        [SerializeField, Min(0)] private int poolInitialSize = 12;
        [SerializeField, Min(1)] private int poolMaxSize = 64;

        [Header("蓄力（尖嘴技能）")]
        [SerializeField] private float maxChargeTime = 1.5f;
        [SerializeField] private float maxChargeBonus = 0.3f; // 30% 额外伤害

        [Header("事件")]
        [SerializeField] private VoidEventChannel onSpit;

        private PlayerController _playerController;
        private SwallowContainer _container;
        private RogueSkillManager _skillManager;
        private ObjectPool<EnergyBall> _energyBallPool;
        private Transform _poolRoot;

        private float _chargeTimer;
        private bool _isCharging;

        public float SpitSpeed
        {
            get => spitSpeed;
            set => spitSpeed = value;
        }
        public float BaseDamage
        {
            get => baseDamage;
            set => baseDamage = value;
        }

        private void Awake()
        {
            _playerController = GetComponent<PlayerController>();
            _container = GetComponent<SwallowContainer>();
            _skillManager = GetComponent<RogueSkillManager>();
        }

        /// <summary>
        /// 吐出能量球
        /// </summary>
        public void Spit()
        {
            if (!_container.HasItems) return;
            if (!EnsurePool()) return;

            var items = _container.ClearItems();

            // 计算伤害：基础伤害 + 质量加成
            float totalMass = 0f;
            foreach (var item in items) totalMass += item.Mass;

            float damage = baseDamage + totalMass * damagePerMass;

            // 蓄力加成（尖嘴技能）
            float chargeBonus = GetChargeBonus();
            damage *= (1f + chargeBonus);

            // 技能加成：多嘴 -> 多颗能量球
            int ballCount = GetBallCount();
            float perBallDamage = damage / ballCount;

            // 每次吐出都重新复制当前技能与最终弹体属性。
            EnergyBallShotSnapshot snapshot = _skillManager != null
                ? _skillManager.CreateEnergyBallSnapshot(perBallDamage, spitSpeed, maxFlyDistance)
                : new EnergyBallShotSnapshot(perBallDamage, spitSpeed, maxFlyDistance, null);

            for (int i = 0; i < ballCount; i++)
            {
                SpawnEnergyBall(snapshot, i, ballCount);
            }

            onSpit?.RaiseEvent();
        }

        private float GetChargeBonus()
        {
            if (!_isCharging) return 0f;
            float progress = Mathf.Clamp01(_chargeTimer / maxChargeTime);
            return progress * maxChargeBonus;
        }

        private int GetBallCount()
        {
            // 多嘴技能每级增加一颗，限制数量避免弹幕失控。
            int multiMouthLevel = _skillManager != null
                ? _skillManager.GetOwnedSkillLevel("多嘴")
                : 0;
            return Mathf.Clamp(1 + multiMouthLevel, 1, 6);
        }

        private void SpawnEnergyBall(EnergyBallShotSnapshot snapshot, int index, int total)
        {
            Vector2 dir = _playerController.FacingDirection;

            // 多颗球时略微偏移方向
            if (total > 1)
            {
                float spread = 10f; // 分散角度
                float offset = (index - (total - 1) * 0.5f) * spread;
                dir = Quaternion.Euler(0, 0, offset) * dir;
            }

            Vector3 spawnPosition = transform.position + (Vector3)(dir * spawnForwardOffset);
            EnergyBall ball = _energyBallPool.Get(spawnPosition, Quaternion.identity);
            ball.Initialize(
                dir,
                snapshot,
                transform,
                ReleaseEnergyBall,
                SpawnSplitEnergyBall);
        }

        private void SpawnSplitEnergyBall(
            Vector3 position,
            Vector2 direction,
            EnergyBallShotSnapshot snapshot,
            int generation)
        {
            if (!EnsurePool()) return;

            EnergyBall ball = _energyBallPool.Get(position, Quaternion.identity);
            ball.Initialize(
                direction,
                snapshot,
                transform,
                ReleaseEnergyBall,
                SpawnSplitEnergyBall,
                generation);
        }

        private void ReleaseEnergyBall(EnergyBall ball)
        {
            if (_energyBallPool != null)
                _energyBallPool.Release(ball);
        }

        private bool EnsurePool()
        {
            if (_energyBallPool != null)
                return true;

            if (energyBallPrefab == null)
            {
                Debug.LogError("[PlayerSpit] 未指定 EnergyBall 预制体。", this);
                return false;
            }

            EnergyBall prefabComponent = energyBallPrefab.GetComponent<EnergyBall>();
            if (prefabComponent == null)
            {
                Debug.LogError("[PlayerSpit] EnergyBall 预制体缺少 EnergyBall 组件。", this);
                return false;
            }

            GameObject root = new GameObject("EnergyBallPool");
            _poolRoot = root.transform;
            _energyBallPool = new ObjectPool<EnergyBall>(
                prefabComponent,
                poolInitialSize,
                Mathf.Max(poolInitialSize, poolMaxSize),
                _poolRoot);
            return true;
        }

        private void OnDestroy()
        {
            if (_energyBallPool != null)
                _energyBallPool.Clear();

            if (_poolRoot != null)
                Destroy(_poolRoot.gameObject);
        }

        /// <summary>
        /// 开始蓄力（尖嘴技能）
        /// </summary>
        public void StartCharge()
        {
            _isCharging = true;
            _chargeTimer = 0f;
        }

        /// <summary>
        /// 停止蓄力
        /// </summary>
        public void StopCharge()
        {
            _isCharging = false;
        }

        private void Update()
        {
            if (_isCharging)
            {
                _chargeTimer += Time.deltaTime;
            }
        }
    }
}
