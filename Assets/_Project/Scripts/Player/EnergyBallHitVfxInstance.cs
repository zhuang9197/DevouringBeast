using System;
using UnityEngine;

namespace DevouringBeast
{
    [DisallowMultipleComponent]
    public sealed class EnergyBallHitVfxInstance : MonoBehaviour
    {
        private static readonly Gradient NormalImpactGradient = CreateImpactGradient(Color.white);
        private static readonly Gradient FireImpactGradient =
            CreateImpactGradient(new Color(1f, 0.45f, 0.08f, 1f));
        private static readonly Gradient PoisonImpactGradient =
            CreateImpactGradient(new Color(0.3f, 0.95f, 0.22f, 1f));
        private static readonly Gradient CachedPoisonCloudGradient = CreatePoisonCloudGradient(0);

        private ParticleSystem _primaryParticles;
        private ParticleSystem _secondaryParticles;
        private ParticleSystem _poisonCloudParticles;
        private SpriteRenderer _animationRenderer;
        private Sprite[] _animationFrames;
        private Action<EnergyBallHitVfxInstance> _release;
        private float _duration;
        private float _elapsed;
        private float _animationFps;
        private bool _playing;

        public void Build()
        {
            _primaryParticles = CreateParticleSystem("PrimaryParticles", 32);
            _secondaryParticles = CreateParticleSystem("SecondaryParticles", 24);
            _poisonCloudParticles = CreateParticleSystem("PoisonCloud", 8);
            _primaryParticles.transform.SetParent(transform, false);
            _secondaryParticles.transform.SetParent(transform, false);
            _poisonCloudParticles.transform.SetParent(transform, false);

            GameObject animationObject = new("FrameAnimation", typeof(SpriteRenderer));
            animationObject.transform.SetParent(transform, false);
            _animationRenderer = animationObject.GetComponent<SpriteRenderer>();
            _animationRenderer.sortingOrder = 31;
            _animationRenderer.enabled = false;
        }

        public void Play(Vector3 position, EnergyBallHitVfxKind kind,
            EnergyBallHitVfxCatalog catalog, Action<EnergyBallHitVfxInstance> release)
        {
            transform.position = position;
            gameObject.SetActive(true);
            _release = release;
            _elapsed = 0f;
            _playing = true;
            ResetVisuals();

            switch (kind)
            {
                case EnergyBallHitVfxKind.FireExplosion:
                    PlayAnimation(catalog.fireExplosionFrames, catalog.explosionFramesPerSecond,
                        catalog.fireExplosionScale);
                    break;
                case EnergyBallHitVfxKind.PoisonExplosion:
                    PlayAnimation(catalog.poisonExplosionFrames, catalog.explosionFramesPerSecond,
                        catalog.poisonExplosionScale);
                    break;
                case EnergyBallHitVfxKind.PoisonCloud:
                    PlayPoisonCloud(catalog);
                    break;
                case EnergyBallHitVfxKind.FirePoisonParticles:
                    PlayImpactParticles(_primaryParticles, catalog.fireParticle, catalog.particleMaterial,
                        new Color(1f, 0.45f, 0.08f, 1f), 13, FireImpactGradient);
                    PlayImpactParticles(_secondaryParticles, catalog.poisonParticle, catalog.particleMaterial,
                        new Color(0.3f, 0.95f, 0.22f, 1f), 11, PoisonImpactGradient);
                    _duration = 0.8f;
                    break;
                case EnergyBallHitVfxKind.FireParticles:
                    PlayImpactParticles(_primaryParticles, catalog.fireParticle, catalog.particleMaterial,
                        new Color(1f, 0.42f, 0.06f, 1f), 18, FireImpactGradient);
                    _duration = 0.8f;
                    break;
                case EnergyBallHitVfxKind.PoisonParticles:
                    PlayImpactParticles(_primaryParticles, catalog.poisonParticle, catalog.particleMaterial,
                        new Color(0.28f, 0.95f, 0.2f, 1f), 18, PoisonImpactGradient);
                    _duration = 0.8f;
                    break;
                default:
                    PlayImpactParticles(_primaryParticles, catalog.normalParticle, catalog.particleMaterial,
                        Color.white, 18, NormalImpactGradient);
                    _duration = 0.8f;
                    break;
            }
        }

        private void Update()
        {
            if (!_playing)
                return;

            _elapsed += Time.deltaTime;
            if (_animationRenderer.enabled && _animationFrames != null && _animationFrames.Length > 0)
            {
                int frame = Mathf.Min(_animationFrames.Length - 1,
                    Mathf.FloorToInt(_elapsed * _animationFps));
                _animationRenderer.sprite = _animationFrames[frame];
            }

            if (_elapsed < _duration)
                return;

            _playing = false;
            Action<EnergyBallHitVfxInstance> release = _release;
            _release = null;
            release?.Invoke(this);
        }

        private void ResetVisuals()
        {
            StopAndClear(_primaryParticles);
            StopAndClear(_secondaryParticles);
            StopAndClear(_poisonCloudParticles);
            _animationRenderer.enabled = false;
            _animationRenderer.sprite = null;
            _animationFrames = null;
        }

        private void PlayAnimation(Sprite[] frames, float framesPerSecond, float scale)
        {
            if (frames == null || frames.Length == 0)
            {
                _duration = 0.05f;
                return;
            }

            _animationFrames = frames;
            _animationFps = Mathf.Max(1f, framesPerSecond);
            _duration = frames.Length / _animationFps;
            _animationRenderer.transform.localScale = Vector3.one * scale;
            _animationRenderer.sprite = frames[0];
            _animationRenderer.enabled = true;
        }

        private void PlayImpactParticles(ParticleSystem particles, Sprite sprite, Material material,
            Color tint, int count, Gradient lifetimeGradient)
        {
            ParticleSystem.MainModule main = particles.main;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.65f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.8f, 4.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.55f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = tint;
            main.maxParticles = 32;

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.12f;
            shape.arc = 360f;

            ParticleSystem.ColorOverLifetimeModule color = particles.colorOverLifetime;
            color.enabled = true;
            color.color = lifetimeGradient;

            ConfigureParticleSprite(particles, sprite, material);
            particles.Emit(count);
        }

        private void PlayPoisonCloud(EnergyBallHitVfxCatalog catalog)
        {
            ParticleSystem particles = _poisonCloudParticles;
            ParticleSystem.MainModule main = particles.main;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(
                catalog.poisonCloudLifetime * 0.85f, catalog.poisonCloudLifetime * 1.15f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.08f, 0.32f);
            main.startSize = new ParticleSystem.MinMaxCurve(
                catalog.poisonCloudStartSize * 0.8f, catalog.poisonCloudStartSize * 1.2f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            // Keep the start color neutral. The lifetime gradient owns the poison hue;
            // multiplying two dark greens made the cloud read as grey in-game.
            main.startColor = new Color(1f, 1f, 1f, 0.88f);
            main.maxParticles = 8;

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.45f;

            ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
            size.enabled = true;
            size.separateAxes = false;
            float growth = catalog.poisonCloudEndSize / Mathf.Max(0.01f, catalog.poisonCloudStartSize);
            size.size = new ParticleSystem.MinMaxCurve(1f,
                AnimationCurve.EaseInOut(0f, 1f, 1f, growth));

            ParticleSystem.ColorOverLifetimeModule color = particles.colorOverLifetime;
            color.enabled = true;
            color.color = CachedPoisonCloudGradient;

            ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingOrder = 29;
            renderer.sharedMaterial = catalog.poisonCloudMaterial;

            particles.Emit(UnityEngine.Random.Range(3, 6));
            _duration = catalog.poisonCloudLifetime * 1.2f;
        }

        private static ParticleSystem CreateParticleSystem(string name, int maxParticles)
        {
            GameObject particleObject = new(name, typeof(ParticleSystem));
            ParticleSystem particles = particleObject.GetComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particles.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = maxParticles;
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = false;
            return particles;
        }

        private void ConfigureParticleSprite(ParticleSystem particles, Sprite sprite, Material material)
        {
            ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingOrder = 30;
            renderer.sharedMaterial = material;

            ParticleSystem.TextureSheetAnimationModule sheet = particles.textureSheetAnimation;
            while (sheet.spriteCount > 0)
                sheet.RemoveSprite(0);
            sheet.enabled = sprite != null;
            sheet.mode = ParticleSystemAnimationMode.Sprites;
            if (sprite != null)
                sheet.AddSprite(sprite);
        }

        private static void StopAndClear(ParticleSystem particles)
        {
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particles.Clear(true);
        }

        private static Gradient CreateImpactGradient(Color tint)
        {
            Gradient gradient = new();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(tint, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            return gradient;
        }

        private static Gradient CreatePoisonCloudGradient(int _)
        {
            Gradient gradient = new();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.04f, 0.22f, 0.05f), 0f),
                    new GradientColorKey(new Color(0.2f, 0.48f, 0.12f), 0.65f),
                    new GradientColorKey(new Color(0.08f, 0.18f, 0.06f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0.78f, 0f),
                    new GradientAlphaKey(0.5f, 0.55f),
                    new GradientAlphaKey(0f, 1f)
                });
            return gradient;
        }
    }
}
