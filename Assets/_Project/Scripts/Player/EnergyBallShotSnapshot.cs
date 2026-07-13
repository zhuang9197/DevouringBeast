using System;
using System.Collections.Generic;

namespace DevouringBeast
{
    /// <summary>
    /// 发射瞬间生成的只读数据快照。弹体飞行期间不再读取玩家的实时属性。
    /// </summary>
    public sealed class EnergyBallShotSnapshot
    {
        [Serializable]
        public readonly struct SkillEntry
        {
            public SkillEntry(string name, ItemTag tag, int level, float value)
            {
                Name = name ?? string.Empty;
                Tag = tag;
                Level = level;
                Value = value;
            }

            public string Name { get; }
            public ItemTag Tag { get; }
            public int Level { get; }
            public float Value { get; }
        }

        private readonly SkillEntry[] _skills;

        public EnergyBallShotSnapshot(
            float damage,
            float speed,
            float maxDistance,
            IEnumerable<RogueSkillData> ownedSkills)
        {
            Damage = Math.Max(0f, damage);
            Speed = Math.Max(0f, speed);
            MaxDistance = Math.Max(0.01f, maxDistance);

            List<SkillEntry> entries = new List<SkillEntry>();
            if (ownedSkills != null)
            {
                foreach (RogueSkillData skill in ownedSkills)
                {
                    if (skill == null || skill.currentLevel <= 0)
                        continue;

                    entries.Add(new SkillEntry(
                        skill.skillName,
                        skill.tag,
                        skill.currentLevel,
                        skill.CurrentValue));
                }
            }

            _skills = entries.ToArray();
            CacheProjectileEffects();
        }

        public float Damage { get; }
        public float Speed { get; }
        public float MaxDistance { get; }
        public IReadOnlyList<SkillEntry> Skills => _skills;

        public int PierceCount { get; private set; }
        public int SplitProjectileCount { get; private set; }
        public int MaxSplitGenerations { get; private set; }
        public float PoisonDamagePerSecond { get; private set; }
        public float PoisonDuration { get; private set; }
        public float ExplosionRadius { get; private set; }
        public float ExplosionDamageMultiplier { get; private set; }

        public bool HasPoison => PoisonDamagePerSecond > 0f && PoisonDuration > 0f;
        public bool HasSplit => SplitProjectileCount > 1 && MaxSplitGenerations > 0;
        public bool HasExplosion => ExplosionRadius > 0f && ExplosionDamageMultiplier > 0f;

        public bool TryGetSkill(string nameFragment, out SkillEntry entry)
        {
            if (!string.IsNullOrWhiteSpace(nameFragment))
            {
                for (int i = 0; i < _skills.Length; i++)
                {
                    if (_skills[i].Name.IndexOf(nameFragment, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        entry = _skills[i];
                        return true;
                    }
                }
            }

            entry = default;
            return false;
        }

        private void CacheProjectileEffects()
        {
            if (TryGetEither("穿透", "pierce", out SkillEntry pierce))
                PierceCount = ResolveCount(pierce, 1, 8);

            if (TryGetEither("分裂", "split", out SkillEntry split))
            {
                SplitProjectileCount = ResolveCount(split, 2, 6);
                SplitProjectileCount = Math.Max(2, SplitProjectileCount);
                MaxSplitGenerations = Math.Min(2, Math.Max(1, split.Level));
            }

            if (TryGetEither("中毒", "poison", out SkillEntry poison))
            {
                float damageRatio = poison.Value > 0f && poison.Value <= 1f
                    ? poison.Value
                    : 0.15f + 0.05f * Math.Max(0, poison.Level - 1);

                PoisonDamagePerSecond = Math.Max(0.01f, Damage * damageRatio);
                PoisonDuration = 2f + 0.5f * Math.Max(1, poison.Level);
            }

            if (TryGetEither("爆炸", "explosion", out SkillEntry explosion))
            {
                ExplosionRadius = Math.Min(3.5f, 1.5f + 0.25f * Math.Max(1, explosion.Level));
                ExplosionDamageMultiplier = explosion.Value > 0f && explosion.Value <= 2f
                    ? explosion.Value
                    : 0.5f + 0.1f * Math.Max(0, explosion.Level - 1);
            }
        }

        private bool TryGetEither(string first, string second, out SkillEntry entry)
        {
            return TryGetSkill(first, out entry) || TryGetSkill(second, out entry);
        }

        private static int ResolveCount(SkillEntry entry, int fallback, int maximum)
        {
            int valueCount = entry.Value >= 1f ? (int)Math.Round(entry.Value) : 0;
            int resolved = valueCount > 0 ? valueCount : Math.Max(fallback, entry.Level);
            return Math.Min(maximum, Math.Max(0, resolved));
        }
    }
}
