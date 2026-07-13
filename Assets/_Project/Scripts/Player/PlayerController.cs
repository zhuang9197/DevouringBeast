using UnityEngine;

namespace DevouringBeast
{
    /// <summary>
    /// 玩家动画/行动状态
    /// </summary>
    public enum PlayerState
    {
        Idle,       // 静止（嘴里无东西）
        IdleFull,   // 静止（嘴里有东西）
        Run,        // 跑步（嘴里无东西）
        FullWalk,   // 满嘴走（嘴里有东西）
        SuckWindup, // 吸入第一阶段（充气）
        SuckLoop    // 吸入第二阶段（循环）
    }

    /// <summary>
    /// PlayerController — 玩家移动 + 动画状态机
    /// 完全代码驱动，基于 FrameAnimator 逐帧播放
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController : MonoBehaviour
    {
        [Header("移动")]
        [SerializeField] private float moveSpeed = 8f;
        [Range(0f, 1f), Tooltip("嘴里有东西时的移速倍率")]
        [SerializeField] private float fullWalkSpeedMultiplier = 0.8f;

        [Header("动画")]
        [SerializeField] private PlayerAnimData animData;
        [SerializeField] private FrameAnimator frameAnimator;
        [SerializeField] private SpriteRenderer spriteRenderer;

        [Header("吸入时移动")]
        [Tooltip("吸入时是否有「吸时行走」技能")]
        [SerializeField] private bool hasInhaleWalkSkill = false;
        [Tooltip("吸时行走时的移速倍率（相对基础移速）")]
        [SerializeField] private float inhaleWalkMultiplier = 0.5f;

        // 缓存
        private Rigidbody2D _rb;
        private Vector2 _moveInput;
        private bool _isInhaling;

        // 状态
        public Facing CurrentFacing { get; private set; } = Facing.Front;
        public PlayerState CurrentState { get; private set; } = PlayerState.Idle;
        private bool _suctionMaxed;

        // 上一帧状态追踪（避免每帧重复调用 Play）；初值设为 -1 确保首帧执行
        private Facing _lastAppliedFacing = (Facing)(-1);
        private PlayerState _lastAppliedState = (PlayerState)(-1);

        // 属性（兼容旧代码）
        public float MoveSpeed
        {
            get => moveSpeed;
            set => moveSpeed = value;
        }
        public bool IsInhaling
        {
            get => _isInhaling;
            set => _isInhaling = value;
        }
        public bool HasInhaleWalkSkill
        {
            get => hasInhaleWalkSkill;
            set => hasInhaleWalkSkill = value;
        }
        public Vector2 MoveDirection => _moveInput;
        public Vector2 FacingDirection
        {
            get
            {
                return CurrentFacing switch
                {
                    Facing.Front => Vector2.down,
                    Facing.Back => Vector2.up,
                    Facing.SideLeft => Vector2.left,
                    _ => Vector2.right
                };
            }
        }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            if (spriteRenderer == null)
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (frameAnimator == null)
                frameAnimator = GetComponentInChildren<FrameAnimator>();
        }

        // ============================================================
        // 移动
        // ============================================================

        private void FixedUpdate()
        {
            if (_moveInput == Vector2.zero) return;

            float speed = CalculateMoveSpeed();
            if (speed <= 0f) return;

            Vector2 targetPos = _rb.position + _moveInput * (speed * Time.fixedDeltaTime);

            // 地图边界限制
            if (MapBounds.Instance != null)
                targetPos = MapBounds.Instance.ClampPosition(targetPos);

            _rb.MovePosition(targetPos);
        }

        private float CalculateMoveSpeed()
        {
            if (_isInhaling)
            {
                // 吸入时：无技能不能动，有技能可以慢移
                if (!hasInhaleWalkSkill) return 0f;
                return moveSpeed * inhaleWalkMultiplier;
            }

            // 有东西时减速
            if (CurrentState == PlayerState.IdleFull || CurrentState == PlayerState.FullWalk)
                return moveSpeed * fullWalkSpeedMultiplier;

            return moveSpeed;
        }

        // ============================================================
        // 输入处理
        // ============================================================

        /// <summary>
        /// 由 InputManager 调用，设置移动输入
        /// 吸入期间：朝向锁定，输入只影响移动方向（若有技能）
        /// </summary>
        public void SetMoveInput(Vector2 input)
        {
            _moveInput = input.normalized;

            if (!_isInhaling)
            {
                // 非吸入状态：根据输入更新朝向
                UpdateFacing(input);
            }
            // 吸入状态：朝向锁定，不调用 UpdateFacing

            UpdateState();
        }

        private void UpdateFacing(Vector2 input)
        {
            if (input == Vector2.zero) return;

            if (Mathf.Abs(input.x) > Mathf.Abs(input.y))
            {
                CurrentFacing = input.x < 0 ? Facing.SideLeft : Facing.SideRight;
            }
            else
            {
                CurrentFacing = input.y > 0 ? Facing.Back : Facing.Front;
            }
        }

        // ============================================================
        // 状态机
        // ============================================================

        private void Update()
        {
            UpdateState();
        }

        private void UpdateState()
        {
            SwallowContainer container = GetComponent<SwallowContainer>();
            bool hasItems = container != null && container.HasItems;

            PlayerState newState;

            if (_isInhaling)
            {
                // 吸入状态：播 suck 动画，分两阶段
                newState = _suctionMaxed ? PlayerState.SuckLoop : PlayerState.SuckWindup;
            }
            else if (_moveInput != Vector2.zero)
            {
                // 移动中
                newState = hasItems ? PlayerState.FullWalk : PlayerState.Run;
            }
            else
            {
                // 静止
                newState = hasItems ? PlayerState.IdleFull : PlayerState.Idle;
            }

            CurrentState = newState;
            ApplyAnimation(hasItems);
        }

        /// <summary>
        /// 当吸力达到最大值时，由 PlayerInhale 调用
        /// </summary>
        public void SetSuctionMaxed(bool maxed)
        {
            _suctionMaxed = maxed;
        }

        // ============================================================
        // 动画应用
        // ============================================================

        private void ApplyAnimation(bool hasItems)
        {
            if (animData == null || frameAnimator == null) return;

            // 状态或朝向变化时才切换（避免每帧重复调用）
            if (CurrentState == _lastAppliedState && CurrentFacing == _lastAppliedFacing)
                return;

            _lastAppliedState = CurrentState;
            _lastAppliedFacing = CurrentFacing;

            // 设置 flipX（side 镜像）
            if (spriteRenderer != null)
            {
                spriteRenderer.flipX = CurrentFacing == Facing.SideRight;
            }

            switch (CurrentState)
            {
                case PlayerState.Idle:
                case PlayerState.IdleFull:
                    frameAnimator.Stop(animData.GetIdleSprite(CurrentFacing, hasItems));
                    break;

                case PlayerState.Run:
                    frameAnimator.Play(animData.GetRun(CurrentFacing), 0,
                        animData.GetRun(CurrentFacing).Length - 1, true);
                    break;

                case PlayerState.FullWalk:
                    frameAnimator.Play(animData.GetFullWalk(CurrentFacing), 0,
                        animData.GetFullWalk(CurrentFacing).Length - 1, true);
                    break;

                case PlayerState.SuckWindup:
                    {
                        var suck = animData.GetSuck(CurrentFacing);
                        int windupEnd = animData.GetSuckWindupEnd(CurrentFacing);
                        // 先完整播放张嘴段(0~windupEnd)，播完后循环最后4帧（嘴巴持续张大微动）
                        // 等吸力达到最大值后由 SuckLoop 接管
                        int windupLoopStart = Mathf.Max(0, windupEnd - 3);
                        frameAnimator.PlayThenLoop(suck, 0, windupEnd, windupLoopStart, windupEnd);
                    }
                    break;

                case PlayerState.SuckLoop:
                    {
                        var suck = animData.GetSuck(CurrentFacing);
                        int windupEnd = animData.GetSuckWindupEnd(CurrentFacing);
                        frameAnimator.Play(suck, windupEnd + 1, suck.Length - 1, true);
                    }
                    break;
            }
        }
    }
}
