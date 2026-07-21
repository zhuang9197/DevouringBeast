using System;
using System.Collections.Generic;

namespace DevouringBeast
{
    public sealed class EnergyBallShotSnapshot
    {
        private readonly Dictionary<RogueSkillId, int> _levels;

        public EnergyBallShotSnapshot(float damage, float speed, float maxDistance,
            IReadOnlyDictionary<RogueSkillId, int> levels, bool isSplitProjectile = false)
        {
            Damage = Math.Max(0f, damage);
            Speed = Math.Max(0f, speed);
            MaxDistance = Math.Max(0.01f, maxDistance);
            IsSplitProjectile = isSplitProjectile;
            _levels = levels != null ? new Dictionary<RogueSkillId, int>(levels) : new Dictionary<RogueSkillId, int>();
            CacheEffects();
        }

        public float Damage { get; }
        public float Speed { get; }
        public float MaxDistance { get; }
        public bool IsSplitProjectile { get; }
        public int PierceCount { get; private set; }
        public float PierceDamageLoss { get; private set; }
        public int SplitProjectileCount { get; private set; }
        public float SplitDamageMultiplier { get; private set; }
        public bool SplitCarriesPoison { get; private set; }
        public bool SplitCarriesFire { get; private set; }
        public float PoisonDamagePerSecond { get; private set; }
        public float PoisonDuration { get; private set; }
        public float StunChance { get; private set; }
        public float StunDuration { get; private set; }
        public float SlowPercent { get; private set; }
        public float SlowDuration { get; private set; }
        public int ErosionMaxStacks { get; private set; }
        public float ErosionDamageMultiplier { get; private set; }
        public float ErosionMissingHealthPercent { get; private set; }
        public float ExplosionRadius { get; private set; }
        public float PrimaryHitMultiplier { get; private set; }
        public float ExplosionDamageMultiplier { get; private set; }
        public float BurnDamagePerSecond { get; private set; }
        public float BurnDuration { get; private set; }
        public float BurnGrowthPerHit { get; private set; }

        public bool HasPoison => PoisonDamagePerSecond > 0f && (!IsSplitProjectile || SplitCarriesPoison);
        public bool HasStun => StunChance > 0f && (!IsSplitProjectile || SplitCarriesPoison);
        public bool HasSlow => SlowPercent > 0f && (!IsSplitProjectile || SplitCarriesPoison);
        public bool HasErosion => ErosionMaxStacks > 0 && (!IsSplitProjectile || SplitCarriesPoison);
        public bool HasExplosion => ExplosionRadius > 0f && (!IsSplitProjectile || SplitCarriesFire);
        public bool HasBurn => BurnDamagePerSecond > 0f && (!IsSplitProjectile || SplitCarriesFire);
        public bool HasSplit => !IsSplitProjectile && SplitProjectileCount > 0;

        public int GetLevel(RogueSkillId id) => _levels.TryGetValue(id, out int level) ? level : 0;

        public EnergyBallShotSnapshot CreateSplitSnapshot()
        {
            return new EnergyBallShotSnapshot(Damage * SplitDamageMultiplier, Speed, MaxDistance, _levels, true);
        }

        private void CacheEffects()
        {
            int piece = GetLevel(RogueSkillId.SuperPiece);
            if (piece > 0)
            {
                PierceCount = 64;
                PierceDamageLoss = Math.Max(0.1f, 0.2f - 0.05f * Math.Min(2, piece - 1));
            }

            int split = GetLevel(RogueSkillId.SuperSplit);
            if (split > 0)
            {
                SplitProjectileCount = 2 + GetLevel(RogueSkillId.SuperSplitMore);
                SplitDamageMultiplier = 0.3f;
            }
            SplitCarriesPoison = GetLevel(RogueSkillId.PoisonLegacy) > 0;
            SplitCarriesFire = GetLevel(RogueSkillId.FireLegacy) > 0;

            int poison = GetLevel(RogueSkillId.PoisonDeadly);
            if (poison > 0) { PoisonDamagePerSecond = 4f + poison; PoisonDuration = 5f; }
            int numb = GetLevel(RogueSkillId.PoisonNumb);
            if (numb > 0) { StunChance = 0.2f + numb * 0.1f; StunDuration = 1f; }
            int warp = GetLevel(RogueSkillId.PoisonWarp);
            if (warp > 0) { SlowPercent = 0.05f + warp * 0.05f; SlowDuration = 5f; }
            int erosion = GetLevel(RogueSkillId.PoisonErode);
            if (erosion > 0)
            {
                ErosionMaxStacks = 3;
                ErosionDamageMultiplier = 3f;
                ErosionMissingHealthPercent = 0.03f + erosion * 0.02f;
            }

            if (GetLevel(RogueSkillId.FirePyroblast) > 0)
            {
                PrimaryHitMultiplier = 1.2f;
                ExplosionDamageMultiplier = 0.3f + GetLevel(RogueSkillId.FirePyroblastFlame) * 0.1f;
                ExplosionRadius = 1.5f * (1f + GetLevel(RogueSkillId.FirePyroblastScope) * 0.1f);
            }
            else PrimaryHitMultiplier = 1f;

            int bottle = GetLevel(RogueSkillId.FireBottle);
            if (bottle > 0)
            {
                BurnDamagePerSecond = 4f + bottle;
                BurnDuration = 15f + bottle * 5f;
                BurnGrowthPerHit = 0.1f;
            }
        }
    }
}
