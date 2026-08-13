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
        private float moveSpeed;
        private float fullWalkSpeedMultiplier;

        [Header("动画")]
        [SerializeField] private PlayerAnimData animData;
        [SerializeField] private FrameAnimator frameAnimator;
        [SerializeField] private SpriteRenderer spriteRenderer;

        [Header("吸入时移动")]
        [Tooltip("吸入时是否有「吸时行走」技能")]
        [SerializeField] private bool hasInhaleWalkSkill = false;
        [Tooltip("吸时行走时的移速倍率（相对基础移速）")]

        private float runStepInterval;
        private float walkStepInterval;
        private float idleSoundDelay;
        private float idleSoundRepeatInterval;
        private float inhaleWalkMultiplier;
        private float knockbackDuration;

        // 缓存
        private Rigidbody2D _rb;
        private PlayerBaseAttributes _baseAttributes;
        private SwallowContainer _swallowContainer;
        private Collider2D _movementCollider;
        private bool _movementColliderInitialTrigger;
        private Vector2 _moveInput;
        private readonly Collider2D[] _beastOverlapBuffer = new Collider2D[64];
        private readonly System.Collections.Generic.HashSet<int> _beastSpeedHitEnemies = new();
        private readonly System.Collections.Generic.HashSet<int> _beastSpeedContactsThisFrame = new();

        private float _footstepTimer;
        private float _idleTimer;

        private bool _isInhaling;
        private float _skillMoveSpeedMultiplier = 1f;
        private float _foodMoveSpeedMultiplier = 1f;
        private Coroutine _foodSpeedRoutine;
        private Coroutine _knockbackRoutine;
        private bool _isBeingKnockedBack;
        private bool _witchEnabled;
        private bool _beastForm;
        private int _witchLevel;
        private RogueSkillCatalog _rogueCatalog;
        private bool _beastRolling;
        private bool _beastHitThisFrame;
        private float _beastHitSoundCooldown;
        private Facing _lastBeastFacing = (Facing)(-1);
        private float beastRollingSpeedMultiplier;
        private float beastDamageReductionBase;
        private float beastDamageReductionPerLevel;
        private float beastDamageReductionLimit;
        private float beastHitRadius;
        private float beastDamagePerSecond;
        private float beastDamagePerLevel;
        private float beastHitSoundInterval;
        private float _beastEndTime;
        private float _beastSpeedBoost;
        private float _beastSpeedBoostEnd;

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
            get => _baseAttributes != null ? _baseAttributes.InitialMoveSpeed : moveSpeed;
            set
            {
                moveSpeed = value;
                if (_baseAttributes != null) _baseAttributes.InitialMoveSpeed = value;
                MovementSpeedSystem.SetPlayerSpeedUnit(value);
            }
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
        public float SkillMoveSpeedMultiplier { get => _skillMoveSpeedMultiplier; set => _skillMoveSpeedMultiplier = Mathf.Max(0f, value); }
        public bool IsBeastForm => _beastForm;
        public float BeastDamageReduction => !_beastForm ? 0f :
            Mathf.Min(beastDamageReductionLimit, beastDamageReductionBase +
                Mathf.Max(0, _witchLevel - 1) * beastDamageReductionPerLevel);
        public bool IsBeastRolling => _beastForm && _beastRolling;
        public float BeastRollingSpeedMultiplier => beastRollingSpeedMultiplier;
        public void ExtendBeastForm(float seconds) { if (_beastForm) _beastEndTime += Mathf.Max(0f, seconds); }
        public void ApplyBeastSpeedBoost(float amount, float duration)
        {
            _beastSpeedBoost = Mathf.Clamp(_beastSpeedBoost + Mathf.Max(0f, amount), 0f, 0.35f);
            _beastSpeedBoostEnd = Mathf.Max(_beastSpeedBoostEnd, Time.time + Mathf.Max(0f, duration));
        }
        public float RunStepInterval
        {
            get => runStepInterval;
            set => runStepInterval = Mathf.Max(0.05f, value);
        }
        public float WalkStepInterval
        {
            get => walkStepInterval;
            set => walkStepInterval = Mathf.Max(0.05f, value);
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
            PlayerBalanceSettings config = GameBalance.Current?.Player;
            if (config != null)
            {
                moveSpeed = config.baseMoveSpeed;
                fullWalkSpeedMultiplier = config.fullWalkSpeedMultiplier;
                inhaleWalkMultiplier = config.inhaleWalkSpeedMultiplier;
                runStepInterval = config.runStepInterval;
                walkStepInterval = config.walkStepInterval;
                idleSoundDelay = config.idleSoundDelay;
                idleSoundRepeatInterval = config.idleSoundRepeatInterval;
                knockbackDuration = config.knockbackDuration;
                beastRollingSpeedMultiplier = config.beastRollingSpeedMultiplier;
                beastDamageReductionBase = config.beastDamageReductionBase;
                beastDamageReductionPerLevel = config.beastDamageReductionPerLevel;
                beastDamageReductionLimit = config.beastDamageReductionLimit;
                beastHitRadius = config.beastHitRadius;
                beastDamagePerSecond = config.beastDamagePerSecond;
                beastDamagePerLevel = config.beastDamagePerLevel;
                beastHitSoundInterval = config.beastHitSoundCooldown;
            }
            _baseAttributes = GetComponent<PlayerBaseAttributes>();
            if (_baseAttributes == null) _baseAttributes = gameObject.AddComponent<PlayerBaseAttributes>();
            _baseAttributes.InitializeFromConfig();
            moveSpeed = _baseAttributes.InitialMoveSpeed;
            MovementSpeedSystem.SetPlayerSpeedUnit(moveSpeed);
            _rb = GetComponent<Rigidbody2D>();
            _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            _rb.useFullKinematicContacts = true;
            _movementCollider = GetComponent<Collider2D>();
            if (_movementCollider != null)
                _movementColliderInitialTrigger = _movementCollider.isTrigger;
            _swallowContainer = GetComponent<SwallowContainer>();
            if (spriteRenderer == null)
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (frameAnimator == null)
                frameAnimator = GetComponentInChildren<FrameAnimator>();
            ResizeMovementCollider(config);
        }

        private System.Collections.IEnumerator Start()
        {
            // FrameAnimator applies the first visible sprite after Awake.
            yield return null;
            ResizeMovementCollider(GameBalance.Current?.Player);
        }

        // ============================================================
        // 移动
        // ============================================================

        private void FixedUpdate()
        {
            if (!GameManager.Instance.IsPlaying) return;
            if (_isBeingKnockedBack) return;
            if (_moveInput == Vector2.zero) return;

            float speed = CalculateMoveSpeed();
            if (speed <= 0f) return;

            Vector2 targetPos = _rb.position + _moveInput * (speed * Time.fixedDeltaTime);

            // 地图边界限制
            if (MapBounds.Instance != null)
                targetPos = MapBounds.Instance.ClampPosition(targetPos);
            targetPos = StatueController.ConstrainMovement(_movementCollider, _rb.position, targetPos);

            _rb.MovePosition(targetPos);
        }

        private float CalculateMoveSpeed()
        {
            float currentMoveSpeed = _baseAttributes != null ? _baseAttributes.MoveSpeed : moveSpeed;
            if (_beastForm)
                return currentMoveSpeed * _skillMoveSpeedMultiplier *
                    _foodMoveSpeedMultiplier * (1f + (Time.time < _beastSpeedBoostEnd ? _beastSpeedBoost : 0f)) *
                    (_moveInput.sqrMagnitude > 0.001f ? beastRollingSpeedMultiplier : 1f);
            if (_isInhaling)
            {
                // 吸入时：无技能不能动，有技能可以慢移
                if (!hasInhaleWalkSkill) return 0f;
                return currentMoveSpeed * inhaleWalkMultiplier * _skillMoveSpeedMultiplier * _foodMoveSpeedMultiplier;
            }

            // 有东西时减速
            if (CurrentState == PlayerState.IdleFull || CurrentState == PlayerState.FullWalk)
                return currentMoveSpeed * fullWalkSpeedMultiplier * _skillMoveSpeedMultiplier * _foodMoveSpeedMultiplier;

            return currentMoveSpeed * _skillMoveSpeedMultiplier * _foodMoveSpeedMultiplier;
        }

        public void ApplyFoodSpeedBoost(float bonusPercent, float duration)
        {
            if (_foodSpeedRoutine != null) StopCoroutine(_foodSpeedRoutine);
            _foodMoveSpeedMultiplier = 1f + Mathf.Max(0f, bonusPercent);
            _foodSpeedRoutine = StartCoroutine(FoodSpeedRoutine(Mathf.Max(0f, duration)));
        }

        public void ApplyKnockback(Vector2 direction, float distance)
        {
            if (_rb == null || direction.sqrMagnitude <= 0.001f || distance <= 0f) return;
            if (_knockbackRoutine != null) return;
            Vector2 target = _rb.position + direction.normalized * distance;
            if (MapBounds.Instance != null) target = MapBounds.Instance.ClampPosition(target);
            target = StatueController.ConstrainMovement(_movementCollider, _rb.position, target);
            _knockbackRoutine = StartCoroutine(KnockbackRoutine(target, knockbackDuration));
        }

        private System.Collections.IEnumerator KnockbackRoutine(Vector2 target, float duration)
        {
            _isBeingKnockedBack = true;
            Vector2 start = _rb.position;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - (1f - t) * (1f - t);
                _rb.MovePosition(Vector2.LerpUnclamped(start, target, eased));
                yield return new WaitForFixedUpdate();
            }
            _rb.position = target;
            _isBeingKnockedBack = false;
            _knockbackRoutine = null;
        }

        private System.Collections.IEnumerator FoodSpeedRoutine(float duration)
        {
            yield return new WaitForSeconds(duration);
            _foodMoveSpeedMultiplier = 1f;
            _foodSpeedRoutine = null;
        }

        public void SetWitchFormEnabled(bool enabled, int level, RogueSkillCatalog catalog)
        {
            _witchEnabled = enabled;
            _witchLevel = level;
            _rogueCatalog = catalog;
        }

        public void EnterBeastForm(float duration = 8f)
        {
            if (!_witchEnabled || _beastForm) return;
            StartCoroutine(BeastRoutine(duration));
        }

        private System.Collections.IEnumerator BeastRoutine(float duration)
        {
            _beastForm = true;
            _beastSpeedBoost = 0f;
            _beastSpeedBoostEnd = 0f;
            _beastRolling = false;
            // 已有移动输入时立即切成触发器，避免变身首帧被实体碰撞挡住而漏掉首次伤害。
            SetBeastRollingCollision(_moveInput.sqrMagnitude > 0.001f);
            _beastHitThisFrame = false;
            _beastHitSoundCooldown = 0f;
            _lastBeastFacing = (Facing)(-1);
            _beastEndTime = Time.time + duration;
            // 立即结算一次固定帧伤害，确保开始变身时已经重叠的敌人不会漏掉第一次碰撞。
            ProcessBeastHits(Time.fixedDeltaTime);
            while (Time.time < _beastEndTime)
            {
                ProcessBeastHits(Time.deltaTime);
                yield return null;
            }
            AudioManager.Existing?.StopLoop(AudioCue.Roll);
            _beastForm = false;
            _beastSpeedBoost = 0f;
            _beastSpeedBoostEnd = 0f;
            _beastSpeedHitEnemies.Clear();
            _beastRolling = false;
            SetBeastRollingCollision(false);
            _lastBeastFacing = (Facing)(-1);
            _lastAppliedState = (PlayerState)(-1);
        }

        private void ProcessBeastHits(float damageDeltaTime)
        {
            int hitCount = Physics2D.OverlapCircleNonAlloc(transform.position, beastHitRadius, _beastOverlapBuffer);
            _beastHitThisFrame = false;
            _beastSpeedContactsThisFrame.Clear();
            for (int i = 0; i < hitCount; i++)
            {
                Collider2D hit = _beastOverlapBuffer[i];
                EnemyBase enemy = hit.GetComponentInParent<EnemyBase>();
                if (enemy != null && !enemy.IsDead)
                {
                    enemy.TakeDamage(beastDamagePerSecond *
                        (1f + _witchLevel * beastDamagePerLevel) * damageDeltaTime);
                    RogueSkillManager manager = RogueSkillManager.Active;
                    if (manager != null && manager.Has(RogueSkillId.WitchClaw))
                        EnemyStatusEffects.EnsureFor(enemy).ApplyPoison(3f + manager.GetLevel(RogueSkillId.WitchClaw) * 2f, 0.5f);
                    int enemyId = enemy.GetInstanceID();
                    if (manager != null && manager.Has(RogueSkillId.WitchDeterrence))
                    {
                        _beastSpeedContactsThisFrame.Add(enemyId);
                        if (!_beastSpeedHitEnemies.Contains(enemyId))
                        {
                            _beastSpeedHitEnemies.Add(enemyId);
                            ApplyBeastSpeedBoost(0.1f + Mathf.Max(0, manager.GetLevel(RogueSkillId.WitchDeterrence) - 1) * 0.1f, 3f);
                        }
                    }
                    _beastHitThisFrame = true;
                }
            }
            _beastSpeedHitEnemies.RemoveWhere(id => !_beastSpeedContactsThisFrame.Contains(id));
            if (_beastHitThisFrame && _beastHitSoundCooldown <= 0f)
            {
                AudioManager.Instance.PlaySfx(AudioCue.BeastHit);
                _beastHitSoundCooldown = beastHitSoundInterval;
            }
            _beastHitSoundCooldown -= damageDeltaTime;
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
            if (input.sqrMagnitude > 0.001f)
                NotifyPlayerActivity();
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
            if (!GameManager.Instance.IsPlaying)
            {
                _footstepTimer = 0f;
                _idleTimer = 0f;
                return;
            }
            UpdateState();
            UpdateAudio();
        }

        private void UpdateAudio()
        {
            if (_isInhaling)
            {
                AudioManager.Existing?.StopSfx(AudioCue.Idle);
                _footstepTimer = 0f;
                _idleTimer = 0f;
                return;
            }

            bool hasItems = _swallowContainer != null && _swallowContainer.HasItems;
            bool moving = _moveInput.sqrMagnitude > 0.001f;

            if (moving)
            {
                AudioManager.Existing?.StopSfx(AudioCue.Idle);
                _idleTimer = 0f;
                _footstepTimer -= Time.deltaTime;
                if (_footstepTimer <= 0f)
                {
                    AudioManager.Instance.PlaySfx(hasItems ? AudioCue.Walk : AudioCue.Run);
                    _footstepTimer = hasItems ? walkStepInterval : runStepInterval;
                }
                return;
            }

            _footstepTimer = 0f;
            if (hasItems)
            {
                _idleTimer = 0f;
                return;
            }

            _idleTimer += Time.deltaTime;
            if (_idleTimer >= idleSoundDelay)
            {
                AudioManager.Instance.PlaySfx(AudioCue.Idle);
                _idleTimer = idleSoundDelay - idleSoundRepeatInterval;
            }
        }

        public void NotifyPlayerActivity()
        {
            _idleTimer = 0f;
            AudioManager.Existing?.StopSfx(AudioCue.Idle);
        }


        private void UpdateState()
        {
            bool hasItems = _swallowContainer != null && _swallowContainer.HasItems;

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

            if (_beastForm && _rogueCatalog != null)
            {
                Sprite[] frames = CurrentFacing switch
                {
                    Facing.Front => _rogueCatalog.beastFrontRoll,
                    Facing.Back => _rogueCatalog.beastBackRoll,
                    _ => _rogueCatalog.beastSideRoll
                };
                if (spriteRenderer != null) spriteRenderer.flipX = CurrentFacing == Facing.SideRight;
                bool moving = _moveInput.sqrMagnitude > 0.001f;
                if (!moving)
                {
                    Sprite idle = CurrentFacing switch
                    {
                        Facing.Front => _rogueCatalog.beastFront,
                        Facing.Back => _rogueCatalog.beastBack,
                        _ => _rogueCatalog.beastSide
                    };
                    frameAnimator.Stop(idle);
                    _beastRolling = false;
                    SetBeastRollingCollision(false);
                    AudioManager.Existing?.StopLoop(AudioCue.Roll);
                    _lastBeastFacing = CurrentFacing;
                    return;
                }

                GetBeastAnimationRange(CurrentFacing, frames, out int introEnd, out int loopStart, out int loopEnd);
                if (!_beastRolling)
                {
                    frameAnimator.PlayThenLoop(frames, 0, introEnd, loopStart, loopEnd);
                    _beastRolling = true;
                    SetBeastRollingCollision(true);
                    AudioManager.Instance.PlayLoop(AudioCue.Roll);
                }
                else if (_lastBeastFacing != CurrentFacing)
                {
                    frameAnimator.Play(frames, loopStart, loopEnd, true);
                }
                _lastBeastFacing = CurrentFacing;
                return;
            }

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

        private void SetBeastRollingCollision(bool rolling)
        {
            if (_movementCollider != null)
                _movementCollider.isTrigger = rolling || _movementColliderInitialTrigger;
        }

        private void ResizeMovementCollider(PlayerBalanceSettings config)
        {
            if (_movementCollider is not CircleCollider2D circle || config == null) return;
            circle.radius = Mathf.Max(circle.radius, config.minimumColliderRadius);
            if (spriteRenderer == null || spriteRenderer.sprite == null) return;

            Vector2 visualSize = spriteRenderer.bounds.size;
            float rootScale = Mathf.Max(0.0001f,
                Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y)));
            float visualRadius = Mathf.Min(visualSize.x, visualSize.y) * 0.5f *
                config.visualColliderRadiusScale / rootScale;
            circle.radius = Mathf.Max(circle.radius, visualRadius);
        }

        private void OnDisable()
        {
            SetBeastRollingCollision(false);
        }

        private static void GetBeastAnimationRange(Facing facing, Sprite[] frames,
            out int introEnd, out int loopStart, out int loopEnd)
        {
            int last = frames != null ? Mathf.Max(0, frames.Length - 1) : 0;
            switch (facing)
            {
                case Facing.Back:
                    introEnd = Mathf.Min(7, last); loopStart = Mathf.Min(8, last); break;
                case Facing.Front:
                    introEnd = Mathf.Min(6, last); loopStart = Mathf.Min(7, last); break;
                default:
                    introEnd = Mathf.Min(4, last); loopStart = Mathf.Min(5, last); break;
            }
            loopEnd = last;
        }
    }
}
