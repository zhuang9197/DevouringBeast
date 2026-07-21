using System;
using System.Collections.Generic;
using UnityEngine;

namespace DevouringBeast
{
    /// <summary>
    /// 能量球弹体：使用发射快照飞行，在命中时结算伤害、状态、分裂与穿透。
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public sealed class EnergyBall : MonoBehaviour
    {
        [Header("视觉效果")]
        [SerializeField] private ParticleSystem trailParticles;
        [SerializeField] private GameObject hitVfxPrefab;
        [SerializeField, Min(0.05f)] private float splitSpawnPadding = 0.35f;

        private readonly HashSet<int> _hitEnemies = new HashSet<int>();
        private readonly HashSet<int> _explosionEnemies = new HashSet<int>();
        private readonly RaycastHit2D[] _castHits = new RaycastHit2D[16];
        private readonly Collider2D[] _overlapHits = new Collider2D[32];
        private Rigidbody2D _rigidbody;
        private CircleCollider2D _circleCollider;
        private EnergyBallShotSnapshot _snapshot;
        private Vector2 _direction;
        private Vector2 _spawnPosition;
        private Transform _owner;
        private Action<EnergyBall> _releaseAction;
        private Action<Vector3, Vector2, EnergyBallShotSnapshot, int, int> _splitSpawnAction;
        private int _remainingPierces;
        private int _piercedEnemies;
        private int _splitGeneration;
        private bool _hasSplit;
        private bool _initialized;
        private bool _released;

        public EnergyBallShotSnapshot Snapshot => _snapshot;
        public float Damage => _snapshot != null ? _snapshot.Damage : 0f;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody2D>();
            _rigidbody.bodyType = RigidbodyType2D.Kinematic;
            _rigidbody.gravityScale = 0f;
            _rigidbody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            _rigidbody.interpolation = RigidbodyInterpolation2D.Interpolate;
            _rigidbody.useFullKinematicContacts = true;
            _circleCollider = GetComponent<CircleCollider2D>();

            if (trailParticles == null)
                trailParticles = GetComponentInChildren<ParticleSystem>(true);
        }

        /// <summary>
        /// 每次从对象池取出时都必须调用，以刷新玩家属性和肉鸽技能快照。
        /// </summary>
        public void Initialize(
            Vector2 direction,
            EnergyBallShotSnapshot snapshot,
            Transform owner,
            Action<EnergyBall> releaseAction,
            Action<Vector3, Vector2, EnergyBallShotSnapshot, int, int> splitSpawnAction,
            int splitGeneration = 0,
            int ignoredEnemyId = 0)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));

            _snapshot = snapshot;
            _direction = direction.sqrMagnitude > 0f ? direction.normalized : Vector2.right;
            // 显式同步池对象的 Transform 与 Rigidbody2D，避免沿用上次回收位置。
            _rigidbody.position = transform.position;
            _spawnPosition = transform.position;
            _owner = owner;
            _releaseAction = releaseAction;
            _splitSpawnAction = splitSpawnAction;
            _remainingPierces = snapshot.PierceCount;
            _piercedEnemies = 0;
            _splitGeneration = Mathf.Max(0, splitGeneration);
            _hasSplit = false;
            _initialized = true;
            _released = false;
            _hitEnemies.Clear();
            if (ignoredEnemyId != 0)
                _hitEnemies.Add(ignoredEnemyId);

            float angle = Mathf.Atan2(_direction.y, _direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);

            if (trailParticles != null)
            {
                trailParticles.Clear(true);
                trailParticles.Play(true);
            }
        }

        /// <summary>兼容不使用对象池的旧调用。</summary>
        public void Initialize(Vector2 direction, float speed, float maxDistance, float damage)
        {
            Initialize(
                direction,
                new EnergyBallShotSnapshot(damage, speed, maxDistance, null),
                null,
                null,
                null);
        }

        private void FixedUpdate()
        {
            if (!_initialized || _released || _snapshot == null)
                return;

            Vector2 currentPosition = _rigidbody.position;
            float travelDistance = _snapshot.Speed * Time.fixedDeltaTime;
            Vector2 nextPosition = currentPosition + _direction * travelDistance;

            SweepForEnemies(currentPosition, travelDistance);
            if (_released)
                return;

            _rigidbody.MovePosition(nextPosition);

            float maxDistanceSquared = _snapshot.MaxDistance * _snapshot.MaxDistance;
            if ((_rigidbody.position - _spawnPosition).sqrMagnitude >= maxDistanceSquared)
                Release(false);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!_initialized || _released || other == null)
                return;

            if (_owner != null && other.transform.IsChildOf(_owner))
                return;

            TryHitEnemy(other.GetComponentInParent<EnemyBase>());
        }

        private void SweepForEnemies(Vector2 origin, float distance)
        {
            if (distance <= 0f)
                return;

            float radius = _circleCollider != null
                ? _circleCollider.radius * Mathf.Max(
                    Mathf.Abs(transform.lossyScale.x),
                    Mathf.Abs(transform.lossyScale.y))
                : 0.35f;

            int hitCount = Physics2D.CircleCastNonAlloc(
                origin,
                radius,
                _direction,
                _castHits,
                distance,
                ~0);

            for (int i = 0; i < hitCount && !_released; i++)
            {
                Collider2D collider = _castHits[i].collider;
                if (collider == null || (_owner != null && collider.transform.IsChildOf(_owner)))
                    continue;

                TryHitEnemy(collider.GetComponentInParent<EnemyBase>());
            }
        }

        private void TryHitEnemy(EnemyBase enemy)
        {
            if (enemy == null || enemy.IsDead || _released || _snapshot == null)
                return;

            int enemyId = enemy.GetInstanceID();
            
            AudioManager.Instance.PlaySfx(
                _snapshot.HasExplosion ? AudioCue.Bomb : AudioCue.Hit);

if (!_hitEnemies.Add(enemyId))
                return;

            float pierceMultiplier = Mathf.Max(0.4f, 1f - _snapshot.PierceDamageLoss * _piercedEnemies);
            float hitDamage = _snapshot.Damage * _snapshot.PrimaryHitMultiplier * pierceMultiplier;
            EnemyStatusEffects status = EnemyStatusEffects.EnsureFor(enemy);
            if (_snapshot.HasErosion)
            {
                hitDamage += status.ApplyErosion(
                    _snapshot.Damage,
                    _snapshot.ErosionMaxStacks,
                    _snapshot.ErosionDamageMultiplier,
                    _snapshot.ErosionMissingHealthPercent);
            }
            enemy.TakeDamage(hitDamage);
            ApplyStatusEffects(enemy);
            ApplyExplosion(enemy);
            SpawnSplitProjectiles(enemy);

            if (_remainingPierces > 0)
            {
                _remainingPierces--;
                _piercedEnemies++;
                return;
            }

            Release(true);
        }

        private void ApplyExplosion(EnemyBase primaryTarget)
        {
            if (!_snapshot.HasExplosion)
                return;

            _explosionEnemies.Clear();
            _explosionEnemies.Add(primaryTarget.GetInstanceID());

            int count = Physics2D.OverlapCircleNonAlloc(
                primaryTarget.transform.position,
                _snapshot.ExplosionRadius,
                _overlapHits,
                ~0);
            float explosionDamage = _snapshot.Damage * _snapshot.ExplosionDamageMultiplier;

            for (int i = 0; i < count; i++)
            {
                Collider2D collider = _overlapHits[i];
                if (collider == null)
                    continue;

                EnemyBase enemy = collider.GetComponentInParent<EnemyBase>();
                if (enemy == null || enemy.IsDead || !_explosionEnemies.Add(enemy.GetInstanceID()))
                    continue;

                enemy.TakeDamage(explosionDamage);
                ApplyStatusEffects(enemy);
            }
        }

        private void ApplyStatusEffects(EnemyBase enemy)
        {
            if (enemy.IsDead) return;
            EnemyStatusEffects status = EnemyStatusEffects.EnsureFor(enemy);
            if (_snapshot.HasPoison) status.ApplyPoison(_snapshot.PoisonDamagePerSecond, _snapshot.PoisonDuration);
            if (_snapshot.HasBurn) status.ApplyBurn(_snapshot.BurnDamagePerSecond, _snapshot.BurnDuration, _snapshot.BurnGrowthPerHit);
            if (_snapshot.HasSlow) status.ApplySlow(_snapshot.SlowPercent, _snapshot.SlowDuration);
            if (_snapshot.HasStun && UnityEngine.Random.value <= _snapshot.StunChance)
                status.ApplyStun(_snapshot.StunDuration);
        }

        private void SpawnSplitProjectiles(EnemyBase sourceEnemy)
        {
            if (_hasSplit || !_snapshot.HasSplit || _splitSpawnAction == null || _splitGeneration > 0)
            {
                return;
            }

            _hasSplit = true;
            int count = _snapshot.SplitProjectileCount;
            const float totalArc = 70f;
            float step = count > 1 ? totalArc / (count - 1) : 0f;
            float startAngle = -totalArc * 0.5f;
            Vector3 splitOrigin = transform.position + (Vector3)(_direction * splitSpawnPadding);

            Collider2D[] sourceColliders = sourceEnemy.GetComponentsInChildren<Collider2D>();
            if (sourceColliders.Length > 0)
            {
                Bounds bounds = sourceColliders[0].bounds;
                for (int colliderIndex = 1; colliderIndex < sourceColliders.Length; colliderIndex++)
                    bounds.Encapsulate(sourceColliders[colliderIndex].bounds);

                float projectedExtent = Mathf.Abs(_direction.x) * bounds.extents.x +
                    Mathf.Abs(_direction.y) * bounds.extents.y;
                splitOrigin = bounds.center + (Vector3)(_direction * (projectedExtent + splitSpawnPadding));
            }

            for (int i = 0; i < count; i++)
            {
                float angle = startAngle + step * i;
                Vector2 splitDirection = Quaternion.Euler(0f, 0f, angle) * _direction;
                Vector3 spawnPosition = splitOrigin + (Vector3)(splitDirection * 0.05f);
                _splitSpawnAction(
                    spawnPosition,
                    splitDirection,
                    _snapshot.CreateSplitSnapshot(),
                    _splitGeneration + 1,
                    sourceEnemy.GetInstanceID());
            }
        }

        private void Release(bool spawnHitVfx)
        {
            if (_released)
                return;

            _released = true;
            _initialized = false;

            if (spawnHitVfx && hitVfxPrefab != null)
                Instantiate(hitVfxPrefab, transform.position, Quaternion.identity);

            if (trailParticles != null)
                trailParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            Action<EnergyBall> release = _releaseAction;
            _releaseAction = null;
            _splitSpawnAction = null;
            _owner = null;
            _snapshot = null;

            if (release != null)
                release(this);
            else
                Destroy(gameObject);
        }

        private void OnDisable()
        {
            _initialized = false;
            _hitEnemies.Clear();
        }
    }
}
