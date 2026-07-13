using UnityEngine;
using UnityEngine.Events;

namespace DevouringBeast
{
    /// <summary>
    /// PlayerHealth — 玩家血量组件
    /// </summary>
    public class PlayerHealth : MonoBehaviour
    {
        [Header("属性")]
        [SerializeField] private int maxHealth = 100;
        [SerializeField] private int currentHealth;

        [Header("无敌帧")]
        [SerializeField] private float invincibleDuration = 1f;

        [Header("事件")]
        public UnityEvent<int, int> OnHealthChanged; // (current, max)
        public UnityEvent OnPlayerDeath;

        private float _invincibleTimer;
        private bool _isInvincible;
        private SpriteRenderer _spriteRenderer;

        public int CurrentHealth => currentHealth;
        public int MaxHealth => maxHealth;
        public bool IsDead => currentHealth <= 0;

        private void Awake()
        {
            currentHealth = maxHealth;
            _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        private void Update()
        {
            if (_isInvincible)
            {
                _invincibleTimer -= Time.deltaTime;
                // 闪烁效果
                if (_spriteRenderer != null)
                    _spriteRenderer.enabled = Mathf.PingPong(Time.time * 10f, 1f) > 0.5f;

                if (_invincibleTimer <= 0f)
                {
                    _isInvincible = false;
                    if (_spriteRenderer != null)
                        _spriteRenderer.enabled = true;
                }
            }
        }

        /// <summary>
        /// 受到伤害
        /// </summary>
        public void TakeDamage(int damage)
        {
            if (_isInvincible || IsDead) return;

            currentHealth = Mathf.Max(0, currentHealth - damage);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);

            if (currentHealth <= 0)
            {
                Die();
            }
            else
            {
                StartInvincibility();
            }
        }

        /// <summary>
        /// 治疗
        /// </summary>
        public void Heal(int amount)
        {
            currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }

        private void StartInvincibility()
        {
            _isInvincible = true;
            _invincibleTimer = invincibleDuration;
        }

        private void Die()
        {
            OnPlayerDeath?.Invoke();
            GameManager.Instance.GameOver();
        }
    }
}
