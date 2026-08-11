#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DevouringBeast.Editor
{
    public static class EnemyConfigTuningBuilder
    {
        [MenuItem("Tools/Devouring Beast/Apply Enemy Config Tuning")]
        public static void Apply()
        {
            Dictionary<EnemyArchetype, (float aimed, float radial, float dash, float jump, float fireball, int deathStart)> values =
                new()
                {
                    [EnemyArchetype.Baby] = (8f, 6f, 0f, 0f, 0f, 17),
                    [EnemyArchetype.SkeletonMan] = (8f, 7f, 1.8f, 0f, 0f, -1),
                    [EnemyArchetype.LittleSatan] = (8f, 7f, 2f, 0f, 0.72f, 25),
                    [EnemyArchetype.Satan] = (8f, 7f, 0f, 0f, 0.72f, 11),
                    [EnemyArchetype.MeatMountain] = (8f, 7f, 0f, 0f, 0f, 16),
                    [EnemyArchetype.Skeleton] = (8f, 7f, 0f, 0f, 0f, -1),
                    [EnemyArchetype.DoubleWhite] = (8f, 7f, 0f, 0f, 0f, -1),
                    [EnemyArchetype.GreenBubble] = (8f, 7f, 0f, 0f, 0f, 15),
                    [EnemyArchetype.BigMeatballs] = (8f, 7f, 0f, 0f, 0f, 9),
                    [EnemyArchetype.HomeSpider] = (8f, 7f, 0f, 0f, 0f, 9),
                    [EnemyArchetype.BigSpider] = (8f, 7f, 0f, 0f, 0f, -1),
                    [EnemyArchetype.Gloomy] = (8f, 7f, 1.75f, 0f, 0f, 16),
                    [EnemyArchetype.Bat] = (8f, 7f, 0f, 0f, 0f, -1),
                    [EnemyArchetype.Fly] = (8f, 7f, 0f, 0f, 0f, -1),
                    [EnemyArchetype.GroundWorm] = (8f, 7f, 0f, 0f, 0f, -1),
                    [EnemyArchetype.Meatballs] = (8f, 7f, 0f, 0f, 0f, -1),
                    [EnemyArchetype.BloodBag] = (8f, 7f, 0f, 0f, 0f, -1),
                    [EnemyArchetype.Spider] = (8f, 7f, 0f, 1.4f, 0f, -1),
                    [EnemyArchetype.Mushroom] = (8f, 7f, 0f, 0f, 0f, -1),
                    [EnemyArchetype.White] = (8f, 7f, 0f, 0f, 0f, -1)
                };

            foreach (EnemyData data in LoadAllConfigs())
            {
                if (!values.TryGetValue(data.archetype, out var value)) continue;
                data.aimedProjectileSpeed = value.aimed;
                data.radialProjectileSpeed = value.radial;
                if (data.behavior == null) data.behavior = new EnemyBehaviorSettings();
                data.behavior.dashSpeed = value.dash;
                data.behavior.jumpSpeed = value.jump;
                data.behavior.fireballFallDuration = value.fireball;
                data.deathAnimationStartFrame = value.deathStart;
                EditorUtility.SetDirty(data);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[EnemyConfigTuningBuilder] Applied projectile, movement and death animation tuning.");
        }

        private static IEnumerable<EnemyData> LoadAllConfigs()
        {
            string[] guids = AssetDatabase.FindAssets("t:EnemyData", new[] { "Assets/_Project/Config/Enemies" });
            foreach (string guid in guids)
            {
                EnemyData data = AssetDatabase.LoadAssetAtPath<EnemyData>(AssetDatabase.GUIDToAssetPath(guid));
                if (data != null) yield return data;
            }
        }
    }
}
#endif
