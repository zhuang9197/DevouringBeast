using UnityEngine;

namespace DevouringBeast
{
    [DisallowMultipleComponent]
    public sealed class EnemyFireHazard : MonoBehaviour
    {
        private static Material _sharedParticleMaterial;
        private static Sprite _burnSprite;

        private PlayerHealth _playerHealth;
        private Transform _player;
        private SpriteRenderer _burnRenderer;
        private float _radius;
        private float _remaining;
        private float _damageTimer;
        private int _damage;
        private float _visualScale;
        private readonly Collider2D[] _hits = new Collider2D[8];

        public static Material SharedParticleMaterial
        {
            get
            {
                if (_sharedParticleMaterial != null) return _sharedParticleMaterial;
                Shader shader = Shader.Find("Sprites/Default");
                _sharedParticleMaterial = new Material(shader)
                {
                    name = "EnemyFireParticles",
                    hideFlags = HideFlags.HideAndDontSave,
                    mainTexture = CreateSoftTexture()
                };
                return _sharedParticleMaterial;
            }
        }

        public static void Spawn(Vector2 position, float radius, int damage, float duration, float visualScale)
        {
            GameObject owner = new("EnemyGroundFire");
            owner.transform.position = position;
            Rigidbody2D body = owner.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            CircleCollider2D trigger = owner.AddComponent<CircleCollider2D>();
            trigger.isTrigger = true;
            owner.AddComponent<EnemyFireHazard>().Initialize(radius, damage, duration, visualScale, trigger);
        }

        private void Initialize(float radius, int damage, float duration, float visualScale, CircleCollider2D trigger)
        {
            _radius = Mathf.Max(0.1f, radius);
            _damage = Mathf.Max(0, damage);
            _remaining = Mathf.Max(0.1f, duration);
            _visualScale = Mathf.Max(0.1f, visualScale);
            if (trigger != null) trigger.radius = _radius;
            FindPlayer();
            CreateBurnVisual();
            CreateParticles();
        }

        private void Update()
        {
            _remaining -= Time.deltaTime;
            if (_remaining <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            if (_burnRenderer != null)
            {
                Color color = _burnRenderer.color;
                color.a = (0.5f + Mathf.PingPong(Time.time * 0.6f, 0.16f)) * Mathf.Clamp01(_remaining * 2f);
                _burnRenderer.color = color;
            }

            _damageTimer -= Time.deltaTime;
            if (_damageTimer > 0f || _damage <= 0) return;
            DamageOverlappingPlayer();
        }

        private void OnTriggerEnter2D(Collider2D other) => TryDamage(other);

        private void OnTriggerStay2D(Collider2D other) => TryDamage(other);

        private void DamageOverlappingPlayer()
        {
            int count = Physics2D.OverlapCircleNonAlloc(transform.position, _radius, _hits);
            for (int i = 0; i < count; i++)
                if (TryDamage(_hits[i])) return;
        }

        private bool TryDamage(Collider2D other)
        {
            if (_damageTimer > 0f || other == null) return false;
            PlayerHealth health = other.GetComponentInParent<PlayerHealth>();
            if (health == null) return false;
            _playerHealth = health;
            _player = health.transform;
            health.TakeDamage(_damage);
            _damageTimer = 0.25f;
            return true;
        }

        private void FindPlayer()
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            _player = playerObject != null ? playerObject.transform : null;
            _playerHealth = playerObject != null ? playerObject.GetComponent<PlayerHealth>() : null;
        }

        private void CreateBurnVisual()
        {
            GameObject visual = new("ScorchedGround", typeof(SpriteRenderer));
            visual.transform.SetParent(transform, false);
            _burnRenderer = visual.GetComponent<SpriteRenderer>();
            _burnRenderer.sprite = GetBurnSprite();
            _burnRenderer.color = new Color(0.4f, 0.025f, 0.005f, 0.62f);
            _burnRenderer.sortingOrder = 3;
            visual.transform.localScale = Vector3.one * (_radius * 2f * _visualScale);
        }

        private void CreateParticles()
        {
            ParticleSystem particles = gameObject.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particles.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 220;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.15f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.22f * _visualScale, 0.62f * _visualScale);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.12f, 0.45f);
            main.startColor = new ParticleSystem.MinMaxGradient(Color.yellow, new Color(1f, 0.08f, 0f));
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 72f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 52) });
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = _radius * 0.92f * _visualScale;
            ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.y = new ParticleSystem.MinMaxCurve(0.8f, 2.4f);
            ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.2f), new Keyframe(0.2f, 1f), new Keyframe(1f, 0.05f)));
            ParticleSystem.ColorOverLifetimeModule color = particles.colorOverLifetime;
            color.enabled = true;
            Gradient gradient = new();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.yellow, 0f), new GradientColorKey(new Color(1f, 0.08f, 0f), 1f) },
                new[] { new GradientAlphaKey(0.95f, 0f), new GradientAlphaKey(0f, 1f) });
            color.color = new ParticleSystem.MinMaxGradient(gradient);
            ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.material = SharedParticleMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.1f;
            renderer.lengthScale = 4f;
            renderer.sortingOrder = 6;
            particles.Play();
        }

        private static Texture2D CreateSoftTexture()
        {
            const int size = 32;
            Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
            {
                name = "EnemyFireSoftParticle",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f;
                float dy = (y + 0.5f) / size * 2f - 1f;
                float alpha = Mathf.Pow(Mathf.Clamp01(1f - dx * dx - dy * dy), 1.6f);
                pixels[x + y * size] = new Color(1f, 1f, 1f, alpha);
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static Sprite GetBurnSprite()
        {
            if (_burnSprite != null) return _burnSprite;
            Texture2D texture = CreateSoftTexture();
            _burnSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), texture.width);
            _burnSprite.name = "EnemyScorchedGround";
            _burnSprite.hideFlags = HideFlags.HideAndDontSave;
            return _burnSprite;
        }
    }
}
