using UnityEngine;

namespace DevouringBeast
{
    [DisallowMultipleComponent]
    public sealed class InhaleAirEffect : MonoBehaviour
    {
        private PlayerInhale _inhale;
        private PlayerController _controller;
        private ParticleSystem _particles;
        private ParticleSystem.MainModule _main;
        private Material _particleMaterial;
        private float _emissionAccumulator;
        private const float ParticlesPerSecond = 42f;
        private const float InwardSpeed = 12f;

        private void Awake()
        {
            _inhale = GetComponent<PlayerInhale>();
            _controller = GetComponent<PlayerController>();
            GameObject child = new("InhaleAirParticles");
            child.transform.SetParent(transform, false);
            _particles = child.AddComponent<ParticleSystem>();
            _main = _particles.main;
            _main.loop = false;
            _main.playOnAwake = false;
            _main.simulationSpace = ParticleSystemSimulationSpace.World;
            _main.startSpeed = 0f;
            _main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.14f);
            _main.maxParticles = 96;
            ParticleSystem.EmissionModule emission = _particles.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = _particles.shape;
            shape.enabled = false;
            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = _particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient alphaGradient = new();
            alphaGradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.9f, 0.12f),
                    new GradientAlphaKey(0.75f, 0.72f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = alphaGradient;
            ParticleSystemRenderer renderer = _particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            Shader particleShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ?? Shader.Find("Sprites/Default");
            if (particleShader != null)
            {
                _particleMaterial = new Material(particleShader);
                renderer.material = _particleMaterial;
                if (_particleMaterial.HasProperty("_Color")) _particleMaterial.SetColor("_Color", Color.white);
                if (_particleMaterial.HasProperty("_BaseColor")) _particleMaterial.SetColor("_BaseColor", Color.white);
            }
            renderer.velocityScale = 0.16f;
            renderer.lengthScale = 0.8f;
            renderer.sortingOrder = 95;
        }

        private void Update()
        {
            if (_inhale == null || !_inhale.IsInhaling)
            {
                if (_particles.isPlaying) _particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                _emissionAccumulator = 0f;
                return;
            }

            Vector2 facing = _controller != null ? _controller.FacingDirection : Vector2.down;
            Vector3 mouth = transform.position + (Vector3)(facing * 0.42f);
            float radius = Mathf.Max(0.8f, _inhale.CurrentInhaleRadius);
            float halfAngle = Mathf.Min(175f, _inhale.CurrentInhaleAngle * 0.5f);
            Color particleColor = RogueSkillManager.Active != null && RogueSkillManager.Active.Has(RogueSkillId.FaithDemon)
                ? new Color(0.035f, 0.03f, 0.045f, 0.92f)
                : new Color(1f, 0.96f, 0.82f, 0.92f);

            _emissionAccumulator += Time.deltaTime * ParticlesPerSecond;
            int emitCount = Mathf.Min(6, Mathf.FloorToInt(_emissionAccumulator));
            _emissionAccumulator -= emitCount;
            for (int i = 0; i < emitCount; i++)
            {
                float angle = Random.Range(-halfAngle, halfAngle);
                Vector2 outward = Quaternion.Euler(0f, 0f, angle) * facing;
                float distance = Random.Range(radius * 0.55f, radius);
                Vector3 start = mouth + (Vector3)(outward * distance);
                float speed = Random.Range(InwardSpeed * 0.85f, InwardSpeed * 1.15f);
                ParticleSystem.EmitParams emit = new()
                {
                    position = start,
                    velocity = ((Vector2)mouth - (Vector2)start).normalized * speed,
                    startLifetime = distance / speed,
                    startSize = Random.Range(0.07f, 0.14f),
                    startColor = particleColor
                };
                _particles.Emit(emit, 1);
            }
        }

        private void OnDestroy()
        {
            if (_particleMaterial != null) Destroy(_particleMaterial);
        }
    }
}
