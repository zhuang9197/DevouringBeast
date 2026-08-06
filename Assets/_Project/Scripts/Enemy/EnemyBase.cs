using System;
using System.Collections;
using UnityEngine;

namespace DevouringBeast
{
    /// <summary>
    /// EnemyBase — 敌人基类
    /// 处理血量、双值吸入阈值、死亡行为（旋转倒地+闪烁消失）
    /// </summary>
    [RequireComponent(typeof(InhaleableItem))]
    public class EnemyBase : MonoBehaviour
    {
        [Header("数据")]
        [SerializeField] private EnemyData data;

        [Header("渲染")]
        [SerializeField] protected SpriteRenderer spriteRenderer;
        [SerializeField] protected Animator animator;

        [Header("死亡行为")]
        [SerializeField] protected float corpseDuration = 20f;
        [SerializeField] protected float corpseFlickerStart = 3f;
        [SerializeField] protected float flickerInterval = 0.15f;
        protected InhaleableItem _item;
        protected bool _isDead;
        protected float _currentHealth;
        protected float _maxHealth;
        private Quaternion _initialSpriteRotation;
        private EnemyData _runtimeData;

        public bool IsDead => _isDead;
        public EnemyData Data => data;
        public float HealthPercent => _maxHealth > 0 ? _currentHealth / _maxHealth : 0f;
        public float CurrentHealth => _currentHealth;
        public float MaxHealth => _maxHealth;
        public bool IsVisible => spriteRenderer != null && spriteRenderer.isVisible;
        public float MassValue => data != null ? Mathf.Max(0f, data.massValue) : 5f;

        public event Action<EnemyBase> OnDeath;
        public static event Action<EnemyBase> OnAnyEnemyDeath;

        protected virtual void Awake()
        {
            _item = GetComponent<InhaleableItem>();
            if (spriteRenderer == null)
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (animator == null)
                animator = GetComponentInChildren<Animator>();
            if (spriteRenderer != null) _initialSpriteRotation = spriteRenderer.transform.localRotation;
        }

        public EnemyData GetOrCreateRuntimeData()
        {
            if (_runtimeData == null)
            {
                _runtimeData = ScriptableObject.CreateInstance<EnemyData>();
                _runtimeData.hideFlags = HideFlags.DontSave;
            }
            return _runtimeData;
        }

        /// <summary>
        /// 用 EnemyData 初始化敌人属性（由 Spawner 调用）
        /// </summary>
        public virtual void Initialize(EnemyData enemyData)
        {
            StopAllCoroutines();
            data = enemyData;
            _isDead = false;
            _maxHealth = data.maxHealth;
            _currentHealth = _maxHealth;

            // 配置 InhaleableItem
            if (_item != null)
            {
                _item.Mass = MassValue;
                _item.AliveInhaleThreshold = data.aliveInhaleThreshold;
                _item.DeadInhaleThreshold = data.deadInhaleThreshold;
                _item.IsAlive = true;
                _item.ResetForReuse();
            }

            // 配置 AnimatorController
            if (animator != null && data.animatorController != null)
            {
                animator.runtimeAnimatorController = data.animatorController;
                animator.enabled = true;
                animator.Rebind();
                animator.Update(0f);
            }

            if (spriteRenderer != null)
            {
                spriteRenderer.enabled = true;
                spriteRenderer.transform.localRotation = _initialSpriteRotation;
            }
            EnemyAI ai = GetComponent<EnemyAI>();
            if (ai != null) ai.ResetForReuse();

            EnemyHealthBar.EnsureFor(this).ResetForReuse();
            EnemyStatusEffects.EnsureFor(this).ResetForReuse();
        }

        protected virtual void Start()
        {
            // 如果没有通过 Initialize 初始化，用默认 data
            if (data != null && _maxHealth == 0)
            {
                Initialize(data);
            }
            else if (_maxHealth == 0)
            {
                _maxHealth = 100f;
                _currentHealth = _maxHealth;
            }

            EnemyHealthBar.EnsureFor(this);
            EnemyStatusEffects.EnsureFor(this);
        }

        public virtual void TakeDamage(float damage)
        {
            if (_isDead) return;

            _currentHealth -= damage;
            if (_currentHealth <= 0f)
            {
                _currentHealth = 0f;
                Die();
            }
        }

        /// <summary>
        /// 治疗
        /// </summary>
        public virtual void Heal(float amount)
        {
            if (_isDead) return;
            _currentHealth = Mathf.Min(_maxHealth, _currentHealth + amount);
        }

        public void EmpowerForCrisis(WaveConfig config, int floor)
        {
            if (_isDead || data == null || config == null) return;
            data.attackDamage = Mathf.Max(data.attackDamage + 1f, config.GetAttackDamage(floor));
            data.moveSpeed *= Mathf.Max(1f, config.normalSpeedScale);
            data.attackRange *= Mathf.Max(1f, config.survivorAttackRangeScale);
            data.detectRange *= Mathf.Max(1f, config.survivorDetectRangeScale);
            data.attackCooldown = Mathf.Max(0.2f,
                data.attackCooldown / Mathf.Max(1f, config.survivorAttackSpeedScale));
            float resistance = Mathf.Max(1f, config.survivorInhaleResistanceScale);
            data.aliveInhaleThreshold *= resistance;
            data.deadInhaleThreshold *= resistance;
            if (_item != null)
            {
                _item.AliveInhaleThreshold = data.aliveInhaleThreshold;
                _item.DeadInhaleThreshold = data.deadInhaleThreshold;
                _item.Mass = MassValue;
            }
        }

        protected virtual void Die()
        {
            
            bool bossRoom = WaveManager.Instance != null && WaveManager.Instance.CurrentRoomKind == RoomKind.Boss;
            AudioManager.Instance.PlaySfx(bossRoom ? AudioCue.BossDie : AudioCue.EnemyDie);

_isDead = true;

            // 更新 InhaleableItem：死后质量和阈值
            if (_item != null)
            {
                _item.IsAlive = false;
                _item.Mass = MassValue;
            }

            // 停止 AI 和动画
            var ai = GetComponent<EnemyAI>();
            if (ai != null) ai.enabled = false;

            if (animator != null)
            {
                animator.enabled = false;
            }

            // 旋转 180° 倒地
            if (spriteRenderer != null)
            {
                spriteRenderer.transform.localRotation = Quaternion.Euler(0, 0, 180);
            }

            OnDeath?.Invoke(this);
            OnAnyEnemyDeath?.Invoke(this);

            int wave = WaveManager.Instance != null ? WaveManager.Instance.CurrentWave : 1;
            float dropChance = Mathf.Max(0.01f, 0.2f - Mathf.Floor(wave / 10f) * 0.05f);
            bool directHeal = IsFaithNoInhaleActive();
            if (directHeal)
            {
                PlayerHealth playerHealth = FindPlayerHealth();
                if (playerHealth != null && UnityEngine.Random.value < dropChance)
                    playerHealth.Heal(UnityEngine.Random.value < 0.2f ? 2 : 1);
            }
            else if (UnityEngine.Random.value < dropChance)
                BloodDrop.Spawn(transform.position, UnityEngine.Random.value < 0.2f);

            // 启动尸体消失协程
            if (directHeal)
            {
                EnemyPoolMember immediatePool = GetComponent<EnemyPoolMember>();
                if (immediatePool != null) immediatePool.Release();
                else Destroy(gameObject);
                return;
            }
            StartCoroutine(CorpseDecayRoutine());
        }

        private static bool IsFaithNoInhaleActive()
        {
            RogueSkillManager manager = RogueSkillManager.Active;
            return manager != null && (manager.Has(RogueSkillId.FaithAngel) || manager.Has(RogueSkillId.FaithDemon));
        }

        private static PlayerHealth FindPlayerHealth()
        {
            return RogueSkillManager.Active != null
                ? RogueSkillManager.Active.GetComponent<PlayerHealth>()
                : null;
        }

        private IEnumerator CorpseDecayRoutine()
        {
            // 停留（减去闪烁时间）
            yield return new WaitForSeconds(corpseDuration - corpseFlickerStart);

            // 闪烁
            float flickerEnd = Time.time + corpseFlickerStart;
            bool visible = true;
            while (Time.time < flickerEnd)
            {
                visible = !visible;
                if (spriteRenderer != null)
                    spriteRenderer.enabled = visible;
                yield return new WaitForSeconds(flickerInterval);
            }

            // 消失
            EnemyPoolMember poolMember = GetComponent<EnemyPoolMember>();
            if (poolMember != null) poolMember.Release();
            else Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (_runtimeData != null) Destroy(_runtimeData);
        }
    }
}
