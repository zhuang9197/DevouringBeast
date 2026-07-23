using System.Collections.Generic;
using UnityEngine;

namespace DevouringBeast
{
    public enum EnergyBallHitVfxKind
    {
        NormalParticles,
        FireParticles,
        PoisonParticles,
        FirePoisonParticles,
        FireExplosion,
        PoisonCloud,
        PoisonExplosion
    }

    /// <summary>
    /// Routes energy-ball hit visuals and reuses effect objects to avoid Instantiate/Destroy spikes.
    /// </summary>
    public sealed class EnergyBallHitVfxService : MonoBehaviour
    {
        private const string CatalogResourcePath = "System/EnergyBallHitVfxCatalog";
        private const int InitialPoolSize = 12;
        private const int MaximumPoolSize = 48;

        private static EnergyBallHitVfxService _instance;
        private readonly Stack<EnergyBallHitVfxInstance> _available = new();
        private readonly HashSet<EnergyBallHitVfxInstance> _active = new();
        private EnergyBallHitVfxCatalog _catalog;
        private Transform _poolRoot;
        private int _createdCount;

        public static EnergyBallHitVfxKind ResolveKind(EnergyBallShotSnapshot snapshot)
        {
            if (snapshot.HasExplosion && snapshot.HasAnyPoisonSkill)
                return EnergyBallHitVfxKind.PoisonExplosion;
            if (snapshot.HasExplosion)
                return EnergyBallHitVfxKind.FireExplosion;
            if (snapshot.HasDeadlyPoison)
                return EnergyBallHitVfxKind.PoisonCloud;
            if (snapshot.HasNonExplosionFire && snapshot.HasAnyPoisonSkill)
                return EnergyBallHitVfxKind.FirePoisonParticles;
            if (snapshot.HasNonExplosionFire)
                return EnergyBallHitVfxKind.FireParticles;
            if (snapshot.HasAnyPoisonSkill)
                return EnergyBallHitVfxKind.PoisonParticles;
            return EnergyBallHitVfxKind.NormalParticles;
        }

        public static void Play(Vector3 position, EnergyBallShotSnapshot snapshot)
        {
            if (snapshot == null)
                return;

            EnsureInstance();
            if (_instance == null || _instance._catalog == null)
                return;

            _instance.PlayInternal(position, ResolveKind(snapshot));
        }

        public static void WarmUp() => EnsureInstance();

        private static void EnsureInstance()
        {
            if (_instance != null)
                return;

            _instance = FindObjectOfType<EnergyBallHitVfxService>();
            if (_instance != null)
                return;

            GameObject root = new("[EnergyBallHitVfx]");
            _instance = root.AddComponent<EnergyBallHitVfxService>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            _catalog = Resources.Load<EnergyBallHitVfxCatalog>(CatalogResourcePath);
            if (_catalog == null)
            {
                Debug.LogError($"[EnergyBallHitVfx] Missing Resources/{CatalogResourcePath}.asset");
                enabled = false;
                return;
            }

            GameObject poolObject = new("Pool");
            poolObject.transform.SetParent(transform, false);
            _poolRoot = poolObject.transform;
            for (int i = 0; i < InitialPoolSize; i++)
                _available.Push(CreateInstance());
        }

        private void PlayInternal(Vector3 position, EnergyBallHitVfxKind kind)
        {
            EnergyBallHitVfxInstance effect = _available.Count > 0
                ? _available.Pop()
                : CreateInstance();
            _active.Add(effect);
            effect.Play(position, kind, _catalog, Release);
        }

        private EnergyBallHitVfxInstance CreateInstance()
        {
            GameObject effectObject = new($"HitVfx_{_createdCount:00}");
            effectObject.transform.SetParent(_poolRoot != null ? _poolRoot : transform, false);
            EnergyBallHitVfxInstance effect = effectObject.AddComponent<EnergyBallHitVfxInstance>();
            effect.Build();
            effectObject.SetActive(false);
            _createdCount++;
            return effect;
        }

        private void Release(EnergyBallHitVfxInstance effect)
        {
            if (effect == null || !_active.Remove(effect))
                return;

            effect.gameObject.SetActive(false);
            effect.transform.SetParent(_poolRoot, false);
            if (_available.Count < MaximumPoolSize)
                _available.Push(effect);
            else
                Destroy(effect.gameObject);
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }
    }
}
