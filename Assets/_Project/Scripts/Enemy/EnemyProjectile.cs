using UnityEngine;

namespace DevouringBeast
{
    [DisallowMultipleComponent]
    public sealed class EnemyProjectile : MonoBehaviour
    {
        [SerializeField] private bool fireball;

        private readonly Collider2D[] _hits = new Collider2D[8];
        private Vector2 _direction;
        private Vector2 _landingTarget;
        private float _speed;
        private float _damage;
        private float _lifetime;
        private float _radius;
        private float _fallDuration;
        private float _fallElapsed;
        private float _fallHeight;
        private float _orbitRadius;
        private float _orbitTurns;
        private float _burnRadius;
        private float _burnDuration;
        private int _burnDamage;
        private float _burnVisualScale;
        private float _particleScale;
        private float _landingMarkerScale;
        private float _impactBulletSpeed;
        private int _radialBulletCount;
        private bool _landed;
        private SpriteRenderer _spriteRenderer;
        private Collider2D _collider;
        private ParticleSystem _trail;
        private GameObject _landingMarker;

        public void Initialize(Vector2 direction, float speed, float damage, bool isFireball, float lifetime = 5f)
        {
            _direction = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.right;
            _speed = speed;
            _damage = damage;
            fireball = isFireball;
            _lifetime = lifetime;
            _radius = isFireball ? 0.55f : 0.25f;
            _landed = false;
            CacheComponents();
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(_direction.y, _direction.x) * Mathf.Rad2Deg);
            gameObject.SetActive(true);
        }

        public void InitializeFireball(Vector2 target, Vector2 start, float damage, int radialBulletCount,
            EnemyCommonBalanceSettings balance, float fallDurationOverride = -1f, float impactBulletSpeed = -1f)
        {
            CacheComponents();
            fireball = true;
            _landed = false;
            _damage = damage;
            _landingTarget = target;
            _fallElapsed = 0f;
            _fallHeight = Mathf.Max(0.1f, start.y - target.y);
            _fallDuration = fallDurationOverride > 0f ? fallDurationOverride :
                balance != null ? Mathf.Max(0.1f, balance.fireballFallDuration) : 1.2f;
            _impactBulletSpeed = impactBulletSpeed > 0f ? impactBulletSpeed :
                balance != null ? balance.radialProjectileSpeed : 7f;
            _orbitRadius = balance != null ? balance.fireballOrbitRadius : 0.75f;
            _orbitTurns = balance != null ? balance.fireballOrbitTurns : 1.5f;
            _radius = balance != null ? balance.fireballExplosionRadius : 1.25f;
            _burnRadius = balance != null ? balance.fireballBurnRadius : 0.9f;
            _burnDuration = balance != null ? balance.fireballBurnDuration : 2f;
            _burnDamage = balance != null ? balance.fireballBurnDamage : 1;
            _burnVisualScale = balance != null ? Mathf.Max(0.1f, balance.fireballBurnVisualScale) : 1f;
            _particleScale = balance != null ? Mathf.Max(0.1f, balance.fireballParticleScale) : 1f;
            _landingMarkerScale = balance != null ? Mathf.Max(0.1f, balance.fireballLandingMarkerScale) : 1f;
            _radialBulletCount = Mathf.Max(0, radialBulletCount);
            transform.position = start;
            transform.rotation = Quaternion.identity;
            float visualScale = balance != null ? Mathf.Max(0.1f, balance.fireballVisualScale) : 1f;
            transform.localScale = Vector3.one * visualScale;
            CreateLandingMarker();
            CreateFireTrail();
            gameObject.SetActive(true);
        }

        private void CacheComponents()
        {
            if (_spriteRenderer == null) _spriteRenderer = GetComponent<SpriteRenderer>();
            if (_collider == null) _collider = GetComponent<Collider2D>();
            if (_spriteRenderer != null) _spriteRenderer.enabled = true;
            if (_collider != null) _collider.enabled = true;
        }

        private void Update()
        {
            if (_landed) return;
            if (fireball)
            {
                UpdateFireball();
                return;
            }

            transform.position += (Vector3)(_direction * (_speed * Time.deltaTime));
            _lifetime -= Time.deltaTime;
            if (_lifetime <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            Collider2D hit = Physics2D.OverlapCircle(transform.position, _radius);
            PlayerHealth health = hit != null ? hit.GetComponentInParent<PlayerHealth>() : null;
            if (health == null) return;
            health.TakeDamage(Mathf.Max(1, Mathf.RoundToInt(_damage)));
            Destroy(gameObject);
        }

        private void UpdateFireball()
        {
            _fallElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_fallElapsed / _fallDuration);
            float eased = Mathf.SmoothStep(0f, 1f, t);
            float angle = t * _orbitTurns * Mathf.PI * 2f;
            float orbitScale = 1f - eased;
            Vector2 orbit = new(Mathf.Cos(angle) * _orbitRadius * orbitScale,
                Mathf.Sin(angle) * _orbitRadius * 0.42f * orbitScale);
            transform.position = _landingTarget + Vector2.up * (_fallHeight * (1f - eased)) + orbit;
            transform.Rotate(0f, 0f, 420f * Time.deltaTime);
            if (t >= 1f) LandFireball();
        }

        private void LandFireball()
        {
            if (_landed) return;
            _landed = true;
            transform.position = _landingTarget;
            if (_spriteRenderer != null) _spriteRenderer.enabled = false;
            if (_collider != null) _collider.enabled = false;
            if (_trail != null) _trail.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (_landingMarker != null) Destroy(_landingMarker);

            int hitCount = Physics2D.OverlapCircleNonAlloc(_landingTarget, _radius, _hits);
            for (int i = 0; i < hitCount; i++)
            {
                PlayerHealth health = _hits[i] != null ? _hits[i].GetComponentInParent<PlayerHealth>() : null;
                if (health != null) health.TakeDamage(Mathf.Max(1, Mathf.RoundToInt(_damage)));
            }

            for (int i = 0; i < _radialBulletCount; i++)
            {
                float angle = Mathf.PI * 2f * i / _radialBulletCount;
                SpawnImpactBullet(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)));
            }
            EnemyFireHazard.Spawn(_landingTarget, _burnRadius, _burnDamage, _burnDuration, _burnVisualScale);
            Destroy(gameObject, 0.6f);
        }

        private void SpawnImpactBullet(Vector2 direction)
        {
            GameObject prefab = Resources.Load<GameObject>("Enemy/EnemyBullet");
            if (prefab == null) return;
            EnemyProjectile projectile = Instantiate(prefab, _landingTarget, Quaternion.identity)
                .GetComponent<EnemyProjectile>();
            projectile.Initialize(direction, _impactBulletSpeed, Mathf.Max(1f, _damage * 0.5f), false);
        }

        private void CreateLandingMarker()
        {
            _landingMarker = new GameObject("FireballLandingShadow");
            _landingMarker.transform.position = _landingTarget;
            _landingMarker.transform.localScale = Vector3.one * _landingMarkerScale;
            GroundShadow shadow = GroundShadow.Ensure(_landingMarker);
            shadow.BeginLanding(_fallDuration);
        }

        private void CreateFireTrail()
        {
            if (_trail == null) _trail = gameObject.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = _trail.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 160;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.28f, 0.62f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.16f * _particleScale, 0.42f * _particleScale);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 1.15f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.92f, 0.2f, 0.95f), new Color(1f, 0.18f, 0.02f, 0.9f));
            ParticleSystem.EmissionModule emission = _trail.emission;
            emission.rateOverTime = 85f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 18) });
            ParticleSystem.ShapeModule shape = _trail.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.3f * _particleScale;
            ParticleSystem.VelocityOverLifetimeModule velocity = _trail.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.y = new ParticleSystem.MinMaxCurve(0.5f, 1.6f);
            ParticleSystem.SizeOverLifetimeModule size = _trail.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.35f), new Keyframe(0.25f, 1f), new Keyframe(1f, 0.08f)));
            ParticleSystem.ColorOverLifetimeModule color = _trail.colorOverLifetime;
            color.enabled = true;
            Gradient gradient = new();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.yellow, 0f), new GradientColorKey(new Color(1f, 0.12f, 0f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            color.color = new ParticleSystem.MinMaxGradient(gradient);
            ParticleSystemRenderer renderer = _trail.GetComponent<ParticleSystemRenderer>();
            renderer.material = EnemyFireHazard.SharedParticleMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.12f;
            renderer.lengthScale = 3.6f;
            renderer.sortingOrder = 8;
            _trail.Play();
        }

        private void OnDestroy()
        {
            if (_landingMarker != null) Destroy(_landingMarker);
        }
    }
}
