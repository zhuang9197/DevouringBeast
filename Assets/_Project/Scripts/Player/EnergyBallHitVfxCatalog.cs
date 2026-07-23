using System;
using UnityEngine;

namespace DevouringBeast
{
    [CreateAssetMenu(menuName = "DevouringBeast/Energy Ball Hit VFX Catalog",
        fileName = "EnergyBallHitVfxCatalog")]
    public sealed class EnergyBallHitVfxCatalog : ScriptableObject
    {
        [Header("Particle hit sprites")]
        public Sprite normalParticle;
        public Sprite fireParticle;
        public Sprite poisonParticle;
        public Material particleMaterial;

        [Header("Frame animations")]
        public Sprite[] fireExplosionFrames = Array.Empty<Sprite>();
        public Sprite[] poisonExplosionFrames = Array.Empty<Sprite>();
        [Min(1f)] public float explosionFramesPerSecond = 18f;
        [Min(0.1f)] public float fireExplosionScale = 3.2f;
        [Min(0.1f)] public float poisonExplosionScale = 3.4f;

        [Header("Deadly poison cloud")]
        public Sprite poisonCloud;
        public Material poisonCloudMaterial;
        [Min(0.1f)] public float poisonCloudLifetime = 1.8f;
        [Min(0.1f)] public float poisonCloudStartSize = 1.2f;
        [Min(0.1f)] public float poisonCloudEndSize = 3.4f;
    }
}
