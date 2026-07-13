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
        [SerializeField] protected float corpseDuration = 10f;
        [SerializeField] protected float corpseFlickerStart = 3f;
        [SerializeField] protected float flickerInterval = 0.15f;

        protected InhaleableItem _item;
        protected bool _isDead;
        protected float _currentHealth;
        protected float _maxHealth;

        public bool IsDead => _isDead;
        public EnemyData Data => data;
        public float HealthPercent => _maxHealth > 0 ? _currentHealth / _maxHealth : 0f;
        public float CurrentHealth => _currentHealth;
        public float MaxHealth => _maxHealth;

        public event Action<EnemyBase> OnDeath;

        protected virtual void Awake()
        {
            _item = GetComponent<InhaleableItem>();
            if (spriteRenderer == null)
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (animator == null)
                animator = GetComponentInChildren<Animator>();
        }

        /// <summary>
        /// 用 EnemyData 初始化敌人属性（由 Spawner 调用）
        /// </summary>
        public virtual void Initialize(EnemyData enemyData)
        {
            data = enemyData;
            _maxHealth = data.maxHealth;
            _currentHealth = _maxHealth;

            // 配置 InhaleableItem
            if (_item != null)
            {
                _item.Tag = data.tag;
                _item.Mass = data.killMass;
                _item.AliveInhaleThreshold = data.aliveInhaleThreshold;
                _item.DeadInhaleThreshold = data.deadInhaleThreshold;
                _item.IsAlive = true;
            }

            // 配置 AnimatorController
            if (animator != null && data.animatorController != null)
            {
                animator.runtimeAnimatorController = data.animatorController;
            }

            EnemyHealthBar.EnsureFor(this);
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

        protected virtual void Die()
        {
            _isDead = true;

            // 更新 InhaleableItem：死后质量和阈值
            if (_item != null)
            {
                _item.IsAlive = false;
                _item.Mass = data != null ? data.deadMass : 5f;
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

            // 启动尸体消失协程
            StartCoroutine(CorpseDecayRoutine());
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
            Destroy(gameObject);
        }
    }
}
