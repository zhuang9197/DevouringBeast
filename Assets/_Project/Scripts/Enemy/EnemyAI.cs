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

        private EnemyBase _enemy;
        private Rigidbody2D _rb;
        private Transform _player;

        private AIState _currentState = AIState.Chase;
        private float _attackTimer;
        private Vector2 _moveDirection;
        private bool _wasMoving;
        private bool _hasDealtDamage; // 当前攻击周期是否已造成伤害

        // Animator 参数 ID
        private static readonly int ParamIsMoving = Animator.StringToHash("IsMoving");
        private static readonly int ParamIsAttacking = Animator.StringToHash("IsAttacking");

        private float MoveSpeed => _enemy.Data != null ? _enemy.Data.moveSpeed : 3f;
        private float DetectRange => _enemy.Data != null ? _enemy.Data.detectRange : 8f;
        private float AttackRange => _enemy.Data != null ? _enemy.Data.attackRange : 1.5f;
        private float AttackCooldown => _enemy.Data != null ? _enemy.Data.attackCooldown : 1.5f;
        private float AttackDamage => _enemy.Data != null ? _enemy.Data.attackDamage : 10f;

        private void Awake()
        {
            _enemy = GetComponent<EnemyBase>();
            _rb = GetComponent<Rigidbody2D>();
            if (animator == null)
                animator = GetComponentInChildren<Animator>();
            if (spriteRenderer == null)
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        private void Start()
        {
            _player = GameObject.FindGameObjectWithTag("Player")?.transform;
        }

        private void Update()
        {
            if (_enemy.IsDead || _player == null) return;

            float dist = Vector2.Distance(transform.position, _player.position);

            // 状态决策：始终追击玩家，只有在攻击范围内才攻击
            if (dist <= AttackRange)
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
                    TryAttack();
                    break;
            }

            // 更新朝向（左右翻转）
            UpdateFacing();

            // 更新动画
            UpdateAnimation();
        }

        private void FixedUpdate()
        {
            if (_enemy.IsDead || _moveDirection == Vector2.zero) return;

            Vector2 targetPos = _rb.position + _moveDirection * (MoveSpeed * Time.fixedDeltaTime);

            // 地图边界限制
            if (MapBounds.Instance != null)
                targetPos = MapBounds.Instance.ClampPosition(targetPos);

            _rb.MovePosition(targetPos);
        }

        private void UpdateFacing()
        {
            if (spriteRenderer == null) return;

            float dirX = _player.position.x - transform.position.x;

            // 精灵默认朝左：玩家在左(dirX<0)→不翻转(朝左)，玩家在右(dirX>0)→翻转(朝右)
            if (dirX > 0.1f)
                spriteRenderer.flipX = true;  // 朝右
            else if (dirX < -0.1f)
                spriteRenderer.flipX = false; // 朝左
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

            animator.SetBool(ParamIsAttacking, isAttacking);
        }

        private void TryAttack()
        {
            _attackTimer += Time.deltaTime;

            // 在攻击动画播放到约 40% 时造成伤害（与动画挥击帧同步）
            if (!_hasDealtDamage && _attackTimer >= AttackCooldown * 0.4f)
            {
                _hasDealtDamage = true;
                var playerHealth = _player.GetComponent<PlayerHealth>();
                if (playerHealth != null)
                    playerHealth.TakeDamage(Mathf.RoundToInt(AttackDamage));
            }

            // 冷却结束，重置标记准备下一次攻击
            if (_attackTimer >= AttackCooldown)
            {
                _attackTimer = 0f;
                _hasDealtDamage = false;
            }
        }

        private void TransitionTo(AIState newState)
        {
            _currentState = newState;
            _attackTimer = 0f;
            _hasDealtDamage = false;
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
