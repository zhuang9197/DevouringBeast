using UnityEngine;
using System;

namespace DevouringBeast
{
    public enum EnemyArchetype
    {
        Baby, SkeletonMan, LittleSatan, Satan, MeatMountain,
        Skeleton, DoubleWhite, GreenBubble, BigMeatballs, HomeSpider, BigSpider, Gloomy,
        Bat, Fly, GroundWorm, Meatballs, BloodBag, Spider, Mushroom, White
    }

    public enum EnemyDeathMode
    {
        StaticDeathSprite,
        DeathAnimationKeepLastFrame,
        DeathAnimationDropChest,
        DropChest
    }

    public enum EnemyDeathEffect
    {
        None,
        AreaExplosion,
        SummonSpidersAndOvary,
        SplitWhiteVariants,
        DropFullHeart
    }

    [Serializable]
    public sealed class EnemyDeathEffectSettings
    {
        public EnemyDeathEffect effect;
        [Min(0f)] public float delay;
        [Min(0f)] public float radius;
        [Min(0)] public int damage;
        public bool knockback;
        [Min(0)] public int summonCount;
        [Range(0f, 1f)] public float secondaryChance;

        public EnemyDeathEffectSettings Copy() => (EnemyDeathEffectSettings)MemberwiseClone();
    }

    [Serializable]
    public sealed class EnemyBehaviorSettings
    {
        [Header("游走与规避")]
        public Vector2 wanderIntervalRange;
        [Min(0f)] public float proximityRange;
        [Min(0f)] public float specialMoveSpeed;
        [Min(0f)] public float dashSpeed;
        [Min(0f)] public float jumpSpeed;
        [Min(0f)] public float fireballFallDuration;
        [Min(0f)] public float dashDuration;
        [Min(0f)] public float movementCycleDuration;
        [Min(0f)] public float movementActiveDuration;
        [Min(0f)] public float movementIdleDuration;
        [Min(0)] public int actionsPerSpecial;
        [Range(0f, 180f)] public float wanderMaximumTurnAngle;
        [Min(0f)] public float wanderTurnSpeed;
        [Min(0f)] public float evasiveTurnSpeed;

        [Header("圆周追踪")]
        [Min(0f)] public float orbitAngularSpeed;
        [Min(0f)] public float orbitPursuitWeight;
        [Min(0f)] public float orbitTangentWeight;
        [Min(0f)] public float orbitSeparationWeight;

        [Header("动作节奏")]
        [Min(0f)] public float specialAttackCooldown;

        [Header("特殊投射物")]
        [Min(0)] public int specialProjectileWaves;
        [Min(0)] public int specialProjectileCount;
        [Min(0f)] public float specialProjectileInterval;
        [Range(0f, 360f)] public float specialProjectileAngle = 360f;
        [Range(-360f, 360f)] public float specialProjectileAngleStep;
        [Min(0)] public int fireballCount;
        [Min(0f)] public float fireballInterval;
        [Min(0)] public int fireballRadialBulletCount;

        [Header("离地动作")]
        [Min(0f)] public float jumpHeight;
        [Min(0f)] public float takeoffDuration;
        [Min(0f)] public float airborneDuration;
        [Min(0f)] public float landingDuration;
        [Min(0f)] public float offscreenPadding = 2f;

        [Header("阶段与姿态切换")]
        [Min(0f)] public float stateTransitionDuration = 1f;
        [Min(0f)] public float stateHoldDuration = 5f;
        [Min(0f)] public float dashPreparationDuration = 0.5f;
        [Min(0f)] public float dashRecoveryDuration = 0.5f;

        [Header("受伤阈值效果")]
        [Range(0f, 1f)] public float healthLossEffectInterval;
        [Min(0)] public int healthLossEffectMaximumTriggers;
        [Min(0)] public int healthLossEffectBulletCount;
        public bool healthLossEffectSummonsMeatball;
        [Min(1f)] public float healthLossEffectMaximumScale = 1.35f;
        [Min(0.01f)] public float healthLossEffectPulseDuration = 0.12f;

        public EnemyBehaviorSettings Copy(float speedMultiplier)
        {
            return new EnemyBehaviorSettings
            {
                wanderIntervalRange = wanderIntervalRange,
                proximityRange = proximityRange,
                specialMoveSpeed = specialMoveSpeed * speedMultiplier,
                dashSpeed = dashSpeed * speedMultiplier,
                jumpSpeed = jumpSpeed * speedMultiplier,
                fireballFallDuration = fireballFallDuration,
                dashDuration = dashDuration,
                movementCycleDuration = movementCycleDuration,
                movementActiveDuration = movementActiveDuration,
                movementIdleDuration = movementIdleDuration,
                actionsPerSpecial = actionsPerSpecial,
                wanderMaximumTurnAngle = wanderMaximumTurnAngle,
                wanderTurnSpeed = wanderTurnSpeed,
                evasiveTurnSpeed = evasiveTurnSpeed,
                orbitAngularSpeed = orbitAngularSpeed,
                orbitPursuitWeight = orbitPursuitWeight,
                orbitTangentWeight = orbitTangentWeight,
                orbitSeparationWeight = orbitSeparationWeight,
                specialAttackCooldown = specialAttackCooldown,
                specialProjectileWaves = specialProjectileWaves,
                specialProjectileCount = specialProjectileCount,
                specialProjectileInterval = specialProjectileInterval,
                specialProjectileAngle = specialProjectileAngle,
                specialProjectileAngleStep = specialProjectileAngleStep,
                fireballCount = fireballCount,
                fireballInterval = fireballInterval,
                fireballRadialBulletCount = fireballRadialBulletCount,
                jumpHeight = jumpHeight,
                takeoffDuration = takeoffDuration,
                airborneDuration = airborneDuration,
                landingDuration = landingDuration,
                offscreenPadding = offscreenPadding,
                stateTransitionDuration = stateTransitionDuration,
                stateHoldDuration = stateHoldDuration,
                dashPreparationDuration = dashPreparationDuration,
                dashRecoveryDuration = dashRecoveryDuration,
                healthLossEffectInterval = healthLossEffectInterval,
                healthLossEffectMaximumTriggers = healthLossEffectMaximumTriggers,
                healthLossEffectBulletCount = healthLossEffectBulletCount,
                healthLossEffectSummonsMeatball = healthLossEffectSummonsMeatball,
                healthLossEffectMaximumScale = healthLossEffectMaximumScale,
                healthLossEffectPulseDuration = healthLossEffectPulseDuration
            };
        }
    }

    /// <summary>
    /// 敌人数据 ScriptableObject — 可配置的敌人属性
    /// </summary>
    [CreateAssetMenu(menuName = "DevouringBeast/Enemy Data", fileName = "EnemyData")]
    public class EnemyData : ScriptableObject
    {
        [Header("基础")]
        public string displayName = "Enemy";
        public EnemyArchetype archetype = EnemyArchetype.Bat;
        public int tier = 1; // 等级 1~7

        [Header("战斗属性")]
        public float maxHealth = 50f;
        public float attackDamage = 1f;
        [Tooltip("Normalized speed: the player's configured base speed is 1; enemies are capped at 2.")]
        public float moveSpeed = 1f;
        public float attackRange = 1.5f;
        [Tooltip("两次攻击动画开始之间的间隔（秒）")]
        public float attackCooldown = 0.9f;
        public float detectRange = 8f;

        [Header("投射物")]
        [Min(0f)] public float aimedProjectileSpeed = 8f;
        [Min(0f)] public float radialProjectileSpeed = 7f;
        [Min(0)] public int radialProjectileCount;
        [Range(0f, 360f)] public float radialProjectileAngle = 360f;

        [Header("行为参数")]
        public EnemyBehaviorSettings behavior = new();

        [Header("吸入属性")]
        public float massValue = 5f;
        public float aliveInhaleThreshold = 50f;
        public float deadInhaleThreshold = 10f;

        [Header("动画")]
        public RuntimeAnimatorController animatorController;
        public Sprite fakeDeathHoldSprite;
        public Sprite phaseTwoIdleSprite;

        [Header("生成动画")]
        public AnimationClip popInClip;

        [Header("死亡表现")]
        public EnemyDeathMode deathMode = EnemyDeathMode.DropChest;
        public Sprite deathSprite;
        [Min(0f)] public float deathAnimationDuration;
        [Tooltip("源 Texture 中死亡动画的起始帧索引；-1 表示从死亡帧前 5 帧开始。")]
        public int deathAnimationStartFrame = -1;
        [Tooltip("源 Texture 帧索引；-1 表示使用最后一帧。")]
        public int deathFrameIndex = -1;
        public EnemyDeathEffectSettings deathEffect = new();

        /// <summary>
        /// 应用波次难度系数，返回新的 EnemyData 副本
        /// </summary>
        public EnemyData ApplyScaling(float healthMul, float damageMul, float speedMul)
        {
            var copy = CreateInstance<EnemyData>();
            copy.displayName = displayName;
            copy.archetype = archetype;
            copy.tier = tier;
            copy.maxHealth = maxHealth * healthMul;
            copy.attackDamage = attackDamage * damageMul;
            copy.moveSpeed = moveSpeed * speedMul;
            copy.attackRange = attackRange;
            copy.attackCooldown = attackCooldown;
            copy.detectRange = detectRange;
            copy.aimedProjectileSpeed = aimedProjectileSpeed;
            copy.radialProjectileSpeed = radialProjectileSpeed;
            copy.radialProjectileCount = radialProjectileCount;
            copy.radialProjectileAngle = radialProjectileAngle;
            copy.behavior = behavior != null ? behavior.Copy(speedMul) : new EnemyBehaviorSettings();
            copy.massValue = massValue;
            copy.aliveInhaleThreshold = aliveInhaleThreshold;
            copy.deadInhaleThreshold = deadInhaleThreshold;
            copy.animatorController = animatorController;
            copy.fakeDeathHoldSprite = fakeDeathHoldSprite;
            copy.phaseTwoIdleSprite = phaseTwoIdleSprite;
            copy.popInClip = popInClip;
            copy.deathMode = deathMode;
            copy.deathSprite = deathSprite;
            copy.deathAnimationDuration = deathAnimationDuration;
            copy.deathAnimationStartFrame = deathAnimationStartFrame;
            copy.deathFrameIndex = deathFrameIndex;
            copy.deathEffect = deathEffect != null ? deathEffect.Copy() : new EnemyDeathEffectSettings();
            return copy;
        }
    }
}
