using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DevouringBeast.Editor
{
    public static class GameConfigValidator
    {
        public const string BalancePath = "Assets/_Project/Config/Resources/GameBalanceConfig.asset";
        public const string WavePath = "Assets/_Project/Config/Balance/WaveConfig.asset";
        public const string EnemyConfigRoot = "Assets/_Project/Config/Enemies";
        public const string RogueCatalogPath = "Assets/_Project/Config/Resources/Rogue/RogueSkillCatalog.asset";

        [MenuItem("Tools/Devouring Beast/Validate Game Configuration")]
        public static void ValidateFromMenu()
        {
            ValidateOrThrow();
            Debug.Log("[GameConfigValidator] Game configuration is valid.");
        }

        public static void ValidateOrThrow()
        {
            List<string> errors = new();
            GameBalanceConfig balance = AssetDatabase.LoadAssetAtPath<GameBalanceConfig>(BalancePath);
            if (balance == null)
                errors.Add($"Missing {BalancePath}");
            else
                ValidateBalance(balance, errors);

            WaveConfig wave = AssetDatabase.LoadAssetAtPath<WaveConfig>(WavePath);
            if (wave == null)
                errors.Add($"Missing {WavePath}");

            ValidateEnemies(balance, errors);
            ValidateRogueCatalog(errors);
            if (errors.Count > 0)
                throw new BuildFailedException("Game configuration validation failed:\n- " +
                    string.Join("\n- ", errors));
        }

        private static void ValidateBalance(GameBalanceConfig config, List<string> errors)
        {
            if (config.Player.baseMoveSpeed <= 0f) errors.Add("Player base move speed must be positive.");
            if (config.Player.maxHealth <= 0) errors.Add("Player maximum health must be positive.");
            if (config.Player.visualColliderRadiusScale <= 0f || config.Player.minimumColliderRadius <= 0f)
                errors.Add("Player visual collider scale and minimum radius must be positive.");
            if (config.Inhale.radius <= 0f || config.Inhale.maximumDuration <= 0f)
                errors.Add("Inhale radius and duration must be positive.");
            if (config.Spit.speed <= 0f || config.Spit.maximumDistance <= 0f)
                errors.Add("Energy-ball speed and distance must be positive.");
            if (config.Spit.poolMaximumSize < config.Spit.poolInitialSize)
                errors.Add("Energy-ball pool maximum size cannot be smaller than its initial size.");
            if (config.Food.refreshBatchSize <= 0 || config.Food.refreshSeconds <= 0f ||
                config.Food.popeGuaranteeRefreshSeconds <= 0f)
                errors.Add("Food room limit and refresh interval must be positive.");
            if (config.Enemy.normalizedSpeedLimit <= 0f)
                errors.Add("Enemy normalized speed limit must be positive.");
            if (config.Enemy.separationRadius <= 0f)
                errors.Add("Enemy separation radius must be positive.");
            if (config.Enemy.visualColliderRadiusScale <= 0f || config.Enemy.minimumColliderRadius <= 0f)
                errors.Add("Enemy visual collider scale and minimum radius must be positive.");
            if (config.Enemy.fireballFallHeight <= 0f || config.Enemy.fireballOffscreenPadding <= 0f ||
                config.Enemy.fireballFallDuration <= 0f || config.Enemy.fireballVisualScale <= 0f ||
                config.Enemy.fireballParticleScale <= 0f || config.Enemy.fireballLandingMarkerScale <= 0f ||
                config.Enemy.fireballExplosionRadius <= 0f || config.Enemy.fireballExplosionDamage <= 0 ||
                config.Enemy.fireballBurnRadius <= 0f ||
                config.Enemy.fireballBurnDuration <= 0f || config.Enemy.fireballBurnDamage <= 0 ||
                config.Enemy.fireballBurnVisualScale <= 0f)
                errors.Add("Enemy fireball fall, explosion and burn settings must be positive.");
        }

        private static void ValidateEnemies(GameBalanceConfig balance, List<string> errors)
        {
            string[] guids = AssetDatabase.FindAssets("t:EnemyData", new[] { EnemyConfigRoot });
            List<EnemyData> configs = guids
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<EnemyData>)
                .Where(data => data != null)
                .ToList();
            EnemyArchetype[] expected = (EnemyArchetype[])Enum.GetValues(typeof(EnemyArchetype));
            if (configs.Count != expected.Length)
                errors.Add($"Expected {expected.Length} enemy configs, found {configs.Count}.");

            foreach (EnemyArchetype archetype in expected)
            {
                List<EnemyData> matches = configs.Where(data => data.archetype == archetype).ToList();
                if (matches.Count != 1)
                {
                    errors.Add($"Enemy archetype {archetype} has {matches.Count} configs; expected exactly one.");
                    continue;
                }
                ValidateEnemy(matches[0], balance, errors);
            }

            string[] generatedConfigs = AssetDatabase.FindAssets("t:EnemyData",
                new[] { "Assets/_Project/Generated/Enemies" });
            if (generatedConfigs.Length > 0)
                errors.Add("Generated enemy folders must not contain editable EnemyData assets.");
        }

        private static void ValidateEnemy(EnemyData data, GameBalanceConfig balance, List<string> errors)
        {
            string path = AssetDatabase.GetAssetPath(data);
            if (data.maxHealth <= 0f) errors.Add($"{path}: maximum health must be positive.");
            if (data.attackDamage < 0f) errors.Add($"{path}: attack damage cannot be negative.");
            if (data.moveSpeed < 0f) errors.Add($"{path}: movement speed cannot be negative.");
            if (balance != null && data.moveSpeed > balance.Enemy.normalizedSpeedLimit)
                errors.Add($"{path}: movement speed exceeds the configured enemy speed limit.");
            if (data.attackCooldown <= 0f) errors.Add($"{path}: attack cooldown must be positive.");
            if (data.massValue <= 0f) errors.Add($"{path}: mass value must be positive.");
            if (data.behavior == null)
            {
                errors.Add($"{path}: behavior settings are missing.");
                return;
            }

            switch (data.archetype)
            {
                case EnemyArchetype.Bat:
                    if (data.behavior.orbitAngularSpeed <= 0f || data.behavior.orbitTangentWeight <= 0f)
                        errors.Add($"{path}: bat orbit settings must be positive.");
                    break;
                case EnemyArchetype.BloodBag:
                    if (data.behavior.proximityRange <= 0f || data.behavior.wanderTurnSpeed <= 0f ||
                        data.behavior.wanderIntervalRange.x <= 0f)
                        errors.Add($"{path}: BloodBag flee and wander settings must be positive.");
                    break;
                case EnemyArchetype.HomeSpider:
                    if (data.behavior.proximityRange <= 0f || data.behavior.specialMoveSpeed <= 0f)
                        errors.Add($"{path}: HomeSpider flee range and speed must be positive.");
                    break;
                case EnemyArchetype.BigSpider:
                    if (data.behavior.movementActiveDuration <= 0f || data.behavior.movementIdleDuration <= 0f)
                        errors.Add($"{path}: BigSpider movement phase durations must be positive.");
                    break;
                case EnemyArchetype.Spider:
                    if (data.behavior.movementCycleDuration <= 0f || data.behavior.dashDuration <= 0f)
                        errors.Add($"{path}: Spider movement cycle and dash duration must be positive.");
                    break;
            }

            if (data.archetype == EnemyArchetype.LittleSatan || data.archetype == EnemyArchetype.Satan ||
                data.archetype == EnemyArchetype.MeatMountain || data.archetype == EnemyArchetype.Spider)
            {
                if (data.behavior.jumpHeight <= 0f || data.behavior.takeoffDuration <= 0f ||
                    data.behavior.landingDuration <= 0f)
                    errors.Add($"{path}: airborne height and takeoff/landing durations must be positive.");
            }

            if (data.behavior.healthLossEffectInterval > 0f &&
                (data.behavior.healthLossEffectMaximumTriggers <= 0 || data.behavior.healthLossEffectBulletCount <= 0 ||
                 data.behavior.healthLossEffectMaximumScale <= 1f ||
                 data.behavior.healthLossEffectPulseDuration <= 0f))
                errors.Add($"{path}: health-loss effect interval requires a trigger limit and bullet count.");

            if ((data.archetype == EnemyArchetype.Skeleton || data.archetype == EnemyArchetype.SkeletonMan) &&
                (data.behavior.stateTransitionDuration <= 0f || data.behavior.stateHoldDuration <= 0f))
                errors.Add($"{path}: fake-death transition and hold durations must be positive.");
            if (data.archetype == EnemyArchetype.LittleSatan &&
                (data.behavior.stateTransitionDuration <= 0f || data.behavior.dashDuration <= 0f ||
                 data.behavior.dashPreparationDuration <= 0f || data.behavior.dashRecoveryDuration <= 0f))
                errors.Add($"{path}: phase-two transform and dash timings must be positive.");

            if (data.deathEffect != null && data.deathEffect.effect == EnemyDeathEffect.AreaExplosion &&
                (data.deathEffect.radius <= 0f || data.deathEffect.damage <= 0))
                errors.Add($"{path}: death explosion radius and damage must be positive.");
        }

        private static void ValidateRogueCatalog(List<string> errors)
        {
            RogueSkillCatalog catalog = AssetDatabase.LoadAssetAtPath<RogueSkillCatalog>(RogueCatalogPath);
            if (catalog == null)
            {
                errors.Add($"Missing {RogueCatalogPath}");
                return;
            }

            HashSet<RogueSkillId> ids = new();
            foreach (RogueSkillDefinition skill in catalog.skills)
            {
                if (skill == null) continue;
                if (!ids.Add(skill.id)) errors.Add($"{RogueCatalogPath}: duplicate skill {skill.id}.");
                foreach (RogueSkillId prerequisite in skill.prerequisites ?? Array.Empty<RogueSkillId>())
                {
                    if (prerequisite == skill.id)
                        errors.Add($"{RogueCatalogPath}: skill {skill.id} cannot require itself.");
                }
            }

            foreach (RogueSkillDefinition skill in catalog.skills)
                foreach (RogueSkillId prerequisite in skill?.prerequisites ?? Array.Empty<RogueSkillId>())
                    if (!ids.Contains(prerequisite))
                        errors.Add($"{RogueCatalogPath}: {skill.id} has missing prerequisite {prerequisite}.");

            Dictionary<RogueSkillId, RogueSkillId[]> requiredLinks = new()
            {
                { RogueSkillId.FirePyroblastFlame, new[] { RogueSkillId.FirePyroblast } },
                { RogueSkillId.FirePyroblastScope, new[] { RogueSkillId.FirePyroblast } },
                { RogueSkillId.EvolutionMoreMouthMore, new[] { RogueSkillId.EvolutionMoreMouth } },
                { RogueSkillId.EvolutionMoreMouthPower, new[] { RogueSkillId.EvolutionMoreMouth } },
                { RogueSkillId.SuperSplitMore, new[] { RogueSkillId.SuperSplit } },
                { RogueSkillId.PoisonLegacy, new[] { RogueSkillId.PoisonDeadly, RogueSkillId.SuperSplitMore } },
                { RogueSkillId.FireLegacy, new[] { RogueSkillId.FireBottle, RogueSkillId.SuperSplitMore } }
            };
            foreach (var pair in requiredLinks)
            {
                RogueSkillDefinition skill = catalog.Get(pair.Key);
                if (skill == null || pair.Value.Any(required =>
                        !(skill.prerequisites ?? Array.Empty<RogueSkillId>()).Contains(required)))
                    errors.Add($"{RogueCatalogPath}: required prerequisite links are missing for {pair.Key}.");
            }
        }
    }

    public sealed class GameConfigBuildPreprocessor : IPreprocessBuildWithReport
    {
        public int callbackOrder => -1000;
        public void OnPreprocessBuild(BuildReport report) => GameConfigValidator.ValidateOrThrow();
    }
}
