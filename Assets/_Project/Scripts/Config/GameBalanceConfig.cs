using System;
using UnityEngine;

namespace DevouringBeast
{
    [CreateAssetMenu(menuName = "DevouringBeast/Config/Game Balance", fileName = "GameBalanceConfig")]
    public sealed class GameBalanceConfig : ScriptableObject
    {
        public const string ResourceName = "GameBalanceConfig";

        [SerializeField] private PlayerBalanceSettings player = new();
        [SerializeField] private InhaleBalanceSettings inhale = new();
        [SerializeField] private SpitBalanceSettings spit = new();
        [SerializeField] private FoodBalanceSettings food = new();
        [SerializeField] private StatueBalanceSettings statues = new();
        [SerializeField] private EnemyCommonBalanceSettings enemy = new();

        public PlayerBalanceSettings Player => player;
        public InhaleBalanceSettings Inhale => inhale;
        public SpitBalanceSettings Spit => spit;
        public FoodBalanceSettings Food => food;
        public StatueBalanceSettings Statues => statues;
        public EnemyCommonBalanceSettings Enemy => enemy;
    }

    [Serializable]
    public sealed class PlayerBalanceSettings
    {
        [Header("基础属性")]
        [Min(0.01f)] public float baseMoveSpeed;
        [Min(0f)] public float baseSuction;
        [Min(0f)] public float baseEnergyBallDamage;
        [Min(1)] public int maxHealth;
        [Min(0f)] public float invincibleDuration;
        [Range(0.1f, 1f)] public float visualColliderRadiusScale = 0.8f;
        [Min(0.01f)] public float minimumColliderRadius = 0.55f;

        [Header("移动状态")]
        [Range(0f, 1f)] public float fullWalkSpeedMultiplier;
        [Range(0f, 1f)] public float inhaleWalkSpeedMultiplier;
        [Min(0.05f)] public float runStepInterval;
        [Min(0.05f)] public float walkStepInterval;
        [Min(0f)] public float idleSoundDelay;
        [Min(0.1f)] public float idleSoundRepeatInterval;
        [Min(0f)] public float knockbackDuration;

        [Header("野兽形态")]
        [Min(1f)] public float beastRollingSpeedMultiplier;
        [Range(0f, 1f)] public float beastDamageReductionBase;
        [Range(0f, 1f)] public float beastDamageReductionPerLevel;
        [Range(0f, 1f)] public float beastDamageReductionLimit;
        [Min(0f)] public float beastHitRadius;
        [Min(0f)] public float beastDamagePerSecond;
        [Min(0f)] public float beastDamagePerLevel;
        [Min(0f)] public float beastHitSoundCooldown;
    }

    [Serializable]
    public sealed class InhaleBalanceSettings
    {
        [Range(0f, 360f)] public float angle;
        [Min(0f)] public float radius;
        [Min(0f)] public float maximumDuration;
        [Min(0f)] public float maximumSuctionForce;
        [Min(0.01f)] public float suctionRampTime;
        [Min(0.05f)] public float intakeDistance;
        [Min(0.1f)] public float minimumPullSpeed;
        [Min(0.1f)] public float maximumPullSpeed;
        [Min(1f)] public float corpsePullSpeedMultiplier;
        [Min(0.1f)] public float corpseMaximumPullSpeed;
        [Min(0f)] public float suctionMassSpeedFactor;
        [Range(1f, 1.5f)] public float aliveEnemyMaximumSpeedBoost;
    }

    [Serializable]
    public sealed class SpitBalanceSettings
    {
        [Min(0f)] public float speed;
        [Min(0.01f)] public float maximumDistance;
        [Min(0f)] public float spawnForwardOffset;
        [Min(0)] public int poolInitialSize;
        [Min(1)] public int poolMaximumSize;
        [Min(0.01f)] public float maximumChargeTime;
        [Min(0f)] public float bigMassThreshold;
        [Min(0f)] public float spreadAngle;
        [Min(0f)] public float angelShotCooldown;
        [Min(0f)] public float popeDamageMultiplier;
        [Min(0f)] public float chargeBonusPerLevel;
        [Range(0f, 1f)] public float multipleMouthPerBallMultiplier;
        [Min(0f)] public float multipleMouthPowerPerLevel;
        [Min(1)] public int maximumBallCount;
    }

    [Serializable]
    public sealed class FoodBalanceSettings
    {
        [Tooltip("Total normal food budget assigned when a room is first entered.")]
        [Min(1)] public int initialFoodPerRoom;
        [Tooltip("Number of normal food items spawned on each refresh.")]
        [Min(1)] public int refreshBatchSize;
        [Tooltip("Maximum spawned food allowed in one room.")]
        [Min(1)] public int maxActiveFood;
        [Tooltip("Spawn interval while combat is active.")]
        [Min(0.1f)] public float refreshSeconds;
        [Tooltip("Spawn interval after a room is cleared.")]
        [Min(0.1f)] public float clearedRefreshSeconds;
        [Tooltip("Pope guarantee interval when an uncleared room has no budget and no spawned food.")]
        [Min(0.1f)] public float popeGuaranteeRefreshSeconds;
        [Min(0.25f)] public float minimumSpacing;
        [Min(0f)] public float boundsPadding;
        [Min(1)] public int placementAttempts;
        [Min(0f)] public float landingDuration;
        [Min(0.01f)] public float colliderRadius;
        [Min(0.01f)] public float worldScale;
        [Min(0f)] public float riceBallMass;
        [Min(0f)] public float baoziMass;
        [Min(0f)] public float hotDogMass;
        [Min(0f)] public float sushiMass;

        public float GetMass(FoodKind kind) => kind switch
        {
            FoodKind.Baozi => baoziMass,
            FoodKind.HotDog => hotDogMass,
            FoodKind.Sushi => sushiMass,
            _ => riceBallMass
        };
    }

    [Serializable]
    public sealed class StatueBalanceSettings
    {
        [Min(1)] public int healthCost;
        [Min(1)] public int angelBreakHits;
        [Min(1)] public int angelHeartDrops;
        [Min(1)] public int popeFoodPerHealth;
        [Min(1)] public int popeFoodPerOffering;
        [Min(0.5f)] public float visualHeight;
        [Range(0.1f, 0.95f)] public float frontContactDot;
    }

    [Serializable]
    public sealed class EnemyCommonBalanceSettings
    {
        [Header("移动与群体转向")]
        [Min(0.01f)] public float normalizedSpeedLimit;
        [Min(0f)] public float separationRadius;
        [Min(0f)] public float chaseWeight;
        [Min(0f)] public float irregularChaseWeight;
        [Min(0f)] public float separationWeight;
        [Min(0f)] public float horizontalFacingDeadZone;
        public Vector2 initialAttackDelayRange;
        public Vector2 steeringSpeedRange;
        public Vector2 steeringRadiusRange;

        [Header("接触与击退")]
        [Min(0f)] public float colliderContactTolerance;
        [Min(0f)] public float fallbackContactRadius;
        [Min(0f)] public float contactKnockbackDistance;
        [Min(0f)] public float areaKnockbackDistance;
        [Min(0f)] public float contactCooldown;
        [Range(0.1f, 1f)] public float visualColliderRadiusScale = 0.8f;
        [Min(0.01f)] public float minimumColliderRadius = 0.5f;

        [Header("投射物与召唤")]
        [Min(0f)] public float aimedProjectileSpeed;
        [Min(0f)] public float radialProjectileSpeed;
        [Min(0f)] public float summonOffsetMinimum;
        [Min(0f)] public float summonOffsetMaximum;
        [Min(0f)] public float corpseLifetime;

        [Header("火球")]
        [Min(0.1f)] public float fireballFallHeight;
        [Min(0f)] public float fireballOffscreenPadding;
        [Min(0.1f)] public float fireballFallDuration;
        [Min(0f)] public float fireballOrbitRadius;
        [Min(0f)] public float fireballOrbitTurns;
        [Min(0.1f)] public float fireballVisualScale;
        [Min(0.1f)] public float fireballParticleScale;
        [Min(0.1f)] public float fireballLandingMarkerScale;
        [Min(0f)] public float fireballExplosionRadius;
        [Min(1)] public int fireballExplosionDamage;
        [Min(0f)] public float fireballBurnRadius;
        [Min(0f)] public float fireballBurnDuration;
        [Min(0)] public int fireballBurnDamage;
        [Min(0.1f)] public float fireballBurnVisualScale;
    }

    public static class GameBalance
    {
        private static GameBalanceConfig _current;
        private static bool _loadAttempted;

        public static GameBalanceConfig Current
        {
            get
            {
                if (!_loadAttempted)
                {
                    _loadAttempted = true;
                    _current = Resources.Load<GameBalanceConfig>(GameBalanceConfig.ResourceName);
                    if (_current == null)
                        Debug.LogError($"[GameBalance] Missing Resources/{GameBalanceConfig.ResourceName}.asset.");
                }
                return _current;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _current = null;
            _loadAttempted = false;
        }
    }
}
