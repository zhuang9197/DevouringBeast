using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DevouringBeast
{
    public enum WhiteEnemyVariant { White, Red, Purple, Yellow, Green, Blue, Pink }

    /// <summary>
    /// Data-driven runtime behavior shared by all enemies in the new sprite set.
    /// The archetype selects movement, attacks, phase changes and death rewards.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyBase), typeof(Rigidbody2D))]
    public sealed class EnemyActor : MonoBehaviour
    {
        private static readonly HashSet<EnemyActor> ActiveActors = new();

        [SerializeField] private Animator animator;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private WhiteEnemyVariant forcedWhiteVariant;

        private EnemyBase _enemy;
        private Rigidbody2D _body;
        private Collider2D _collider;
        private Transform _player;
        private PlayerHealth _playerHealth;
        private PlayerController _playerController;
        private PlayerInhale _playerInhale;
        private Collider2D _playerCollider;
        private Vector2 _moveDirection;
        private Vector2 _spawnPoint;
        private Vector2 _wanderDirection;
        private float _attackTimer;
        private float _contactTimer;
        private float _movementTimer;
        private float _movementSpeedRatio;
        private float _hitMassProgress;
        private int _healthLossEffectCount;
        private int _hitCount;
        private int _actionIndex;
        private int _dashCount = 3;
        private bool _busy;
        private bool _phaseTwo;
        private bool _skeletonHead;
        private bool _redExplosionTriggered;
        private float _steeringPhase;
        private float _steeringSpeed;
        private float _steeringRadius;
        private float _steeringSide;
        private Vector3 _visualRestingPosition;
        private Vector3 _visualRestingScale;
        private Coroutine _massGrowthRoutine;
        private WhiteEnemyVariant _primaryVariant;
        private WhiteEnemyVariant _secondaryVariant;
        private EnemyCommonBalanceSettings _commonBalance;

        public EnemyArchetype Archetype => _enemy != null && _enemy.Data != null
            ? _enemy.Data.archetype : EnemyArchetype.Bat;
        public bool IsYellowVariant => HasVariant(WhiteEnemyVariant.Yellow);
        private EnemyBehaviorSettings Behavior => _enemy != null && _enemy.Data != null
            ? _enemy.Data.behavior : null;

        private void Awake()
        {
            _commonBalance = GameBalance.Current?.Enemy;
            _enemy = GetComponent<EnemyBase>();
            _body = GetComponent<Rigidbody2D>();
            _collider = GetComponent<Collider2D>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                _visualRestingPosition = spriteRenderer.transform.localPosition;
                _visualRestingScale = spriteRenderer.transform.localScale;
            }
            _body.bodyType = RigidbodyType2D.Kinematic;
            _body.gravityScale = 0f;
            _body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            _body.interpolation = RigidbodyInterpolation2D.Interpolate;
            _body.useFullKinematicContacts = true;
        }

        private void OnEnable() => ActiveActors.Add(this);

        private void OnDisable()
        {
            ActiveActors.Remove(this);
            if (Archetype == EnemyArchetype.Baby)
                AudioManager.Existing?.StopIntervalLoop(AudioCue.BabyCry);
        }

        public void SetSpawnPosition(Vector2 position)
        {
            _spawnPoint = position;
            _body.position = position;
            transform.position = position;
            Physics2D.SyncTransforms();
        }

        public void ResetForReuse()
        {
            StopAllCoroutines();
            _spawnPoint = transform.position;
            _moveDirection = Vector2.zero;
            _wanderDirection = Random.insideUnitCircle.normalized;
            Vector2 initialDelay = _commonBalance != null
                ? _commonBalance.initialAttackDelayRange : Vector2.zero;
            _attackTimer = Random.Range(initialDelay.x, initialDelay.y);
            _contactTimer = 0f;
            _movementTimer = 0f;
            _movementSpeedRatio = _enemy != null && _enemy.Data != null ? _enemy.Data.moveSpeed : 1f;
            _hitMassProgress = 0f;
            _healthLossEffectCount = 0;
            _hitCount = 0;
            _actionIndex = 0;
            _dashCount = 3;
            _busy = false;
            _phaseTwo = false;
            _skeletonHead = false;
            _redExplosionTriggered = false;
            _massGrowthRoutine = null;
            _body.bodyType = RigidbodyType2D.Kinematic;
            _body.gravityScale = 0f;
            _body.velocity = Vector2.zero;
            _body.angularVelocity = 0f;
            _body.constraints = RigidbodyConstraints2D.FreezeRotation;
            _steeringPhase = Random.Range(0f, Mathf.PI * 2f);
            Vector2 steeringSpeed = _commonBalance != null
                ? _commonBalance.steeringSpeedRange : Vector2.zero;
            Vector2 steeringRadius = _commonBalance != null
                ? _commonBalance.steeringRadiusRange : Vector2.zero;
            _steeringSpeed = Random.Range(steeringSpeed.x, steeringSpeed.y);
            _steeringRadius = Random.Range(steeringRadius.x, steeringRadius.y);
            _steeringSide = Random.value < 0.5f ? -1f : 1f;
            if (_collider != null) _collider.enabled = true;
            if (animator != null)
            {
                animator.enabled = true;
                animator.speed = 1f;
            }
            if (spriteRenderer != null)
            {
                spriteRenderer.enabled = true;
                spriteRenderer.color = Color.white;
                spriteRenderer.transform.localPosition = _visualRestingPosition;
                spriteRenderer.transform.localScale = _visualRestingScale;
            }
            ResizeColliderToVisual();
            GetComponent<GroundShadow>()?.SetVisible(true);
            ConfigureWhiteVariants();
            FindPlayer();
        }

        private void FindPlayer()
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            _player = playerObject != null ? playerObject.transform : null;
            _playerHealth = playerObject != null ? playerObject.GetComponent<PlayerHealth>() : null;
            _playerController = playerObject != null ? playerObject.GetComponent<PlayerController>() : null;
            _playerInhale = playerObject != null ? playerObject.GetComponent<PlayerInhale>() : null;
            _playerCollider = playerObject != null ? playerObject.GetComponent<Collider2D>() : null;
        }

        private void Update()
        {
            if (_enemy == null || _enemy.IsDead || !GameManager.Instance.IsPlaying) return;
            if (_player == null) FindPlayer();
            if (_player == null) return;
            UpdateRedVariant();

            _contactTimer -= Time.deltaTime;
            _attackTimer -= Time.deltaTime;
            if (!_busy) UpdateMovement();
            UpdateFacingAndAnimation();
            TryContactDamage();
            if (!_busy && _attackTimer <= 0f) TriggerArchetypeAttack();
            UpdateBlueInvisibility();
        }

        private void FixedUpdate()
        {
            if (_enemy == null || _enemy.IsDead || _busy || _moveDirection == Vector2.zero) return;
            float speed = MovementSpeedSystem.EnemyToWorld(_movementSpeedRatio);
            Vector2 target = _body.position + _moveDirection * (speed * Time.fixedDeltaTime);
            if (MapBounds.Instance != null) target = MapBounds.Instance.ClampPosition(target);
            target = StatueController.ConstrainMovement(_collider, _body.position, target);
            target = ConstrainAgainstPlayer(target);
            _body.MovePosition(target);
        }

        private Vector2 ConstrainAgainstPlayer(Vector2 target)
        {
            if (_player == null || _collider == null || !_collider.enabled ||
                _playerCollider == null || !_playerCollider.enabled || _playerCollider.isTrigger ||
                (_playerController != null && _playerController.IsBeastRolling)) return target;

            float enemyRadius = Mathf.Min(_collider.bounds.extents.x, _collider.bounds.extents.y);
            float playerRadius = Mathf.Min(_playerCollider.bounds.extents.x, _playerCollider.bounds.extents.y);
            float minimumDistance = Mathf.Max(0.05f, enemyRadius + playerRadius);
            Vector2 away = target - (Vector2)_player.position;
            if (away.sqrMagnitude >= minimumDistance * minimumDistance) return target;
            if (away.sqrMagnitude <= 0.0001f)
            {
                away = _body.position - (Vector2)_player.position;
                if (away.sqrMagnitude <= 0.0001f)
                    away = new Vector2(Mathf.Cos(_steeringPhase), Mathf.Sin(_steeringPhase));
            }
            return (Vector2)_player.position + away.normalized * minimumDistance;
        }

        private void LateUpdate()
        {
            if (_enemy == null || _enemy.IsDead || Archetype != EnemyArchetype.GroundWorm) return;
            if (((Vector2)transform.position - _spawnPoint).sqrMagnitude <= 0.0001f) return;
            _body.position = _spawnPoint;
            transform.position = _spawnPoint;
            Physics2D.SyncTransforms();
        }

        private void UpdateMovement()
        {
            Vector2 toPlayer = (Vector2)_player.position - _body.position;
            EnemyArchetype type = Archetype;
            _movementSpeedRatio = _enemy.Data != null ? _enemy.Data.moveSpeed : 1f;
            if (type == EnemyArchetype.Baby || type == EnemyArchetype.Satan ||
                type == EnemyArchetype.MeatMountain || type == EnemyArchetype.GroundWorm ||
                (type == EnemyArchetype.LittleSatan && _phaseTwo))
            {
                _moveDirection = Vector2.zero;
                return;
            }

            if (type == EnemyArchetype.Bat)
                _moveDirection = GetBatCirclingDirection(toPlayer);
            else if (type == EnemyArchetype.Fly)
                _moveDirection = GetFlyCirclingDirection();
            else if (type == EnemyArchetype.Mushroom)
                Wander(NextWanderInterval());
            else if (type == EnemyArchetype.BloodBag)
                UpdateBloodBagMovement(toPlayer);
            else if (type == EnemyArchetype.HomeSpider)
            {
                float proximity = Behavior != null ? Behavior.proximityRange : 0f;
                if (toPlayer.sqrMagnitude < proximity * proximity)
                {
                    _moveDirection = -toPlayer.normalized;
                    _movementSpeedRatio = Behavior != null ? Behavior.specialMoveSpeed : _movementSpeedRatio;
                }
                else Wander(NextWanderInterval());
            }
            else if (type == EnemyArchetype.BigSpider)
            {
                _movementTimer -= Time.deltaTime;
                if (_movementTimer <= 0f)
                {
                    _actionIndex++;
                    _movementTimer = (_actionIndex % 2 == 0)
                        ? Behavior.movementIdleDuration : Behavior.movementActiveDuration;
                    _wanderDirection = _actionIndex % 2 == 0 ? Vector2.zero : Random.insideUnitCircle.normalized;
                    if (Behavior.actionsPerSpecial > 0 && _actionIndex % Behavior.actionsPerSpecial == 0 && !_busy)
                        StartCoroutine(SpawnOvaryRoutine());
                }
                _moveDirection = _wanderDirection;
            }
            else if (type == EnemyArchetype.Spider)
            {
                _movementTimer = (_movementTimer + Time.deltaTime) % Behavior.movementCycleDuration;
                _moveDirection = _movementTimer < Behavior.movementActiveDuration
                    ? ApplyChaseSteering(toPlayer.normalized) : Vector2.zero;
                if (toPlayer.sqrMagnitude < Behavior.proximityRange * Behavior.proximityRange && _attackTimer <= 0f)
                    StartCoroutine(SpiderJumpRoutine());
            }
            else
                _moveDirection = ApplyChaseSteering(toPlayer.normalized);
        }

        private Vector2 GetBatCirclingDirection(Vector2 toPlayer)
        {
            float angle = _steeringPhase + Time.time * Behavior.orbitAngularSpeed * _steeringSide;
            Vector2 tangent = new(-Mathf.Sin(angle) * _steeringSide, Mathf.Cos(angle) * _steeringSide);
            Vector2 pursuit = toPlayer.sqrMagnitude > 0.001f ? toPlayer.normalized : Vector2.zero;
            Vector2 velocity = pursuit * Behavior.orbitPursuitWeight +
                tangent * Behavior.orbitTangentWeight +
                CalculateSeparation() * Behavior.orbitSeparationWeight;
            return Vector2.ClampMagnitude(velocity, 1f);
        }

        private Vector2 GetFlyCirclingDirection()
        {
            float radius = Mathf.Max(0.5f, Behavior != null ? Behavior.proximityRange : 0f);
            Vector2 offset = _body.position - _spawnPoint;
            float distance = offset.magnitude;
            Vector2 radial = distance > 0.05f
                ? offset / distance
                : new Vector2(Mathf.Cos(_steeringPhase), Mathf.Sin(_steeringPhase));
            Vector2 tangent = new Vector2(-radial.y, radial.x) * _steeringSide;
            float radialCorrection = Mathf.Clamp((radius - distance) / radius, -1f, 1f);
            float tangentWeight = Mathf.Max(0.2f, Behavior != null ? Behavior.orbitTangentWeight : 1f);
            float correctionWeight = Mathf.Max(0.75f,
                Behavior != null ? Behavior.orbitAngularSpeed * 0.25f : 1f);
            Vector2 velocity = tangent * tangentWeight + radial * (radialCorrection * correctionWeight);
            if (_commonBalance != null)
                velocity += CalculateSeparation() * Mathf.Max(0f,
                    Behavior != null ? Behavior.orbitSeparationWeight : 0f);
            return velocity.sqrMagnitude > 0.001f ? velocity.normalized : tangent;
        }

        private Vector2 ApplyChaseSteering(Vector2 baseDirection)
        {
            float angle = Time.time * _steeringSpeed + _steeringPhase;
            Vector2 offset = new(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 irregularTarget = (Vector2)_player.position + offset * _steeringRadius;
            Vector2 irregularDirection = (irregularTarget - _body.position).normalized;
            Vector2 separation = CalculateSeparation();
            Vector2 combined = baseDirection.normalized * _commonBalance.chaseWeight +
                irregularDirection * _commonBalance.irregularChaseWeight +
                separation * _commonBalance.separationWeight;
            return combined.sqrMagnitude > 0.001f ? combined.normalized : baseDirection.normalized;
        }

        private Vector2 CalculateSeparation()
        {
            float radius = _commonBalance.separationRadius;
            Vector2 force = Vector2.zero;
            foreach (EnemyActor other in ActiveActors)
            {
                if (other == null || other == this || other._enemy == null || other._enemy.IsDead) continue;
                Vector2 delta = _body.position - other._body.position;
                float distanceSquared = delta.sqrMagnitude;
                if (distanceSquared <= 0.0001f)
                    force += new Vector2(Mathf.Cos(_steeringPhase), Mathf.Sin(_steeringPhase));
                else if (distanceSquared < radius * radius)
                    force += delta.normalized * (1f - Mathf.Sqrt(distanceSquared) / radius);
            }
            return Vector2.ClampMagnitude(force, 1f);
        }

        private void Wander(float interval)
        {
            _movementTimer -= Time.deltaTime;
            if (_movementTimer <= 0f)
            {
                _movementTimer = interval;
                _wanderDirection = Random.insideUnitCircle.normalized;
            }
            _moveDirection = _wanderDirection;
        }

        private float NextWanderInterval()
        {
            if (Behavior == null) return 0f;
            return Random.Range(Behavior.wanderIntervalRange.x, Behavior.wanderIntervalRange.y);
        }

        private void UpdateBloodBagMovement(Vector2 toPlayer)
        {
            float fleeDistanceSquared = Behavior.proximityRange * Behavior.proximityRange;
            bool isFleeing = toPlayer.sqrMagnitude < fleeDistanceSquared;
            Vector2 desiredDirection;

            if (isFleeing)
            {
                desiredDirection = toPlayer.sqrMagnitude > 0.001f
                    ? -toPlayer.normalized
                    : _wanderDirection;
                _movementTimer = 0f;
            }
            else
            {
                _movementTimer -= Time.deltaTime;
                if (_movementTimer <= 0f)
                {
                    _movementTimer = NextWanderInterval();
                    Vector2 previousDirection = _wanderDirection.sqrMagnitude > 0.001f
                        ? _wanderDirection.normalized
                        : Random.insideUnitCircle.normalized;
                    float turnAngle = Random.Range(-Behavior.wanderMaximumTurnAngle, Behavior.wanderMaximumTurnAngle);
                    _wanderDirection = Quaternion.Euler(0f, 0f, turnAngle) * previousDirection;
                }
                desiredDirection = _wanderDirection;
            }

            if (desiredDirection.sqrMagnitude <= 0.001f) return;
            Vector2 currentDirection = _moveDirection.sqrMagnitude > 0.001f
                ? _moveDirection.normalized
                : desiredDirection.normalized;
            float turnSpeed = isFleeing ? Behavior.evasiveTurnSpeed : Behavior.wanderTurnSpeed;
            _moveDirection = Vector3.RotateTowards(currentDirection, desiredDirection.normalized,
                turnSpeed * Mathf.Deg2Rad * Time.deltaTime, 0f);
        }

        private void TriggerArchetypeAttack()
        {
            switch (Archetype)
            {
                case EnemyArchetype.Baby:
                    StartCoroutine(BabyAttackRoutine()); break;
                case EnemyArchetype.SkeletonMan:
                    StartCoroutine(SkeletonManAttackRoutine()); break;
                case EnemyArchetype.LittleSatan:
                    StartCoroutine(_phaseTwo
                        ? LittleSatanDashRoutine()
                        : FireRainRoutine(4, false)); break;
                case EnemyArchetype.Satan:
                    StartCoroutine(FireRainRoutine(5, true)); break;
                case EnemyArchetype.MeatMountain:
                    StartCoroutine(MeatMountainAttackRoutine()); break;
                case EnemyArchetype.Skeleton:
                    ShootAtPlayer(); ResetAttackTimer(); break;
                case EnemyArchetype.GroundWorm:
                    StartCoroutine(GroundWormRoutine()); break;
                case EnemyArchetype.Gloomy:
                    ResetAttackTimer(); break;
                default:
                    ResetAttackTimer();
                    break;
            }
        }

        private IEnumerator BabyAttackRoutine()
        {
            _busy = true;
            bool scream = (_actionIndex++ & 1) == 0;
            PlayState(scream ? "SkillA" : "SkillB");
            AudioManager.Instance.PlayIntervalLoop(AudioCue.BabyCry, 0.3f);
            if (scream)
            {
                yield return new WaitForSeconds(0.5f);
                if (WaveManager.Instance == null || WaveManager.Instance.ActiveLivingEnemyCount <= 15)
                {
                    DamagePlayerInRadius(3.5f, 1, false);
                    Summon(EnemyArchetype.Fly, 2); Summon(EnemyArchetype.Bat, 2); Summon(EnemyArchetype.Gloomy, 1);
                    if (Random.value < 0.05f) Summon(EnemyArchetype.BigSpider, 1);
                }
                yield return new WaitForSeconds(2f);
            }
            else
            {
                yield return new WaitForSeconds(0.5f);
                for (int wave = 0; wave < 3; wave++)
                {
                    ShootRadial(16, 1f);
                    yield return new WaitForSeconds(1f);
                }
            }
            AudioManager.Existing?.StopIntervalLoop(AudioCue.BabyCry);
            _busy = false;
            ResetAttackTimer();
        }

        private IEnumerator SkeletonManAttackRoutine()
        {
            _busy = true;
            bool hasBat = WaveManager.Instance != null && WaveManager.Instance.HasLivingArchetype(EnemyArchetype.Bat);
            if (!hasBat)
            {
                PlayState("SkillA");
                for (int i = 0; i < 3; i++) { ShootRadial(8, 1f); yield return new WaitForSeconds(0.5f); }
                Summon(EnemyArchetype.Bat, 3);
            }
            else
                yield return DashRoutineInternal(GetDashSpeed(), Behavior.dashDuration, "SkillB");
            _busy = false;
            ResetAttackTimer();
        }

        private IEnumerator FireRainRoutine(int count, bool radialOnLand)
        {
            _busy = true;
            PlayState("SkillA");
            bool invulnerableInAir = Archetype == EnemyArchetype.Satan;
            if (Archetype == EnemyArchetype.Satan)
                AudioManager.Instance.PlaySfx(AudioCue.SatanLaugh);
            if (invulnerableInAir) _enemy.IsInvulnerable = true;
            yield return MoveVisualHeight(0f, Behavior.jumpHeight, Behavior.takeoffDuration, true);
            float airborneStarted = Time.time;
            for (int i = 0; i < count; i++)
            {
                Vector2 center = MapBounds.Instance != null ? (MapBounds.Instance.Min + MapBounds.Instance.Max) * 0.5f : (Vector2)transform.position;
                Vector2 half = MapBounds.Instance != null ? (MapBounds.Instance.Max - MapBounds.Instance.Min) * 0.42f : Vector2.one * 5f;
                Vector2 target = center + new Vector2(Random.Range(-half.x, half.x), Random.Range(-half.y, half.y));
                SpawnFireball(target, radialOnLand ? 4 : 0);
                yield return new WaitForSeconds(0.45f);
            }
            float remainingAirTime = Behavior.airborneDuration - (Time.time - airborneStarted);
            if (remainingAirTime > 0f) yield return new WaitForSeconds(remainingAirTime);
            yield return MoveVisualHeight(Behavior.jumpHeight, 0f, Behavior.landingDuration, false);
            if (invulnerableInAir) _enemy.IsInvulnerable = false;
            _busy = false;
            ResetAttackTimer();
        }

        private IEnumerator MeatMountainAttackRoutine()
        {
            _busy = true;
            bool slam = (_actionIndex++ % 3) != 2;
            PlayState(slam ? "SkillB" : "SkillA");
            if (slam)
            {
                _enemy.IsInvulnerable = true;
                if (_collider != null) _collider.enabled = false;
                float offscreenWorldY = GetVisibleTopY() + Mathf.Max(0f, Behavior.offscreenPadding);
                float offscreenHeight = Mathf.Max(Behavior.jumpHeight, offscreenWorldY - _body.position.y);
                offscreenWorldY = _body.position.y + offscreenHeight;
                yield return MoveVisualHeight(0f, offscreenHeight, Behavior.takeoffDuration, true);
                Vector2 landingPosition = _player != null ? (Vector2)_player.position : _body.position;
                if (MapBounds.Instance != null) landingPosition = MapBounds.Instance.ClampPosition(landingPosition);
                _body.position = landingPosition;
                transform.position = landingPosition;
                float relocatedHeight = Mathf.Max(Behavior.jumpHeight, offscreenWorldY - landingPosition.y);
                SetVisualHeight(relocatedHeight);
                Physics2D.SyncTransforms();
                if (Behavior.airborneDuration > 0f) yield return new WaitForSeconds(Behavior.airborneDuration);
                yield return MoveVisualHeight(relocatedHeight, 0f, Behavior.landingDuration, false);
                if (_collider != null) _collider.enabled = true;
                Physics2D.SyncTransforms();
                _enemy.IsInvulnerable = false;
                AudioManager.Instance.PlaySfx(AudioCue.MeatMountainLand);
                SpawnMeatMountainLandingVfx();
                Camera.main?.GetComponent<CameraFollow>()?.Shake(0.5f, 0.22f);
                DamagePlayerInRadius(2f, 4, true);
                ShootRadial(16, 1f);
            }
            else
            {
                for (int i = 0; i < 3; i++) { ShootRadial(8, 1f); yield return new WaitForSeconds(0.6f); }
            }
            _busy = false;
            ResetAttackTimer();
        }

        private IEnumerator GroundWormRoutine()
        {
            _busy = true;
            Vector2 burrowPosition = _body.position;
            PlayState("SkillA");
            yield return HoldEnemyPosition(burrowPosition, 0.5f);
            if (spriteRenderer != null) spriteRenderer.enabled = false;
            if (_collider != null) _collider.enabled = false;
            GetComponent<GroundShadow>()?.SetVisible(false);
            yield return HoldEnemyPosition(burrowPosition, 1f);
            Vector2 emergencePosition = _body.position;
            if (MapBounds.Instance != null)
                emergencePosition = ChooseGroundWormEmergencePosition();
            _spawnPoint = emergencePosition;
            _body.position = emergencePosition;
            transform.position = emergencePosition;
            Physics2D.SyncTransforms();
            if (spriteRenderer != null) spriteRenderer.enabled = true;
            GetComponent<GroundShadow>()?.SetVisible(true);
            yield return PlayGroundWormEmergence(emergencePosition);
            _body.position = emergencePosition;
            transform.position = emergencePosition;
            Physics2D.SyncTransforms();
            if (_collider != null) _collider.enabled = true;
            ShootAtPlayer();
            yield return FireShakeRoutine(0.22f, 0.13f);
            _busy = false;
            ResetAttackTimer();
        }

        private Vector2 ChooseGroundWormEmergencePosition()
        {
            Vector2 minimum = MapBounds.Instance.Min + Vector2.one;
            Vector2 maximum = MapBounds.Instance.Max - Vector2.one;
            Vector2 result = _body.position;
            for (int i = 0; i < 8; i++)
            {
                result = new Vector2(Random.Range(minimum.x, maximum.x), Random.Range(minimum.y, maximum.y));
                if (_player == null || ((Vector2)_player.position - result).sqrMagnitude >= 6.25f) break;
            }
            return MapBounds.Instance.ClampPosition(result);
        }

        private IEnumerator HoldEnemyPosition(Vector2 position, float duration)
        {
            float elapsed = 0f;
            int frameGuard = 0;
            while (elapsed < duration && frameGuard++ < 240)
            {
                _body.position = position;
                transform.position = position;
                Physics2D.SyncTransforms();
                elapsed += Mathf.Max(Time.unscaledDeltaTime, 1f / 60f);
                yield return null;
            }
            _body.position = position;
            transform.position = position;
            Physics2D.SyncTransforms();
        }

        private IEnumerator PlayGroundWormEmergence(Vector2 position)
        {
            AnimationClip burrowClip = null;
            if (animator != null && animator.runtimeAnimatorController != null)
                foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
                    if (clip != null && clip.name == "SkillA")
                    {
                        burrowClip = clip;
                        break;
                    }

            if (animator == null || burrowClip == null)
            {
                yield return new WaitForSeconds(0.5f);
                yield break;
            }

            animator.enabled = false;
            float elapsed = 0f;
            int frameGuard = 0;
            const float duration = 0.5f;
            while (elapsed < duration && frameGuard++ < 120)
            {
                elapsed += Mathf.Max(Time.unscaledDeltaTime, 1f / 60f);
                float t = Mathf.Clamp01(elapsed / duration);
                burrowClip.SampleAnimation(gameObject, Mathf.Lerp(burrowClip.length, 0f, t));
                _body.position = position;
                transform.position = position;
                Physics2D.SyncTransforms();
                yield return null;
            }
            animator.enabled = true;
            animator.Play("Idle", 0, 0f);
        }

        private IEnumerator FireShakeRoutine(float duration, float strength)
        {
            if (spriteRenderer == null) yield break;
            Transform visual = spriteRenderer.transform;
            Vector3 origin = _visualRestingPosition;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float remaining = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                visual.localPosition = origin + (Vector3)(Random.insideUnitCircle * strength * remaining);
                yield return null;
            }
            visual.localPosition = origin;
        }

        private void ResetAttackTimer()
        {
            float cooldown = Behavior != null && Behavior.specialAttackCooldown > 0f
                ? Behavior.specialAttackCooldown
                : _enemy != null && _enemy.Data != null ? _enemy.Data.attackCooldown : 0f;
            _attackTimer = cooldown;
        }

        private IEnumerator DashRoutine(float speedRatio, float duration)
        {
            if (_busy) yield break;
            _busy = true;
            yield return DashRoutineInternal(speedRatio, duration, "SkillA");
            _busy = false;
            ResetAttackTimer();
        }

        private IEnumerator DashRoutineInternal(float speedRatio, float duration, string state)
        {
            PlayState(state);
            AudioManager.Instance.PlaySfx(AudioCue.Dash);
            Vector2 direction = _player != null ? ((Vector2)_player.position - _body.position).normalized : Vector2.right;
            _moveDirection = direction;
            UpdateFacingAndAnimation();
            float speed = MovementSpeedSystem.EnemyToWorld(speedRatio);
            float end = Time.time + duration;
            while (Time.time < end)
            {
                _moveDirection = direction;
                Vector2 target = _body.position + direction * (speed * Time.deltaTime);
                if (MapBounds.Instance != null) target = MapBounds.Instance.ClampPosition(target);
                _body.MovePosition(target);
                yield return null;
            }
            _moveDirection = Vector2.zero;
        }

        private IEnumerator LittleSatanDashRoutine()
        {
            if (_busy) yield break;
            _busy = true;
            EnableAnimator();
            Vector2 direction = _player != null
                ? ((Vector2)_player.position - _body.position).normalized
                : Vector2.right;
            _moveDirection = direction;
            UpdateFacingAndAnimation();
            PlayState("PhaseTwoDashPrepare");
            yield return new WaitForSeconds(Mathf.Max(0f, Behavior.dashPreparationDuration));

            AudioManager.Instance.PlaySfx(AudioCue.Dash);
            PlayState("PhaseTwoDashLoop");
            float speed = MovementSpeedSystem.EnemyToWorld(GetDashSpeed());
            float end = Time.time + Mathf.Max(0f, Behavior.dashDuration);
            while (Time.time < end)
            {
                _moveDirection = direction;
                Vector2 target = _body.position + direction * (speed * Time.deltaTime);
                if (MapBounds.Instance != null) target = MapBounds.Instance.ClampPosition(target);
                _body.MovePosition(target);
                yield return null;
            }

            _moveDirection = Vector2.zero;
            PlayState("PhaseTwoDashEnd");
            yield return new WaitForSeconds(Mathf.Max(0f, Behavior.dashRecoveryDuration));
            HoldPhaseTwoIdleSprite();
            _busy = false;
            ResetAttackTimer();
        }

        private IEnumerator SpiderJumpRoutine()
        {
            if (_busy) yield break;
            _busy = true;
            PlayState("SkillA");
            Vector2 start = _body.position;
            Vector2 target = _player != null ? (Vector2)_player.position : start;
            if (MapBounds.Instance != null) target = MapBounds.Instance.ClampPosition(target);
            float duration = Mathf.Max(0.05f, Behavior.dashDuration);
            if (Behavior.jumpSpeed > 0f)
                duration = Mathf.Max(0.05f, Vector2.Distance(start, target) /
                    MovementSpeedSystem.EnemyToWorld(Behavior.jumpSpeed));
            float elapsed = 0f;
            GroundShadow shadow = GetComponent<GroundShadow>();
            shadow?.BeginTakeoff(duration * 0.5f);
            bool landingStarted = false;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                _body.MovePosition(Vector2.Lerp(start, target, Mathf.SmoothStep(0f, 1f, t)));
                SetVisualHeight(Mathf.Sin(t * Mathf.PI) * Behavior.jumpHeight);
                if (!landingStarted && t >= 0.5f)
                {
                    landingStarted = true;
                    shadow?.BeginLanding(duration * 0.5f);
                }
                yield return null;
            }
            _body.position = target;
            SetVisualHeight(0f);
            _busy = false;
            ResetAttackTimer();
        }

        private IEnumerator MoveVisualHeight(float from, float to, float duration, bool takingOff)
        {
            GroundShadow shadow = GetComponent<GroundShadow>();
            if (takingOff) shadow?.BeginTakeoff(duration);
            else shadow?.BeginLanding(duration);
            duration = Mathf.Max(0.01f, duration);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                SetVisualHeight(Mathf.LerpUnclamped(from, to, t));
                yield return null;
            }
            SetVisualHeight(to);
        }

        private void SetVisualHeight(float height)
        {
            if (spriteRenderer != null)
                spriteRenderer.transform.localPosition = _visualRestingPosition + Vector3.up * Mathf.Max(0f, height);
        }

        public bool TryHandleDamage(ref float damage)
        {
            if (damage <= 0f) return false;
            if (IsYellowVariant && WaveManager.Instance != null && WaveManager.Instance.HasOtherLivingEnemy(_enemy, true))
                return true;

            if ((Archetype == EnemyArchetype.Skeleton || Archetype == EnemyArchetype.SkeletonMan) &&
                !_skeletonHead && damage >= _enemy.CurrentHealth)
            {
                damage = 0f;
                StartCoroutine(SkeletonHeadRoutine());
                return true;
            }

            if (Archetype == EnemyArchetype.LittleSatan && !_phaseTwo && damage >= _enemy.CurrentHealth)
            {
                damage = 0f;
                StartCoroutine(LittleSatanTransformRoutine());
                return true;
            }

            _hitCount++;
            float appliedDamage = Mathf.Min(damage, _enemy.CurrentHealth);
            if (Behavior != null && Behavior.healthLossEffectInterval > 0f)
                _hitMassProgress += appliedDamage / Mathf.Max(1f, _enemy.MaxHealth);
            if (Archetype == EnemyArchetype.Satan) Summon(EnemyArchetype.Bat, 1);
            if (Archetype == EnemyArchetype.HomeSpider) Summon(EnemyArchetype.Spider, 1);
            if (Archetype == EnemyArchetype.BloodBag && Random.value < 0.05f) BloodDrop.Spawn(transform.position, false);
            if (Archetype == EnemyArchetype.Gloomy && (_hitCount & 1) == 0) StartCoroutine(GloomyDashRoutine());
            ProcessMassThresholdEffects();
            return false;
        }

        private void ProcessMassThresholdEffects()
        {
            if (Behavior == null || Behavior.healthLossEffectInterval <= 0f ||
                Behavior.healthLossEffectMaximumTriggers <= 0) return;
            if (_massGrowthRoutine != null)
            {
                StopCoroutine(_massGrowthRoutine);
                _massGrowthRoutine = null;
            }
            int crossedThresholds = 0;
            while (_hitMassProgress >= Behavior.healthLossEffectInterval &&
                   _healthLossEffectCount < Behavior.healthLossEffectMaximumTriggers)
            {
                _hitMassProgress -= Behavior.healthLossEffectInterval;
                _healthLossEffectCount++;
                crossedThresholds++;
                if (Behavior.healthLossEffectBulletCount > 0)
                    ShootRadial(Behavior.healthLossEffectBulletCount, 1f);
                if (Behavior.healthLossEffectSummonsMeatball)
                    Summon(EnemyArchetype.Meatballs, 1);
            }
            float progress = _healthLossEffectCount >= Behavior.healthLossEffectMaximumTriggers
                ? 0f
                : _hitMassProgress / Behavior.healthLossEffectInterval;
            if (crossedThresholds > 0)
                _massGrowthRoutine = StartCoroutine(MassThresholdPulseRoutine(crossedThresholds, progress));
            else
                SetMassGrowthVisual(progress);
        }

        private IEnumerator MassThresholdPulseRoutine(int pulseCount, float finalProgress)
        {
            float duration = Mathf.Max(0.01f, Behavior.healthLossEffectPulseDuration);
            for (int i = 0; i < pulseCount; i++)
            {
                SetMassGrowthVisual(1f);
                yield return new WaitForSeconds(duration);
                SetMassGrowthVisual(0f);
                if (i + 1 < pulseCount) yield return new WaitForSeconds(duration * 0.35f);
            }
            SetMassGrowthVisual(finalProgress);
            _massGrowthRoutine = null;
        }

        private void SetMassGrowthVisual(float progress)
        {
            if (spriteRenderer == null) return;
            float maximumScale = Behavior != null ? Mathf.Max(1f, Behavior.healthLossEffectMaximumScale) : 1f;
            float multiplier = Mathf.Lerp(1f, maximumScale, Mathf.Clamp01(progress));
            spriteRenderer.transform.localScale = _visualRestingScale * multiplier;
        }

        private IEnumerator SkeletonHeadRoutine()
        {
            _busy = true;
            _skeletonHead = true;
            ClearNegativeStatusEffects();
            _enemy.IsInvulnerable = true;
            EnableAnimator();
            PlayState("Special");
            yield return new WaitForSeconds(Mathf.Max(0f, Behavior.stateTransitionDuration));
            if (animator != null) animator.enabled = false;
            if (spriteRenderer != null && _enemy.Data.fakeDeathHoldSprite != null)
                spriteRenderer.sprite = _enemy.Data.fakeDeathHoldSprite;
            _enemy.ReplaceHealth(_enemy.MaxHealth * (Archetype == EnemyArchetype.SkeletonMan ? 1f / 3f : 0.5f),
                _enemy.MaxHealth);
            _enemy.IsInvulnerable = false;
            float end = Time.time + Mathf.Max(0f, Behavior.stateHoldDuration);
            while (Time.time < end && !_enemy.IsDead && _enemy.CurrentHealth > 0f) yield return null;
            if (_enemy.IsDead || _enemy.CurrentHealth <= 0f) yield break;
            _enemy.IsInvulnerable = true;
            EnableAnimator();
            PlayState("Revive");
            yield return new WaitForSeconds(Mathf.Max(0f, Behavior.stateTransitionDuration));
            _enemy.ReplaceHealth(_enemy.MaxHealth, _enemy.MaxHealth);
            _enemy.IsInvulnerable = false;
            _skeletonHead = false;
            _busy = false;
            PlayState("Idle");
        }

        private IEnumerator LittleSatanTransformRoutine()
        {
            _busy = true;
            ClearNegativeStatusEffects();
            _enemy.IsInvulnerable = true;
            EnableAnimator();
            PlayState("Special");
            yield return new WaitForSeconds(Mathf.Max(0f, Behavior.stateTransitionDuration));
            float phaseHealth = _enemy.MaxHealth * (100f / 120f);
            _enemy.ReplaceHealth(phaseHealth, phaseHealth);
            _phaseTwo = true;
            HoldPhaseTwoIdleSprite();
            _enemy.IsInvulnerable = false;
            _busy = false;
            ResetAttackTimer();
        }

        private void ClearNegativeStatusEffects()
        {
            GetComponent<EnemyStatusEffects>()?.ClearAll();
        }

        private void EnableAnimator()
        {
            if (animator != null) animator.enabled = true;
        }

        private void HoldPhaseTwoIdleSprite()
        {
            if (animator != null) animator.enabled = false;
            if (spriteRenderer != null && _enemy.Data.phaseTwoIdleSprite != null)
                spriteRenderer.sprite = _enemy.Data.phaseTwoIdleSprite;
        }

        private IEnumerator GloomyDashRoutine()
        {
            if (_busy) yield break;
            _busy = true;
            int count = Mathf.Clamp(_dashCount++, 3, 7);
            for (int i = 0; i < count; i++)
            {
                PlayState("GloomyDashPrepare");
                yield return new WaitForSeconds(Mathf.Max(0f, Behavior.dashPreparationDuration));
                Vector2 target = _player != null ? (Vector2)_player.position : _body.position + Vector2.right;
                if (MapBounds.Instance != null) target = MapBounds.Instance.ClampPosition(target);
                Vector2 direction = target - _body.position;
                _moveDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.zero;
                UpdateFacingAndAnimation();
                AudioManager.Instance.PlaySfx(AudioCue.Dash);
                PlayState("GloomyDashLoop");
                yield return MoveDashToTarget(target, GetDashSpeed());
                yield return new WaitForSeconds(Mathf.Max(0f, Behavior.dashRecoveryDuration));
            }
            PlayState("Idle");
            yield return new WaitForSeconds(5f);
            _busy = false;
        }

        private IEnumerator MoveDashToTarget(Vector2 target, float speedRatio)
        {
            float speed = Mathf.Max(0.01f, MovementSpeedSystem.EnemyToWorld(speedRatio));
            while ((_body.position - target).sqrMagnitude > 0.0025f)
            {
                Vector2 delta = target - _body.position;
                _moveDirection = delta.normalized;
                _body.MovePosition(Vector2.MoveTowards(_body.position, target, speed * Time.deltaTime));
                yield return null;
            }
            _body.position = target;
            _moveDirection = Vector2.zero;
        }

        private IEnumerator MoveDashInDirection(Vector2 direction, float speedRatio, float duration)
        {
            float speed = MovementSpeedSystem.EnemyToWorld(speedRatio);
            float end = Time.time + Mathf.Max(0f, duration);
            while (Time.time < end)
            {
                _moveDirection = direction;
                Vector2 target = _body.position + direction * (speed * Time.deltaTime);
                if (MapBounds.Instance != null) target = MapBounds.Instance.ClampPosition(target);
                _body.MovePosition(target);
                yield return null;
            }
            _moveDirection = Vector2.zero;
        }

        public bool TryReflectEnergyBall()
        {
            bool reflects = Archetype == EnemyArchetype.GreenBubble || HasVariant(WhiteEnemyVariant.Pink);
            if (!reflects || Random.value >= 0.5f) return false;
            PlayState("SkillA");
            AudioManager.Instance.PlaySfx(AudioCue.Rebound);
            if (_player != null)
                SpawnProjectile(transform.position, (Vector2)_player.position - _body.position,
                    GetAimedProjectileSpeed(), 2f, false);
            return true;
        }

        public void PlayDeathPresentation()
        {
            StopAllCoroutines();
            if (Archetype == EnemyArchetype.Baby)
                AudioManager.Existing?.StopIntervalLoop(AudioCue.BabyCry);
            _massGrowthRoutine = null;
            SetVisualHeight(0f);
            SetMassGrowthVisual(0f);
            GetComponent<GroundShadow>()?.BeginLanding(0.15f);
            StartCoroutine(DeathRoutine());
        }

        private IEnumerator DeathRoutine()
        {
            EnemyData data = _enemy.Data;
            EnemyDeathMode mode = data != null ? data.deathMode : EnemyDeathMode.DropChest;
            float animationDuration = data != null ? data.deathAnimationDuration : 1f;
            if (mode == EnemyDeathMode.StaticDeathSprite)
            {
                if (animator != null) animator.enabled = false;
                if (spriteRenderer != null)
                {
                    spriteRenderer.enabled = true;
                    if (data != null && data.deathSprite != null) spriteRenderer.sprite = data.deathSprite;
                }
            }
            if (mode == EnemyDeathMode.DeathAnimationKeepLastFrame || mode == EnemyDeathMode.DeathAnimationDropChest)
            {
                PlayState("Death");
                if (_collider != null && mode == EnemyDeathMode.DeathAnimationDropChest) _collider.enabled = false;
            }

            float effectDelay = data != null && data.deathEffect != null ? data.deathEffect.delay : 0f;
            if (effectDelay > 0f) yield return new WaitForSeconds(effectDelay);
            ApplyDeathSpecialEffects();

            bool noInhaleFaith = RogueSkillManager.Active != null &&
                (RogueSkillManager.Active.Has(RogueSkillId.FaithAngel) || RogueSkillManager.Active.Has(RogueSkillId.FaithDemon));

            float remainingAnimation = animationDuration - effectDelay;
            if ((mode == EnemyDeathMode.DeathAnimationKeepLastFrame || mode == EnemyDeathMode.DeathAnimationDropChest) &&
                remainingAnimation > 0f)
                yield return new WaitForSeconds(remainingAnimation);

            if (noInhaleFaith) { ReleaseOwner(); yield break; }

            if (mode == EnemyDeathMode.DropChest || mode == EnemyDeathMode.DeathAnimationDropChest)
            {
                EnemyRewardChest.Spawn(transform.position, _enemy.MassValue);
                ReleaseOwner();
                yield break;
            }

            if (animator != null) animator.enabled = false;
            if (spriteRenderer != null && data != null && data.deathSprite != null) spriteRenderer.sprite = data.deathSprite;
            if (_collider != null) _collider.enabled = true;
            _enemy.MakeCorpseInhaleable();
        }

        private void ApplyDeathSpecialEffects()
        {
            EnemyDeathEffectSettings settings = _enemy != null && _enemy.Data != null
                ? _enemy.Data.deathEffect : null;
            EnemyDeathEffect effect = settings != null ? settings.effect : EnemyDeathEffect.None;
            if (HasVariant(WhiteEnemyVariant.Red))
            {
                DamagePlayerInRadius(2f, 2, true);
                SpawnRedExplosionVfx();
                if (Archetype == EnemyArchetype.White) return;
            }

            switch (effect)
            {
                case EnemyDeathEffect.AreaExplosion:
                    DamagePlayerInRadius(settings.radius, settings.damage, settings.knockback);
                    break;
                case EnemyDeathEffect.SummonSpidersAndOvary:
                    Summon(EnemyArchetype.Spider, settings.summonCount);
                    if (Random.value < settings.secondaryChance) SpawnOvaryImmediate();
                    break;
                case EnemyDeathEffect.SplitWhiteVariants:
                    EnemyActor first = Summon(EnemyArchetype.White, 1);
                    if (first != null)
                    {
                        first.ForceVariant(_primaryVariant);
                        first.BeginSpawnProtection();
                    }
                    EnemyActor second = Summon(EnemyArchetype.White, 1);
                    if (second != null)
                    {
                        second.ForceVariant(_secondaryVariant);
                        second.BeginSpawnProtection();
                    }
                    break;
                case EnemyDeathEffect.DropFullHeart:
                    BloodDrop.Spawn(transform.position, true);
                    break;
            }
        }

        private void SpawnRedExplosionVfx()
        {
            GameObject effect = new("RedWhiteDeathExplosion", typeof(ParticleSystem));
            effect.transform.position = transform.position;
            ParticleSystem particles = effect.GetComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particles.main;
            main.loop = false;
            main.duration = 0.35f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.42f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 4.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.35f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.95f, 0.3f), new Color(1f, 0.08f, 0.02f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 28) });
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.12f;
            ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.material = EnemyFireHazard.SharedParticleMaterial;
            renderer.sortingOrder = 12;
            particles.Play();
            Destroy(effect, 0.6f);
        }

        private void SpawnMeatMountainLandingVfx()
        {
            GameObject effect = new("MeatMountainLandingSmoke", typeof(ParticleSystem));
            effect.transform.position = transform.position;
            ParticleSystem particles = effect.GetComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particles.main;
            main.loop = false;
            main.duration = 0.45f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.65f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 4.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.65f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.72f, 0.68f, 0.62f, 0.85f), new Color(0.35f, 0.32f, 0.3f, 0.55f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 36) });
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.35f;
            ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.material = EnemyFireHazard.SharedParticleMaterial;
            renderer.sortingOrder = 12;
            particles.Play();
            Destroy(effect, 0.8f);
        }

        private void ResizeColliderToVisual()
        {
            if (_collider is not CircleCollider2D circle || spriteRenderer == null ||
                spriteRenderer.sprite == null || _commonBalance == null) return;
            Vector2 visualSize = spriteRenderer.bounds.size;
            float rootScale = Mathf.Max(0.0001f,
                Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y)));
            float visualRadius = Mathf.Min(visualSize.x, visualSize.y) * 0.5f *
                _commonBalance.visualColliderRadiusScale / rootScale;
            circle.radius = Mathf.Max(_commonBalance.minimumColliderRadius, visualRadius);
        }

        private void TryContactDamage()
        {
            if (_contactTimer > 0f || _player == null || _playerHealth == null) return;
            Vector2 delta = (Vector2)_player.position - _body.position;
            bool touching = _collider != null && _collider.enabled && _playerCollider != null && _playerCollider.enabled
                ? _collider.Distance(_playerCollider).distance <= _commonBalance.colliderContactTolerance
                : delta.sqrMagnitude <= _commonBalance.fallbackContactRadius * _commonBalance.fallbackContactRadius;
            if (!touching) return;
            int damage = Mathf.Max(0, Mathf.RoundToInt(_enemy.Data != null ? _enemy.Data.attackDamage : 1f));
            bool hasContactDamage = Archetype != EnemyArchetype.Mushroom && damage > 0;
            if (hasContactDamage)
                _playerHealth.TakeDamageFrom(damage, _enemy.Data != null ? _enemy.Data.displayName : Archetype.ToString());
            if (HasVariant(WhiteEnemyVariant.Green))
                PlayerStatusEffects.Ensure(_playerHealth)?.ApplyPoison(1, 10f, 30f);
            if (hasContactDamage)
            {
                _playerInhale?.TryInterruptInhale();
                if (delta.sqrMagnitude <= 0.001f)
                    delta = new Vector2(Mathf.Cos(_steeringPhase), Mathf.Sin(_steeringPhase));
                _playerController?.ApplyKnockback(delta, _commonBalance.contactKnockbackDistance);
            }
            _contactTimer = _commonBalance.contactCooldown;
        }

        private void DamagePlayerInRadius(float radius, int damage, bool knockback)
        {
            if (_player == null || _playerHealth == null) return;
            Vector2 delta = (Vector2)_player.position - _body.position;
            if (delta.sqrMagnitude > radius * radius) return;
            _playerHealth.TakeDamage(damage);
            if (knockback) _playerController?.ApplyKnockback(delta, _commonBalance.areaKnockbackDistance);
        }

        private void DamagePlayerOnContact(int damage, bool knockback)
        {
            if (_playerHealth == null || _collider == null || !_collider.enabled ||
                _playerCollider == null || !_playerCollider.enabled) return;
            ColliderDistance2D contact = _collider.Distance(_playerCollider);
            if (!contact.isOverlapped && contact.distance > _commonBalance.colliderContactTolerance) return;
            _playerHealth.TakeDamage(damage);
            if (knockback)
            {
                Vector2 delta = (Vector2)_player.position - _body.position;
                _playerController?.ApplyKnockback(delta, _commonBalance.areaKnockbackDistance);
            }
        }

        private float GetDashSpeed() => Behavior != null && Behavior.dashSpeed > 0f
            ? Behavior.dashSpeed : Behavior != null ? Behavior.specialMoveSpeed : 0f;

        private float GetAimedProjectileSpeed() => _enemy != null && _enemy.Data != null &&
            _enemy.Data.aimedProjectileSpeed > 0f ? _enemy.Data.aimedProjectileSpeed :
            _commonBalance != null ? _commonBalance.aimedProjectileSpeed : 8f;

        private float GetRadialProjectileSpeed() => _enemy != null && _enemy.Data != null &&
            _enemy.Data.radialProjectileSpeed > 0f ? _enemy.Data.radialProjectileSpeed :
            _commonBalance != null ? _commonBalance.radialProjectileSpeed : 7f;

        private float GetFireballFallDuration() => Behavior != null && Behavior.fireballFallDuration > 0f
            ? Behavior.fireballFallDuration : _commonBalance != null ? _commonBalance.fireballFallDuration : 1.2f;

        private void ShootAtPlayer()
        {
            if (_player == null) return;
            SpawnProjectile(transform.position, (Vector2)_player.position - _body.position,
                GetAimedProjectileSpeed(), GetConfiguredAttackDamage(), false);
        }

        private void ShootRadial(int count, float damage)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = Mathf.PI * 2f * i / Mathf.Max(1, count);
                SpawnProjectile(transform.position, new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)),
                    GetRadialProjectileSpeed(), GetConfiguredAttackDamage() * damage, false);
            }
        }

        private float GetConfiguredAttackDamage() => _enemy != null && _enemy.Data != null
            ? Mathf.Max(0f, _enemy.Data.attackDamage) : 0f;

        private static void SpawnProjectile(Vector2 position, Vector2 direction, float speed, float damage, bool fireball)
        {
            string path = fireball ? "Enemy/EnemyFireball" : "Enemy/EnemyBullet";
            GameObject prefab = Resources.Load<GameObject>(path);
            if (prefab == null) return;
            EnemyProjectile projectile = Instantiate(prefab, position, Quaternion.identity).GetComponent<EnemyProjectile>();
            projectile.Initialize(direction, speed, damage, fireball);
        }

        private void SpawnFireball(Vector2 target, int radialBulletCount)
        {
            GameObject prefab = Resources.Load<GameObject>("Enemy/EnemyFireball");
            if (prefab == null) return;
            float height = _commonBalance != null ? _commonBalance.fireballFallHeight : 4f;
            float padding = _commonBalance != null ? _commonBalance.fireballOffscreenPadding : 1f;
            float startY = Mathf.Max(target.y + height, GetVisibleTopY() + padding);
            float damage = _commonBalance != null ? _commonBalance.fireballExplosionDamage : 2f;
            Vector2 start = new(target.x, startY);
            EnemyProjectile projectile = Instantiate(prefab, start,
                Quaternion.identity).GetComponent<EnemyProjectile>();
            projectile.InitializeFireball(target, start, damage, radialBulletCount, _commonBalance,
                GetFireballFallDuration(), GetRadialProjectileSpeed());
        }

        private static float GetVisibleTopY()
        {
            float top = MapBounds.Instance != null ? MapBounds.Instance.Max.y : 0f;
            Camera camera = Camera.main;
            if (camera != null)
                top = Mathf.Max(top, camera.ViewportToWorldPoint(new Vector3(0.5f, 1f,
                    Mathf.Abs(camera.transform.position.z))).y);
            return top;
        }

        private EnemyActor Summon(EnemyArchetype type, int count)
        {
            EnemyActor last = null;
            for (int i = 0; i < count; i++)
            {
                Vector2 offset = Random.insideUnitCircle.normalized * Random.Range(
                    _commonBalance.summonOffsetMinimum, _commonBalance.summonOffsetMaximum);
                EnemyBase spawned = WaveManager.Instance != null
                    ? WaveManager.Instance.SpawnSummoned(type, (Vector2)transform.position + offset) : null;
                last = spawned != null ? spawned.GetComponent<EnemyActor>() : last;
            }
            return last;
        }

        private IEnumerator SpawnOvaryRoutine()
        {
            _busy = true;
            EnableAnimator();
            PlayState("SkillA");
            yield return new WaitForSeconds(0.7f);
            SpawnOvaryImmediate();
            _busy = false;
        }

        private void SpawnOvaryImmediate()
        {
            GameObject prefab = Resources.Load<GameObject>("Enemy/SpiderOvary");
            if (prefab != null) Instantiate(prefab, transform.position, Quaternion.identity);
        }

        private void UpdateRedVariant()
        {
            if (!HasVariant(WhiteEnemyVariant.Red) || _redExplosionTriggered) return;
            if (spriteRenderer != null)
            {
                float pulse = Mathf.Repeat(Time.time * 8f, 1f);
                spriteRenderer.color = pulse < 0.5f ? Color.white : new Color(1f, 0.18f, 0.12f, 1f);
            }
            if (_player != null && !_enemy.IsInvulnerable &&
                (_player.position - transform.position).sqrMagnitude <= 1.15f * 1.15f)
            {
                _redExplosionTriggered = true;
                _enemy.TakeDamage(float.MaxValue);
            }
        }

        private void ConfigureWhiteVariants()
        {
            if (Archetype == EnemyArchetype.White)
                _primaryVariant = forcedWhiteVariant == WhiteEnemyVariant.White
                    ? (WhiteEnemyVariant)Random.Range(0, 7) : forcedWhiteVariant;
            else if (Archetype == EnemyArchetype.DoubleWhite)
            {
                _primaryVariant = (WhiteEnemyVariant)Random.Range(1, 7);
                do _secondaryVariant = (WhiteEnemyVariant)Random.Range(1, 7); while (_secondaryVariant == _primaryVariant);
            }
            ApplyVariantVisual();
        }

        public void ForceVariant(WhiteEnemyVariant variant)
        {
            _primaryVariant = variant;
            ApplyVariantVisual();
        }

        public void BeginSpawnProtection(float duration = 0.65f)
        {
            if (_enemy != null) StartCoroutine(SpawnProtectionRoutine(duration));
        }

        private IEnumerator SpawnProtectionRoutine(float duration)
        {
            _enemy.IsInvulnerable = true;
            yield return new WaitForSeconds(Mathf.Max(0.05f, duration));
            if (_enemy != null && !_enemy.IsDead) _enemy.IsInvulnerable = false;
        }

        private bool HasVariant(WhiteEnemyVariant variant) =>
            (Archetype == EnemyArchetype.White && _primaryVariant == variant) ||
            (Archetype == EnemyArchetype.DoubleWhite && (_primaryVariant == variant || _secondaryVariant == variant));

        private void ApplyVariantVisual()
        {
            if (spriteRenderer == null || (Archetype != EnemyArchetype.White && Archetype != EnemyArchetype.DoubleWhite)) return;
            Color color = VariantColor(_primaryVariant);
            if (Archetype == EnemyArchetype.DoubleWhite) color = Color.Lerp(color, VariantColor(_secondaryVariant), 0.5f);
            MaterialPropertyBlock block = new();
            spriteRenderer.GetPropertyBlock(block);
            block.SetColor("_ShadowColor", Color.Lerp(Color.black, color, 0.35f));
            block.SetColor("_HighlightColor", Color.Lerp(Color.white, color, 0.75f));
            spriteRenderer.SetPropertyBlock(block);
        }

        private static Color VariantColor(WhiteEnemyVariant variant) => variant switch
        {
            WhiteEnemyVariant.Red => new Color(1f, 0.15f, 0.12f),
            WhiteEnemyVariant.Purple => new Color(0.65f, 0.2f, 0.9f),
            WhiteEnemyVariant.Yellow => new Color(1f, 0.85f, 0.12f),
            WhiteEnemyVariant.Green => new Color(0.15f, 0.9f, 0.3f),
            WhiteEnemyVariant.Blue => new Color(0.15f, 0.55f, 1f),
            WhiteEnemyVariant.Pink => new Color(1f, 0.35f, 0.75f),
            _ => Color.white
        };

        private void UpdateBlueInvisibility()
        {
            if (!HasVariant(WhiteEnemyVariant.Blue) || spriteRenderer == null) return;
            spriteRenderer.enabled = Mathf.Repeat(Time.time, 5f) < 3f;
        }

        private void UpdateFacingAndAnimation()
        {
            float facingX = Archetype == EnemyArchetype.Bat && _player != null
                ? _player.position.x - _body.position.x
                : _moveDirection.x;
            if (spriteRenderer != null &&
                Mathf.Abs(facingX) >= _commonBalance.horizontalFacingDeadZone)
            {
                bool phaseTwoFacesRight = Archetype == EnemyArchetype.LittleSatan && _phaseTwo;
                spriteRenderer.flipX = phaseTwoFacesRight
                    ? facingX < 0f
                    : facingX > 0f;
            }
            if (animator != null && animator.enabled) animator.SetBool("IsMoving", _moveDirection.sqrMagnitude > 0.01f);
        }

        private void PlayState(string state)
        {
            if (animator != null && animator.enabled) animator.Play(state, 0, 0f);
        }

        private void ReleaseOwner()
        {
            EnemyPoolMember member = GetComponent<EnemyPoolMember>();
            if (member != null) member.Release(); else Destroy(gameObject);
        }
    }
}
