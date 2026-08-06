using UnityEngine;

namespace DevouringBeast
{
    /// <summary>
    /// EnemyAI — 敌人 AI
    /// 自动寻敌、追击、攻击，左右翻转精灵朝向玩家
    /// 通过 Animator 参数控制 idle/move/attack 动画
    /// </summary>
    [RequireComponent(typeof(EnemyBase))]
    public class EnemyAI : MonoBehaviour
    {
        private enum AIState { Chase, Attack }

        [Header("引用")]
        [SerializeField] private Animator animator;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [Header("攻击接触窗口")]
        [SerializeField, Range(0f, 1f)] private float attackWindowStart = 0.3f;
        [SerializeField, Range(0f, 1f)] private float attackWindowEnd = 0.65f;
        [SerializeField, Min(0.1f)] private float attackContactRadius = 1.15f;
        [SerializeField, Min(0f)] private float attackContactTolerance = 0.08f;
        [SerializeField, Min(0.02f)] private float aiTickInterval = 0.05f;

        private EnemyBase _enemy;
        private Rigidbody2D _rb;
        private Collider2D _collider;
        private Transform _player;
        private Collider2D _playerCollider;
        private PlayerHealth _playerHealth;

        private AIState _currentState = AIState.Chase;
        private float _attackTimer;
        private Vector2 _moveDirection;
        private bool _wasMoving;
        private bool _wasAttacking;
        private bool _hasDealtDamage; // 当前攻击周期是否已造成伤害
        private float _attackAnimationDuration = 0.5f;
        private float _statusSpeedMultiplier = 1f;
        private bool _statusStunned;
        private float _suctionSpeedMultiplier = 1f;
        private float _suctionBoostUntil;
        private float _nextAiTickTime;
        private float _lastAiTickTime;

        // Animator 参数 ID
        private static readonly int ParamIsMoving = Animator.StringToHash("IsMoving");
        private static readonly int ParamIsAttacking = Animator.StringToHash("IsAttacking");
        private static readonly int AttackState = Animator.StringToHash("Base Layer.attack");

        private float MoveSpeed => (_enemy.Data != null ? _enemy.Data.moveSpeed : 3f) * _statusSpeedMultiplier *
            (Time.time <= _suctionBoostUntil ? _suctionSpeedMultiplier : 1f);
        public bool IsStatusStunned => _statusStunned;

        public void SetStatusModifiers(float speedMultiplier, bool stunned)
        {
            _statusSpeedMultiplier = Mathf.Clamp(speedMultiplier, 0.05f, 1f);
            _statusStunned = stunned;
            if (stunned) _moveDirection = Vector2.zero;
        }

        public void ApplySuctionChaseBoost(float multiplier)
        {
            _suctionSpeedMultiplier = Mathf.Clamp(multiplier, 1f, 1.35f);
            _suctionBoostUntil = Time.time + 0.08f;
        }

        public void ResetForReuse()
        {
            enabled = true;
            _currentState = AIState.Chase;
            _attackTimer = 0f;
            _moveDirection = Vector2.zero;
            _wasMoving = false;
            _wasAttacking = false;
            _hasDealtDamage = false;
            _statusSpeedMultiplier = 1f;
            _statusStunned = false;
            _suctionSpeedMultiplier = 1f;
            _suctionBoostUntil = 0f;
            _lastAiTickTime = Time.time;
            _nextAiTickTime = Time.time + Random.Range(0f, aiTickInterval);
            if (_rb != null) _rb.velocity = Vector2.zero;
            if (animator != null)
            {
                animator.SetBool(ParamIsMoving, false);
                animator.SetBool(ParamIsAttacking, false);
            }
        }
        private float DetectRange => _enemy.Data != null ? _enemy.Data.detectRange : 8f;
        private float AttackRange => _enemy.Data != null ? _enemy.Data.attackRange : 1.5f;
        private float AttackCooldown => _enemy.Data != null ? _enemy.Data.attackCooldown : 0.9f;
        private float AttackDamage => _enemy.Data != null ? _enemy.Data.attackDamage : 10f;

        private void Awake()
        {
            _enemy = GetComponent<EnemyBase>();
            _rb = GetComponent<Rigidbody2D>();
            _collider = GetComponent<Collider2D>();
            if (animator == null)
                animator = GetComponentInChildren<Animator>();
            if (animator != null)
                animator.cullingMode = AnimatorCullingMode.CullCompletely;
            if (spriteRenderer == null)
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            CacheAttackAnimationDuration();
        }

        private void Start()
        {
            _player = GameObject.FindGameObjectWithTag("Player")?.transform;
            if (_player != null)
            {
                _playerCollider = _player.GetComponentInChildren<Collider2D>();
                _playerHealth = _player.GetComponent<PlayerHealth>();
            }
        }

        private void Update()
        {
            if (!GameManager.Instance.IsPlaying) { _moveDirection = Vector2.zero; return; }
            if (_enemy.IsDead || _player == null) return;
            float now = Time.time;
            if (now < _nextAiTickTime) return;
            float tickDelta = Mathf.Min(0.2f, Mathf.Max(0f, now - _lastAiTickTime));
            _lastAiTickTime = now;
            _nextAiTickTime = now + aiTickInterval;
            if (_statusStunned)
            {
                _moveDirection = Vector2.zero;
                UpdateAnimation();
                return;
            }

            bool canContactPlayer = IsPlayerInAttackContact();

            // 只有真正贴近玩家才停下攻击，避免卡在中心距离满足、Collider 却未接触的位置发呆。
            if (canContactPlayer)
            {
                if (_currentState != AIState.Attack)
                    TransitionTo(AIState.Attack);
            }
            else
            {
                if (_currentState != AIState.Chase)
                    TransitionTo(AIState.Chase);
            }

            // 状态执行
            switch (_currentState)
            {
                case AIState.Chase:
                    _moveDirection = ((Vector2)_player.position - _rb.position).normalized;
                    break;

                case AIState.Attack:
                    _moveDirection = Vector2.zero;
                    TryAttack(canContactPlayer, tickDelta);
                    break;
            }

            // 更新朝向（左右翻转）
            UpdateFacing();

            // 更新动画
            UpdateAnimation();
        }

        private void FixedUpdate()
        {
            if (!GameManager.Instance.IsPlaying) return;
            if (_enemy.IsDead || _moveDirection == Vector2.zero) return;

            Vector2 targetPos = _rb.position + _moveDirection * (MoveSpeed * Time.fixedDeltaTime);

            // 地图边界限制
            if (MapBounds.Instance != null)
                targetPos = MapBounds.Instance.ClampPosition(targetPos);

            _rb.MovePosition(targetPos);
        }

        private void UpdateFacing()
        {
            float dirX = _player.position.x - transform.position.x;

            // 翻转整个 root 的 localScale.x，确保所有子物体（Mount/Body/Wings）一起翻转
            Vector3 scale = transform.localScale;
            float targetX = scale.x;
            if (dirX > 0.1f)
                targetX = -Mathf.Abs(scale.x);  // 朝右
            else if (dirX < -0.1f)
                targetX = Mathf.Abs(scale.x);   // 朝左（默认）
            if (!Mathf.Approximately(scale.x, targetX))
            {
                scale.x = targetX;
                transform.localScale = scale;
            }
        }

        private void UpdateAnimation()
        {
            if (animator == null) return;

            bool isMoving = _moveDirection != Vector2.zero;
            bool isAttacking = _currentState == AIState.Attack;

            if (isMoving != _wasMoving)
            {
                animator.SetBool(ParamIsMoving, isMoving);
                _wasMoving = isMoving;
            }

            if (isAttacking != _wasAttacking)
            {
                animator.SetBool(ParamIsAttacking, isAttacking);
                _wasAttacking = isAttacking;
            }
        }

        private void TryAttack(bool touchingPlayer, float deltaTime)
        {
            _attackTimer += deltaTime;

            // 伤害窗口按真实攻击动画长度计算，而不是按攻击间隔计算。
            float normalizedTime = _attackTimer / Mathf.Max(0.01f, _attackAnimationDuration);
            if (!_hasDealtDamage && normalizedTime >= attackWindowStart && normalizedTime <= attackWindowEnd && touchingPlayer)
            {
                _hasDealtDamage = true;
                if (_playerHealth != null)
                    _playerHealth.TakeDamage(Mathf.RoundToInt(AttackDamage));
            }

            // 每个攻击周期都显式重播非循环 attack 动画，保证连续贴身时不会只攻击一次。
            float attackInterval = Mathf.Max(_attackAnimationDuration, AttackCooldown);
            if (_attackTimer >= attackInterval)
            {
                _attackTimer = 0f;
                _hasDealtDamage = false;
                RestartAttackAnimation();
            }
        }

        private void TransitionTo(AIState newState)
        {
            _currentState = newState;
            _attackTimer = 0f;
            _hasDealtDamage = false;
            if (newState == AIState.Attack)
                RestartAttackAnimation();
        }

        private bool IsPlayerInAttackContact()
        {
            if (_player == null) return false;
            if (_collider != null && _collider.enabled && _playerCollider != null && _playerCollider.enabled)
            {
                Bounds enemyBounds = _collider.bounds;
                Bounds playerBounds = _playerCollider.bounds;
                float dx = Mathf.Max(0f, Mathf.Abs(enemyBounds.center.x - playerBounds.center.x) -
                    enemyBounds.extents.x - playerBounds.extents.x - attackContactTolerance);
                float dy = Mathf.Max(0f, Mathf.Abs(enemyBounds.center.y - playerBounds.center.y) -
                    enemyBounds.extents.y - playerBounds.extents.y - attackContactTolerance);
                if (dx * dx + dy * dy > attackContactTolerance * attackContactTolerance)
                    return false;
                return _collider.Distance(_playerCollider).distance <= attackContactTolerance;
            }

            float radius = Mathf.Min(AttackRange, attackContactRadius);
            return ((Vector2)transform.position - (Vector2)_player.position).sqrMagnitude <= radius * radius;
        }

        private void RestartAttackAnimation()
        {
            if (animator == null) return;
            animator.SetBool(ParamIsMoving, false);
            animator.SetBool(ParamIsAttacking, true);
            if (animator.HasState(0, AttackState))
                animator.Play(AttackState, 0, 0f);
        }

        private void CacheAttackAnimationDuration()
        {
            if (animator == null || animator.runtimeAnimatorController == null) return;

            AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
            for (int i = 0; i < clips.Length; i++)
            {
                AnimationClip clip = clips[i];
                if (clip != null && string.Equals(clip.name, "attack", System.StringComparison.OrdinalIgnoreCase))
                {
                    _attackAnimationDuration = Mathf.Max(0.05f, clip.length);
                    return;
                }
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, DetectRange);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, AttackRange);
        }
#endif
    }
}
