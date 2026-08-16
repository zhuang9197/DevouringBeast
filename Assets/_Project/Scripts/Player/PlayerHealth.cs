using UnityEngine;
using UnityEngine.Events;

namespace DevouringBeast
{
    /// <summary>
    /// PlayerHealth — 玩家血量组件
    /// </summary>
    public class PlayerHealth : MonoBehaviour
    {
        [Header("阵亡精灵")]
        [SerializeField] private Sprite normalDeathSprite;
        [SerializeField] private Sprite beastDeathSprite;

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
        public string LastDamageSource { get; private set; } = string.Empty;
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
            TakeDamageFrom(damage, null);
        }

        public void TakeDamageFrom(int damage, string source)
        {
            _controller?.NotifyPlayerActivity();
            if ((IsTestMode && !GameManager.Existing.TestDamageEnabled) ||
                _isInvincible || IsDead || (_controller != null && _controller.IsBeastRolling)) return;

            if (_controller != null && _controller.IsBeastForm)
                damage = Mathf.Max(1, Mathf.CeilToInt(damage * (1f - _controller.BeastDamageReduction)));

            int healthBeforeDamage = currentHealth;
            LastDamageSource = string.IsNullOrWhiteSpace(source) ? "环境或投射物" : source;
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

        public void RestoreFullHealth()
        {
            if (maxHealth <= 0) return;
            currentHealth = maxHealth;
            _isInvincible = false;
            _invincibleTimer = 0f;
            if (_spriteRenderer != null) _spriteRenderer.enabled = true;
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }

        public void SetMaxHealthForTesting(int maximum)
        {
            maxHealth = Mathf.Max(1, maximum);
            currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }

        public void IncreaseMaxHealth(int amount, bool alsoHeal = false)
        {
            if (amount <= 0) return;
            maxHealth += amount;
            if (alsoHeal) currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }

        public void RestoreHealth(int current, int maximum)
        {
            maxHealth = Mathf.Max(1, maximum);
            currentHealth = Mathf.Clamp(current, 1, maxHealth);
            _isInvincible = false;
            _invincibleTimer = 0f;
            if (_spriteRenderer != null) _spriteRenderer.enabled = true;
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
            {
                FrameAnimator frameAnimator = _spriteRenderer.GetComponent<FrameAnimator>();
                if (frameAnimator == null) frameAnimator = _spriteRenderer.GetComponentInParent<FrameAnimator>();
                if (frameAnimator != null) frameAnimator.enabled = false;
                Sprite deathSprite = _controller != null && _controller.IsBeastForm
                    ? beastDeathSprite : normalDeathSprite;
                if (deathSprite != null)
                {
                    _spriteRenderer.sprite = deathSprite;
                    _spriteRenderer.transform.localRotation = _initialSpriteRotation;
                }
                else
                    _spriteRenderer.transform.localRotation = _initialSpriteRotation * Quaternion.Euler(0f, 0f, 90f);
            }
            OnPlayerDeath?.Invoke();
            GameManager.Instance.HandlePlayerDeath();
        }
    }
}
