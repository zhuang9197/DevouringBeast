using UnityEngine;

namespace DevouringBeast
{
    /// <summary>
    /// 波次配置 ScriptableObject — 可调节的难度参数
    /// </summary>
    [CreateAssetMenu(menuName = "DevouringBeast/Wave Config", fileName = "WaveConfig")]
    public class WaveConfig : ScriptableObject
    {
        [Header("倒计时")]
        [Tooltip("普通波次倒计时（秒）")]
        public float normalWaveTimer = 60f;
        [Tooltip("Boss波次倒计时（秒）")]
        public float bossWaveTimer = 120f;
        [Tooltip("清敌后重置倒计时（秒）")]
        public float clearResetTimer = 3f;

        [Header("生成数量")]
        [Tooltip("第1波基础敌人数量")]
        public int baseEnemyCount = 5;
        [Tooltip("每波递增数量")]
        public int enemiesPerWaveIncrement = 2;
        [Tooltip("每5波额外生成精英怪数量")]
        public int elitePer5Waves = 1;
        [Tooltip("每10波额外生成Boss数量")]
        public int bossPer10Waves = 1;

        [Header("难度递增系数")]
        [Tooltip("普通怪每波血量递增 (1.05 = 每波+5%)")]
        public float normalHealthScale = 1.05f;
        [Tooltip("普通怪每波移速递增")]
        public float normalSpeedScale = 1.01f;

        [Header("伤害成长（整数点数）")]
        [Min(1)] public int baseAttackDamage = 1;
        [Tooltip("每经过多少波，基础伤害提高 1 点")]
        [Min(1)] public int damageIncreaseInterval = 10;
        [Min(0)] public int eliteDamageBonus = 1;
        [Min(0)] public int bossDamageBonus = 2;

        [Header("跨波存活敌人强化（生命值不变）")]
        [Min(1f)] public float survivorAttackRangeScale = 1.01f;
        [Min(1f)] public float survivorDetectRangeScale = 1.02f;
        [Min(1f)] public float survivorAttackSpeedScale = 1.02f;
        [Min(1f)] public float survivorInhaleResistanceScale = 1.03f;

        [Header("精英怪系数")]
        public float eliteHealthMul = 3f;
        public float eliteDamageMul = 2f;
        public float eliteSpeedMul = 1.2f;

        [Header("Boss系数")]
        public float bossHealthMul = 8f;
        public float bossDamageMul = 3f;
        public float bossSpeedMul = 0.8f;

        [Header("等级封顶")]
        [Tooltip("达到此等级后预制体不再变化")]
        public int maxTier = 7;

        /// <summary>
        /// 根据波次计算怪物等级 (1~7)
        /// wave 1~10 -> tier 1, 11~20 -> tier 2, ..., 61~70 -> tier 7, 71+ 封顶 7
        /// </summary>
        public int GetTier(int wave)
        {
            int tier = Mathf.CeilToInt(wave / 10f);
            return Mathf.Clamp(tier, 1, maxTier);
        }

        /// <summary>
        /// 计算某波次的敌人数量
        /// </summary>
        public int GetEnemyCount(int wave)
        {
            return baseEnemyCount + (wave - 1) * enemiesPerWaveIncrement;
        }

        /// <summary>
        /// 获取波次倒计时
        /// </summary>
        public float GetWaveTimer(int wave)
        {
            if (wave % 10 == 0) return bossWaveTimer;
            return normalWaveTimer;
        }

        /// <summary>
        /// 计算波次的血量系数
        /// </summary>
        public float GetHealthMultiplier(int wave)
        {
            return Mathf.Pow(normalHealthScale, wave - 1);
        }

        public int GetAttackDamage(int wave, EnemyType type)
        {
            int damage = Mathf.Max(1, baseAttackDamage) +
                Mathf.Max(0, wave - 1) / Mathf.Max(1, damageIncreaseInterval);
            if (type == EnemyType.Elite) damage += Mathf.Max(0, eliteDamageBonus);
            else if (type == EnemyType.Boss) damage += Mathf.Max(0, bossDamageBonus);
            return damage;
        }

        public float GetSpeedMultiplier(int wave)
        {
            return Mathf.Min(Mathf.Pow(normalSpeedScale, wave - 1), 2f); // 封顶2倍
        }
    }
}
