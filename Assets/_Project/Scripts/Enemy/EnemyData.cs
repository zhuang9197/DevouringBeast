using UnityEngine;

namespace DevouringBeast
{
    /// <summary>敌人类型</summary>
    public enum EnemyType
    {
        Normal,
        Elite,
        Boss
    }

    /// <summary>
    /// 敌人数据 ScriptableObject — 可配置的敌人属性
    /// </summary>
    [CreateAssetMenu(menuName = "DevouringBeast/Enemy Data", fileName = "EnemyData")]
    public class EnemyData : ScriptableObject
    {
        [Header("基础")]
        public string displayName = "Enemy";
        public EnemyType enemyType = EnemyType.Normal;
        public int tier = 1; // 等级 1~7

        [Header("战斗属性")]
        public float maxHealth = 100f;
        public float attackDamage = 1f;
        public float moveSpeed = 3f;
        public float attackRange = 1.5f;
        [Tooltip("两次攻击动画开始之间的间隔（秒）")]
        public float attackCooldown = 0.9f;
        public float detectRange = 8f;

        [Header("吸入属性")]
        public ItemTag tag = ItemTag.Normal;
        public float killMass = 20f;       // 击杀质量（活着时吸入）
        public float deadMass = 5f;        // 阵亡质量（死后吸入）
        public float aliveInhaleThreshold = 50f;
        public float deadInhaleThreshold = 10f;

        [Header("动画")]
        public RuntimeAnimatorController animatorController;

        [Header("生成动画")]
        public AnimationClip popInClip;

        /// <summary>
        /// 应用波次难度系数，返回新的 EnemyData 副本
        /// </summary>
        public EnemyData ApplyScaling(float healthMul, float damageMul, float speedMul)
        {
            var copy = CreateInstance<EnemyData>();
            copy.displayName = displayName;
            copy.enemyType = enemyType;
            copy.tier = tier;
            copy.maxHealth = maxHealth * healthMul;
            copy.attackDamage = attackDamage * damageMul;
            copy.moveSpeed = moveSpeed * speedMul;
            copy.attackRange = attackRange;
            copy.attackCooldown = attackCooldown;
            copy.detectRange = detectRange;
            copy.tag = tag;
            copy.killMass = killMass;
            copy.deadMass = deadMass;
            copy.aliveInhaleThreshold = aliveInhaleThreshold;
            copy.deadInhaleThreshold = deadInhaleThreshold;
            copy.animatorController = animatorController;
            copy.popInClip = popInClip;
            return copy;
        }
    }
}
