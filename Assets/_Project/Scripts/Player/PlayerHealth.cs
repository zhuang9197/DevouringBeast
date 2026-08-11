using UnityEngine;
using UnityEngine.Events;

namespace DevouringBeast
{
    /// <summary>
    /// PlayerHealth — 玩家血量组件
    /// </summary>
    public class PlayerHealth : MonoBehaviour
    {
        private int maxHealth;
        private int currentHealth;
        private float invincibleDuration;

        [Header("事件")]
        public UnityEvent<int, int> OnHealthChanged; // (current, max)
        public UnityEvent OnPlayerDeath;

        private float _invincibleTimer;
        private bool _isInvincible;
        private SpriteRenderer _spriteRenderer;
        private PlayerController _controller;
        private Quaternion _initialSpriteRotation;

        public int CurrentHealth => currentHealth;
        public int MaxHealth => maxHealth;
        public bool IsDead => currentHealth <= 0;
        public bool IsInvincible => _isInvincible;
        private bool IsTestMode => GameManager.Existing != null && GameManager.Existing.IsTestMode;

        private void Awake()
        {
            PlayerBalanceSettings config = GameBalance.Current?.Player;
            if (config != null)
            {
                maxHealth = config.maxHealth;
                invincibleDuration = config.invincibleDuration;
            }
            currentHealth = maxHealth;
            _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            _controller = GetComponent<PlayerController>();
            if (_spriteRenderer != null) _initialSpriteRotation = _spriteRenderer.transform.localRotation;
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
            _controller?.NotifyPlayerActivity();
            if (IsTestMode || _isInvincible || IsDead) return;

            if (_controller != null && _controller.IsBeastForm)
                damage = Mathf.Max(1, Mathf.CeilToInt(damage * (1f - _controller.BeastDamageReduction)));

            int healthBeforeDamage = currentHealth;
            currentHealth = Mathf.Max(0, currentHealth - damage);
            SaveGameService.RecordHealthSpent(healthBeforeDamage - currentHealth);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);

            if (currentHealth <= 0)
                Die();
            else
            {
                AudioManager.Instance.PlaySfx(AudioCue.Hurt);
                StartInvincibility();
            }
        }

        public bool TrySpendHealth(int amount)
        {
            if (amount <= 0) return true;
            if (IsTestMode) return !IsDead;
            if (IsDead || _isInvincible || currentHealth <= amount) return false;
            _controller?.NotifyPlayerActivity();
            currentHealth -= amount;
            SaveGameService.RecordHealthSpent(amount);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
            AudioManager.Instance.PlaySfx(AudioCue.Hurt);
            StartInvincibility();
            return true;
        }

        /// <summary>
        /// 治疗
        /// </summary>
        public void Heal(int amount)
        {
            if (amount <= 0) return;
            currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }

        public void IncreaseMaxHealth(int amount, bool alsoHeal = false)
        {
            if (amount <= 0) return;
            maxHealth += amount;
            if (alsoHeal) currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }

        private void StartInvincibility()
        {
            _isInvincible = true;
            _invincibleTimer = invincibleDuration;
        }

        private void Die()
        {
            if (_spriteRenderer != null)
                _spriteRenderer.transform.localRotation = _initialSpriteRotation * Quaternion.Euler(0f, 0f, 90f);
            OnPlayerDeath?.Invoke();
            GameManager.Instance.HandlePlayerDeath();
        }
    }
}
