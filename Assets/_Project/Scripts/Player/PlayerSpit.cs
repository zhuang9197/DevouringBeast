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
        [SerializeField, Min(0f), Tooltip("能量球从玩家朝向前方生成的距离")]
        private float spawnForwardOffset = 0.75f;

        [Header("对象池")]
        [SerializeField, Min(0)] private int poolInitialSize = 12;
        [SerializeField, Min(1)] private int poolMaxSize = 64;

        [Header("蓄力（尖嘴技能）")]
        [SerializeField] private float maxChargeTime = 1.5f;
                [SerializeField, Min(0f)] private float bigMassThreshold = 30f;
[SerializeField] private float maxChargeBonus = 0.3f; // 30% 额外伤害

        [Header("事件")]
        [SerializeField] private VoidEventChannel onSpit;

        private PlayerController _playerController;
        private SwallowContainer _container;
        private RogueSkillManager _skillManager;
        private PlayerBaseAttributes _baseAttributes;
        private ObjectPool<EnergyBall> _energyBallPool;
        private Transform _poolRoot;

        private float _chargeTimer;
        private bool _isCharging;
        private float _nextAngelShotTime;
        private float _extraDamageMultiplier;
        private bool _angelFireHeld;

        public float SpitSpeed
        {
            get => spitSpeed;
            set => spitSpeed = value;
        }
        
        public bool IsCharging => _isCharging;
        public float ChargeProgress => maxChargeTime > 0f ? Mathf.Clamp01(_chargeTimer / maxChargeTime) : 1f;
        public bool IsChargeMaxed => _isCharging && ChargeProgress >= 1f;
        public bool CanCharge => _skillManager != null && _skillManager.Has(RogueSkillId.EvolutionCharged);
        public bool CanSpitWithoutItems => _skillManager != null &&
            _skillManager.Has(RogueSkillId.FaithAngel);
public float BaseDamage
        {
            get => _baseAttributes != null ? _baseAttributes.InitialEnergyBallDamage : baseDamage;
            set
            {
                baseDamage = value;
                if (_baseAttributes != null) _baseAttributes.InitialEnergyBallDamage = value;
            }
        }

        private void Awake()
        {
            _playerController = GetComponent<PlayerController>();
            _container = GetComponent<SwallowContainer>();
            _skillManager = GetComponent<RogueSkillManager>();
            _baseAttributes = GetComponent<PlayerBaseAttributes>();
            if (_baseAttributes == null) _baseAttributes = gameObject.AddComponent<PlayerBaseAttributes>();
            _baseAttributes.InitialEnergyBallDamage = baseDamage;
            EnergyBallHitVfxService.WarmUp();
        }

        /// <summary>
        /// 吐出能量球
        /// </summary>
public void Spit()
        {
            if (_playerController != null && _playerController.IsInhaling) return;
            if (!_container.HasItems && !CanSpitWithoutItems) return;
            if (CanSpitWithoutItems && Time.time < _nextAngelShotTime) return;

            bool chargedShot = _isCharging && CanCharge;
            var items = _container.HasItems ? _container.ClearItems() : new List<InhaleableItem>();

            float totalMass = 0f;
            foreach (var item in items)
            {
                if (item != null)
                {
                    totalMass += item.Mass;
                    item.ReleaseFromMouth();
                }
            }
            FireEnergyBalls(totalMass, GetBallCount(), chargedShot);
            if (CanSpitWithoutItems) _nextAngelShotTime = Time.time + 0.5f;
        }

        /// <summary>教皇在吞噬后额外发射的一颗教化能量球，不改变正常吸入/吐出。</summary>
        public void SpitTeachingBall(float consumedMass)
        {
            if (_playerController != null && _playerController.IsInhaling) return;
            bool chargedShot = _isCharging && CanCharge;
            FireEnergyBalls(Mathf.Max(0f, consumedMass), 1, chargedShot);
        }

        private void FireEnergyBalls(float totalMass, int ballCount, bool chargedShot)
        {
            if (!EnsurePool()) return;

            float currentBaseDamage = _baseAttributes != null
                ? _baseAttributes.EnergyBallBaseDamage : baseDamage;
            float extraDamageMultiplier = _extraDamageMultiplier + GetChargeBonus();

            float fullDamageMultiplier = GetPerBallDamageMultiplier(ballCount);
            EnergyBallShotSnapshot snapshot = _skillManager != null
                ? _skillManager.CreateEnergyBallSnapshot(currentBaseDamage, totalMass,
                    extraDamageMultiplier, fullDamageMultiplier, spitSpeed, maxFlyDistance)
                : new EnergyBallShotSnapshot(currentBaseDamage, totalMass,
                    extraDamageMultiplier, fullDamageMultiplier, spitSpeed, maxFlyDistance, null);

            AudioManager.Instance.PlaySfx(
                chargedShot || totalMass >= bigMassThreshold ? AudioCue.BigSplit : AudioCue.Split);

            for (int i = 0; i < ballCount; i++)
                SpawnEnergyBall(snapshot, i, ballCount);

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
            if (_skillManager == null || !_skillManager.Has(RogueSkillId.EvolutionMoreMouth)) return 1;
            return Mathf.Clamp(2 + _skillManager.GetLevel(RogueSkillId.EvolutionMoreMouthMore), 2, 4);
        }

        private float GetPerBallDamageMultiplier(int ballCount)
        {
            if (ballCount <= 1) return 1f;
            return 0.6f * (1f + (_skillManager != null ? _skillManager.GetLevel(RogueSkillId.EvolutionMoreMouthPower) * 0.1f : 0f));
        }

        public void RefreshSkillModifiers()
        {
            if (_skillManager == null) return;
            _extraDamageMultiplier = _skillManager.Has(RogueSkillId.FaithPope) ? 0.5f : 0f;
            int chargedLevel = _skillManager.GetLevel(RogueSkillId.EvolutionCharged);
            maxChargeBonus = chargedLevel * 0.1f;
        }

        public void StartAngelFire()
        {
            if (!CanSpitWithoutItems || _angelFireHeld) return;
            _angelFireHeld = true;
            Spit();
        }

        public void StopAngelFire() => _angelFireHeld = false;

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
            int generation,
            int ignoredEnemyId)
        {
            if (!EnsurePool()) return;

            EnergyBall ball = _energyBallPool.Get(position, Quaternion.identity);
            ball.Initialize(
                direction,
                snapshot,
                transform,
                ReleaseEnergyBall,
                SpawnSplitEnergyBall,
                generation,
                ignoredEnemyId);
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
            AudioManager.Existing?.StopLoop(AudioCue.Charged);
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
            if (_isCharging || !CanCharge || _container == null || (!_container.HasItems && !CanSpitWithoutItems)) return;
            _isCharging = true;
            _chargeTimer = 0f;
            AudioManager.Instance.PlayOnceUntilStopped(AudioCue.Charged);
        }

        /// <summary>
        /// 停止蓄力
        /// </summary>
public void StopCharge()
        {
            if (!_isCharging) return;
            _isCharging = false;
            AudioManager.Instance.StopLoop(AudioCue.Charged);
        }

        private void Update()
        {
            if (_isCharging)
            {
                _chargeTimer += Time.deltaTime;
            }
            if (_angelFireHeld && Time.time >= _nextAngelShotTime) Spit();
        }
    }
}
