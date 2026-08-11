using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DevouringBeast
{
    /// <summary>Small pooled particle burst used before minions are revealed.</summary>
    public sealed class EnemySpawnSmokeEffect : MonoBehaviour
    {
        private static readonly Queue<EnemySpawnSmokeEffect> Pool = new();
        private static Material _material;
        private static Texture2D _texture;
        private ParticleSystem _particles;
        private Coroutine _releaseRoutine;

        public static void Play(Vector3 position)
        {
            EnemySpawnSmokeEffect effect = null;
            while (Pool.Count > 0 && effect == null) effect = Pool.Dequeue();
            if (effect == null)
            {
                GameObject go = new("EnemySpawnSmoke");
                effect = go.AddComponent<EnemySpawnSmokeEffect>();
            }
            effect.transform.position = position;
            effect.gameObject.SetActive(true);
            effect._particles.Clear();
            effect.EmitBurst();
            effect._releaseRoutine = effect.StartCoroutine(effect.ReleaseRoutine());
        }

        private void Awake()
        {
            _particles = gameObject.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = _particles.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 20;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.42f, 0.7f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.22f, 0.52f);
            main.startColor = new Color(0.45f, 0.46f, 0.43f, 0.84f);

            ParticleSystem.SizeOverLifetimeModule size = _particles.sizeOverLifetime;
            size.enabled = true;
            AnimationCurve sizeCurve = new(
                new Keyframe(0f, 0.35f), new Keyframe(0.22f, 1f), new Keyframe(1f, 0.65f));
            size.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            ParticleSystem.ColorOverLifetimeModule color = _particles.colorOverLifetime;
            color.enabled = true;
            Gradient gradient = new();
            gradient.SetKeys(
                new[] { new GradientColorKey(new Color(0.48f, 0.49f, 0.46f), 0f),
                    new GradientColorKey(new Color(0.28f, 0.29f, 0.27f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.85f, 0.12f),
                    new GradientAlphaKey(0.7f, 0.55f), new GradientAlphaKey(0f, 1f) });
            color.color = new ParticleSystem.MinMaxGradient(gradient);

            ParticleSystemRenderer renderer = _particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.material = GetMaterial();
            renderer.sortingOrder = 12;
        }

        private void EmitBurst()
        {
            ParticleSystem.EmitParams emit = new();
            for (int i = 0; i < 5; i++)
            {
                float t = i / 4f;
                emit.position = new Vector3(Mathf.Lerp(-0.22f, 0.22f, t), -0.3f + t * 0.38f, 0f);
                emit.velocity = new Vector3(Random.Range(-0.12f, 0.12f), Random.Range(0.55f, 1.05f), 0f);
                emit.startSize = Random.Range(0.22f, 0.38f);
                _particles.Emit(emit, 1);
            }
            for (int i = 0; i < 9; i++)
            {
                float angle = Mathf.Lerp(0f, Mathf.PI, i / 8f);
                emit.position = new Vector3(Mathf.Cos(angle) * Random.Range(0.18f, 0.5f),
                    0.05f + Mathf.Sin(angle) * Random.Range(0.05f, 0.28f), 0f);
                emit.velocity = new Vector3(Mathf.Cos(angle) * Random.Range(0.25f, 0.65f),
                    Random.Range(0.2f, 0.7f), 0f);
                emit.startSize = Random.Range(0.3f, 0.56f);
                _particles.Emit(emit, 1);
            }
            _particles.Play();
        }

        private IEnumerator ReleaseRoutine()
        {
            yield return new WaitForSeconds(0.8f);
            _particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            gameObject.SetActive(false);
            Pool.Enqueue(this);
            _releaseRoutine = null;
        }

        private static Material GetMaterial()
        {
            if (_material != null) return _material;
            Shader shader = Shader.Find("Sprites/Default");
            _material = new Material(shader) { name = "EnemySpawnSmokeMaterial", hideFlags = HideFlags.HideAndDontSave };
            _texture = CreateSmokeTexture();
            _material.mainTexture = _texture;
            return _material;
        }

        private static Texture2D CreateSmokeTexture()
        {
            const int size = 32;
            Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
            {
                name = "EnemySpawnSmokeTexture",
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
                    float alpha = Mathf.Pow(Mathf.Clamp01(1f - dx * dx - dy * dy), 0.65f);
                    pixels[x + y * size] = new Color(1f, 1f, 1f, alpha);
                }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}
