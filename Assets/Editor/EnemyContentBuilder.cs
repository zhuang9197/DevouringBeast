#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

namespace DevouringBeast.Editor
{
    public static class EnemyContentBuilder
    {
        private const string SourceRoot = "Assets/Art/Sprites/Enemies";
        private const string GeneratedRoot = "Assets/_Project/Generated/Enemies";
        private const string EnemyConfigRoot = "Assets/_Project/Config/Enemies";
        private const string CatalogPath = "Assets/Resources/System/EnemyPrefabCatalog.asset";
        private const string AllContentLabel = "EnemyContent_All";
        private const string MinionGroup = "Group_Minions";
        private const string EliteGroup = "Group_Elites";
        private const string BossGroup = "Group_Bosses";

        private sealed class Spec
        {
            public EnemyArchetype Archetype;
            public string Folder;
            public bool Boss;
            public bool Elite;
            public (int start, int end, float duration) Move;
            public (int start, int end, float duration) SkillA;
            public (int start, int end, float duration) SkillB;
            public (int start, int end, float duration) Special;
        }

        [MenuItem("Tools/Devouring Beast/Build New Enemy Content")]
        public static void Build()
        {
            GameConfigValidator.ValidateOrThrow();
            DeleteGeneratedAsset(GeneratedRoot);
            DeleteGeneratedAsset("Assets/Resources/Enemies");
            DeleteGeneratedAsset(CatalogPath);
            EnsureFolder(GeneratedRoot);
            EnsureFolder(EnemyConfigRoot);
            EnsureFolder("Assets/Resources/Enemy");
            EnsureFolder("Assets/Resources/Drops");
            if (AssetDatabase.IsValidFolder("Assets/Art/Enemy/Prefabs"))
                AssetDatabase.DeleteAsset("Assets/Art/Enemy/Prefabs");

            List<Spec> specs = CreateSpecs();
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            settings.BuildAddressablesWithPlayerBuild = AddressableAssetSettings.PlayerBuildOption.BuildWithPlayer;
            Dictionary<EnemyContentCategory, AddressableAssetGroup> groups = CreateAddressableGroups(settings);
            foreach (Spec spec in specs)
            {
                EnemyContentDefinition definition = BuildEnemy(spec);
                ConfigureAddressableEntry(settings, groups[definition.Category], definition, spec.Folder);
            }

            BuildProjectilePrefab("EnemyBullet", "Assets/Art/Sprites/EnergyBall/bullet.png", false);
            BuildProjectilePrefab("EnemyFireball", "Assets/Art/Sprites/EnergyBall/fireball.png", true);
            BuildOvaryPrefab();
            BuildChestSpriteCopy();
            UnityEngine.SceneManagement.Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidateGeneratedContent(specs, groups);
            Debug.Log($"[EnemyContentBuilder] Built {specs.Count} atomic Addressable enemies with virtual Sprite Atlases.");
        }

        private static void ValidateGeneratedContent(List<Spec> specs,
            Dictionary<EnemyContentCategory, AddressableAssetGroup> groups)
        {
            foreach (Spec spec in specs)
            {
                string atlasPath = $"{SourceRoot}/{spec.Folder}/Atlas/{spec.Folder}.spriteatlas";
                SpriteAtlas atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(atlasPath);
                int expected = Directory.GetFiles($"{SourceRoot}/{spec.Folder}/Textures", "*.png").Length;
                int actual = atlas == null ? -1 : SpriteAtlasExtensions.GetPackables(atlas).Length;
                if (actual != expected)
                    throw new InvalidOperationException($"{spec.Folder} atlas has {actual} packables; expected {expected}.");
                if (SpriteAtlasExtensions.IsIncludeInBuild(atlas))
                    throw new InvalidOperationException($"{spec.Folder} atlas must not be included in the Player build directly.");

                EnemyContentDefinition definition = AssetDatabase.LoadAssetAtPath<EnemyContentDefinition>(
                    $"{GeneratedRoot}/{spec.Folder}/{spec.Folder}_Content.asset");
                if (definition == null || !definition.IsValid)
                    throw new InvalidOperationException($"{spec.Folder} content definition is incomplete.");
            }

            ValidateGroup(groups[EnemyContentCategory.Minion], 8);
            ValidateGroup(groups[EnemyContentCategory.Elite], 7);
            ValidateGroup(groups[EnemyContentCategory.Boss], 5);
        }

        private static void ValidateGroup(AddressableAssetGroup group, int expectedEntries)
        {
            BundledAssetGroupSchema schema = group?.GetSchema<BundledAssetGroupSchema>();
            if (group == null || group.entries.Count != expectedEntries || schema == null ||
                schema.BundleMode != BundledAssetGroupSchema.BundlePackingMode.PackSeparately)
                throw new InvalidOperationException($"Addressables group validation failed for {group?.Name ?? "<missing>"}.");
        }

        private static EnemyContentDefinition BuildEnemy(Spec spec)
        {
            string textureFolder = $"{SourceRoot}/{spec.Folder}/Textures";
            Sprite[] frames = Directory.Exists(textureFolder)
                ? Directory.GetFiles(textureFolder, "*.png").Select(AssetDatabase.LoadAssetAtPath<Sprite>)
                    .Where(sprite => sprite != null).OrderBy(sprite => NaturalIndex(sprite.name)).ToArray()
                : Array.Empty<Sprite>();
            if (frames.Length == 0) throw new InvalidOperationException($"No frames found for {spec.Folder}.");

            string contentFolder = $"{GeneratedRoot}/{spec.Folder}";
            EnsureFolder(contentFolder);
            string dataPath = $"{EnemyConfigRoot}/{spec.Folder}.asset";
            EnemyData data = AssetDatabase.LoadAssetAtPath<EnemyData>(dataPath);
            if (data == null)
                throw new InvalidOperationException($"Missing enemy config: {dataPath}");
            if (data.archetype != spec.Archetype)
                throw new InvalidOperationException($"{dataPath} archetype is {data.archetype}; expected {spec.Archetype}.");
            int deathFrame = data.deathFrameIndex >= 0 ? data.deathFrameIndex : frames.Length - 1;
            Sprite deathSprite = frames[Mathf.Clamp(deathFrame, 0, frames.Length - 1)];
            Sprite fakeDeathHoldSprite = spec.Archetype == EnemyArchetype.Skeleton ||
                spec.Archetype == EnemyArchetype.SkeletonMan
                ? frames[Mathf.Clamp(12, 0, frames.Length - 1)]
                : null;
            Sprite phaseTwoIdleSprite = spec.Archetype == EnemyArchetype.LittleSatan
                ? frames[Mathf.Clamp(1, 0, frames.Length - 1)]
                : null;

            string animationFolder = $"{contentFolder}/Animations";
            EnsureFolder(animationFolder);
            AnimatorController controller = BuildController(spec, data, frames, animationFolder);
            SerializedObject dataSerialized = new(data);
            dataSerialized.FindProperty("deathSprite").objectReferenceValue = deathSprite;
            dataSerialized.FindProperty("fakeDeathHoldSprite").objectReferenceValue = fakeDeathHoldSprite;
            dataSerialized.FindProperty("phaseTwoIdleSprite").objectReferenceValue = phaseTwoIdleSprite;
            dataSerialized.FindProperty("animatorController").objectReferenceValue = controller;
            dataSerialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);

            GameObject root = new(spec.Folder, typeof(Rigidbody2D), typeof(CircleCollider2D),
                typeof(InhaleableItem), typeof(EnemyBase), typeof(EnemyActor), typeof(EnemyPoolMember));
            GameObject visual = new("Visual", typeof(SpriteRenderer), typeof(Animator));
            visual.transform.SetParent(root.transform, false);
            SpriteRenderer renderer = visual.GetComponent<SpriteRenderer>();
            renderer.sprite = frames[0];
            renderer.sortingOrder = 4;
            renderer.material = CreateWhiteMaterialIfNeeded(spec, renderer);
            Animator animator = visual.GetComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            Rigidbody2D body = root.GetComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            CircleCollider2D collider = root.GetComponent<CircleCollider2D>();
            collider.radius = 0.42f;
            InhaleableItem item = root.GetComponent<InhaleableItem>();
            item.Mass = data.massValue;
            item.AliveInhaleThreshold = data.aliveInhaleThreshold;
            item.DeadInhaleThreshold = data.deadInhaleThreshold;
            item.IsAlive = true;
            item.SetRestingScale(Vector3.one * 0.85f);
            EnemyBase enemy = root.GetComponent<EnemyBase>();
            SerializedObject enemySerialized = new(enemy);
            enemySerialized.FindProperty("data").objectReferenceValue = data;
            enemySerialized.FindProperty("spriteRenderer").objectReferenceValue = renderer;
            enemySerialized.FindProperty("animator").objectReferenceValue = animator;
            enemySerialized.ApplyModifiedPropertiesWithoutUndo();
            GroundShadow.Ensure(root);
            string prefabPath = $"{contentFolder}/{spec.Folder}.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            UnityEngine.Object.DestroyImmediate(root);

            SpriteAtlas atlas = BuildAtlas(spec.Folder, textureFolder);
            string definitionPath = $"{contentFolder}/{spec.Folder}_Content.asset";
            EnemyContentDefinition definition = ScriptableObject.CreateInstance<EnemyContentDefinition>();
            AssetDatabase.CreateAsset(definition, definitionPath);
            SerializedObject serialized = new(definition);
            serialized.FindProperty("archetype").enumValueIndex = (int)spec.Archetype;
            serialized.FindProperty("category").enumValueIndex = (int)GetCategory(spec);
            serialized.FindProperty("prefab").objectReferenceValue = prefab;
            serialized.FindProperty("data").objectReferenceValue = data;
            serialized.FindProperty("atlas").objectReferenceValue = atlas;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static SpriteAtlas BuildAtlas(string enemyName, string textureFolder)
        {
            string atlasFolder = $"{SourceRoot}/{enemyName}/Atlas";
            EnsureFolder(atlasFolder);
            string atlasPath = $"{atlasFolder}/{enemyName}.spriteatlas";

            Texture2D[] textures = Directory.GetFiles(textureFolder, "*.png")
                .Select(AssetDatabase.LoadAssetAtPath<Texture2D>)
                .Where(texture => texture != null)
                .OrderBy(texture => NaturalIndex(texture.name))
                .ToArray();
            if (textures.Length == 0) throw new InvalidOperationException($"No atlas textures found for {enemyName}.");

            SpriteAtlas atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(atlasPath);
            if (atlas == null)
            {
                atlas = new SpriteAtlas { name = enemyName };
                AssetDatabase.CreateAsset(atlas, atlasPath);
            }
            else
            {
                UnityEngine.Object[] previousPackables = SpriteAtlasExtensions.GetPackables(atlas);
                if (previousPackables.Length > 0) SpriteAtlasExtensions.Remove(atlas, previousPackables);
            }
            SpriteAtlasExtensions.Add(atlas, textures.Cast<UnityEngine.Object>().ToArray());

            SpriteAtlasPackingSettings packing = SpriteAtlasExtensions.GetPackingSettings(atlas);
            packing.enableRotation = false;
            packing.enableTightPacking = false;
            packing.enableAlphaDilation = true;
            packing.padding = 4;
            SpriteAtlasExtensions.SetPackingSettings(atlas, packing);

            SpriteAtlasTextureSettings textureSettings = SpriteAtlasExtensions.GetTextureSettings(atlas);
            textureSettings.filterMode = FilterMode.Point;
            textureSettings.generateMipMaps = false;
            textureSettings.readable = false;
            textureSettings.sRGB = true;
            SpriteAtlasExtensions.SetTextureSettings(atlas, textureSettings);
            SpriteAtlasExtensions.SetPlatformSettings(atlas, new TextureImporterPlatformSettings
            {
                name = "Android",
                overridden = true,
                maxTextureSize = 2048,
                format = TextureImporterFormat.ASTC_6x6,
                compressionQuality = 50
            });
            SpriteAtlasExtensions.SetIncludeInBuild(atlas, false);
            EditorUtility.SetDirty(atlas);
            return atlas;
        }

        private static Dictionary<EnemyContentCategory, AddressableAssetGroup> CreateAddressableGroups(
            AddressableAssetSettings settings)
        {
            return new Dictionary<EnemyContentCategory, AddressableAssetGroup>
            {
                [EnemyContentCategory.Minion] = RecreateAddressableGroup(settings, MinionGroup),
                [EnemyContentCategory.Elite] = RecreateAddressableGroup(settings, EliteGroup),
                [EnemyContentCategory.Boss] = RecreateAddressableGroup(settings, BossGroup)
            };
        }

        private static AddressableAssetGroup RecreateAddressableGroup(AddressableAssetSettings settings, string name)
        {
            AddressableAssetGroup existing = settings.FindGroup(name);
            if (existing != null) settings.RemoveGroup(existing);
            AddressableAssetGroup group = settings.CreateGroup(name, false, false, true, null,
                typeof(ContentUpdateGroupSchema), typeof(BundledAssetGroupSchema));
            BundledAssetGroupSchema bundled = group.GetSchema<BundledAssetGroupSchema>();
            bundled.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackSeparately;
            bundled.Compression = BundledAssetGroupSchema.BundleCompressionMode.LZ4;
            bundled.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kLocalBuildPath);
            bundled.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kLocalLoadPath);
            EditorUtility.SetDirty(group);
            return group;
        }

        private static void ConfigureAddressableEntry(AddressableAssetSettings settings, AddressableAssetGroup group,
            EnemyContentDefinition definition, string enemyName)
        {
            string path = AssetDatabase.GetAssetPath(definition);
            AddressableAssetEntry entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path), group);
            entry.address = $"Enemy/{enemyName}/Content";
            SetLabel(settings, entry, AllContentLabel);
            SetLabel(settings, entry, GetCategoryLabel(definition.Category));
            SetLabel(settings, entry, "Enemy_" + enemyName);
        }

        private static void SetLabel(AddressableAssetSettings settings, AddressableAssetEntry entry, string label)
        {
            settings.AddLabel(label, false);
            entry.SetLabel(label, true, true, false);
        }

        private static EnemyContentCategory GetCategory(Spec spec)
        {
            return spec.Boss ? EnemyContentCategory.Boss :
                spec.Elite ? EnemyContentCategory.Elite : EnemyContentCategory.Minion;
        }

        private static string GetCategoryLabel(EnemyContentCategory category)
        {
            return category switch
            {
                EnemyContentCategory.Minion => "EnemyContent_Minions",
                EnemyContentCategory.Elite => "EnemyContent_Elites",
                EnemyContentCategory.Boss => "EnemyContent_Bosses",
                _ => throw new ArgumentOutOfRangeException(nameof(category), category, null)
            };
        }

        private static AnimatorController BuildController(Spec spec, EnemyData data, Sprite[] frames, string folder)
        {
            string path = $"{folder}/{spec.Folder}.controller";
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(path) != null) AssetDatabase.DeleteAsset(path);
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            AnimationClip idle = BuildClip($"{folder}/Idle.anim", "Idle", frames, 0, 0, 0.5f, true);
            AnimationClip move = BuildClip($"{folder}/Move.anim", "Move", frames, spec.Move.start, spec.Move.end, spec.Move.duration, true);
            AnimationClip skillA = BuildClip($"{folder}/SkillA.anim", "SkillA", frames, spec.SkillA.start, spec.SkillA.end, spec.SkillA.duration, false);
            AnimationClip skillB = BuildClip($"{folder}/SkillB.anim", "SkillB", frames, spec.SkillB.start, spec.SkillB.end, spec.SkillB.duration, false);
            AnimationClip special = BuildClip($"{folder}/Special.anim", "Special", frames, spec.Special.start, spec.Special.end, spec.Special.duration, false);
            AnimationClip revive = BuildClip($"{folder}/Revive.anim", "Revive", frames, spec.Special.end, spec.Special.start, spec.Special.duration, false);
            int deathFrame = data.deathFrameIndex >= 0 ? data.deathFrameIndex : frames.Length - 1;
            int deathStartFrame = data.deathAnimationStartFrame >= 0
                ? data.deathAnimationStartFrame : Mathf.Max(0, deathFrame - 5);
            AnimationClip death = BuildClip($"{folder}/Death.anim", "Death", frames,
                deathStartFrame, deathFrame, Mathf.Max(0.2f, data.deathAnimationDuration), false);
            AnimatorState idleState = machine.AddState("Idle"); idleState.motion = idle;
            AnimatorState moveState = machine.AddState("Move"); moveState.motion = move;
            AddState(machine, "SkillA", skillA); AddState(machine, "SkillB", skillB); AddState(machine, "Special", special); AddState(machine, "Revive", revive); AddState(machine, "Death", death);
            if (spec.Archetype == EnemyArchetype.LittleSatan)
            {
                AddState(machine, "PhaseTwoIdle", BuildClip($"{folder}/PhaseTwoIdle.anim", "PhaseTwoIdle",
                    frames, 1, 1, 0.2f, true));
                AddState(machine, "PhaseTwoDashPrepare", BuildClip($"{folder}/PhaseTwoDashPrepare.anim",
                    "PhaseTwoDashPrepare", frames, 16, 18, 0.5f, false));
                AddState(machine, "PhaseTwoDashLoop", BuildClip($"{folder}/PhaseTwoDashLoop.anim",
                    "PhaseTwoDashLoop", frames, 19, 21, 0.3f, true));
                AddState(machine, "PhaseTwoDashEnd", BuildClip($"{folder}/PhaseTwoDashEnd.anim",
                    "PhaseTwoDashEnd", frames, 22, 24, 0.5f, false));
            }
            if (spec.Archetype == EnemyArchetype.Gloomy)
            {
                AddState(machine, "GloomyDashPrepare", BuildClip($"{folder}/GloomyDashPrepare.anim",
                    "GloomyDashPrepare", frames, 1, 2, 0.4f, false));
                AddState(machine, "GloomyDashLoop", BuildClip($"{folder}/GloomyDashLoop.anim",
                    "GloomyDashLoop", frames, 3, 8, 0.4f, true));
            }
            machine.defaultState = idleState;
            AnimatorStateTransition toMove = idleState.AddTransition(moveState); toMove.hasExitTime = false; toMove.AddCondition(AnimatorConditionMode.If, 0, "IsMoving");
            AnimatorStateTransition toIdle = moveState.AddTransition(idleState); toIdle.hasExitTime = false; toIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsMoving");
            foreach (AnimatorState state in machine.states.Select(s => s.state))
            {
                if (state == idleState || state == moveState || state.name == "Death" ||
                    state.name == "PhaseTwoIdle" || state.name == "PhaseTwoDashLoop" ||
                    state.name == "GloomyDashLoop") continue;
                AnimatorStateTransition transition = state.AddTransition(idleState);
                transition.hasExitTime = true; transition.exitTime = 0.98f; transition.duration = 0.05f;
            }
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void AddState(AnimatorStateMachine machine, string name, AnimationClip clip)
        {
            AnimatorState state = machine.AddState(name); state.motion = clip;
        }

        private static AnimationClip BuildClip(string path, string name, Sprite[] frames, int start, int end, float duration, bool loop)
        {
            if (AssetDatabase.LoadAssetAtPath<AnimationClip>(path) != null) AssetDatabase.DeleteAsset(path);
            start = Mathf.Clamp(start, 0, frames.Length - 1);
            end = Mathf.Clamp(end, 0, frames.Length - 1);
            List<Sprite> clipFrames = new();
            if (start <= end) for (int i = start; i <= end; i++) clipFrames.Add(frames[i]);
            else for (int i = start; i >= end; i--) clipFrames.Add(frames[i]);
            AnimationClip clip = new() { name = name, frameRate = Mathf.Max(1f, clipFrames.Count / Mathf.Max(0.05f, duration)) };
            ObjectReferenceKeyframe[] keys = new ObjectReferenceKeyframe[clipFrames.Count + 1];
            for (int i = 0; i < clipFrames.Count; i++) keys[i] = new ObjectReferenceKeyframe { time = i / clip.frameRate, value = clipFrames[i] };
            keys[^1] = new ObjectReferenceKeyframe { time = clipFrames.Count / clip.frameRate, value = clipFrames[^1] };
            AnimationUtility.SetObjectReferenceCurve(clip, new EditorCurveBinding { path = "", type = typeof(SpriteRenderer), propertyName = "m_Sprite" }, keys);
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = loop; AnimationUtility.SetAnimationClipSettings(clip, settings);
            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }

        private static Material CreateWhiteMaterialIfNeeded(Spec spec, SpriteRenderer renderer)
        {
            if (spec.Archetype != EnemyArchetype.White && spec.Archetype != EnemyArchetype.DoubleWhite) return renderer.sharedMaterial;
            Shader shader = Shader.Find("DevouringBeast/WhiteGradientMap");
            if (shader == null) return renderer.sharedMaterial;
            string path = $"{GeneratedRoot}/{spec.Folder}/{spec.Folder}_Gradient.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) AssetDatabase.DeleteAsset(path);
            Material material = new(shader) { name = spec.Folder + "Gradient" };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void BuildProjectilePrefab(string name, string spritePath, bool fireball)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            GameObject root = new(name, typeof(SpriteRenderer), typeof(CircleCollider2D), typeof(EnemyProjectile));
            root.GetComponent<SpriteRenderer>().sprite = sprite;
            root.GetComponent<SpriteRenderer>().sortingOrder = 5;
            root.GetComponent<CircleCollider2D>().isTrigger = true;
            root.GetComponent<CircleCollider2D>().radius = fireball ? 0.4f : 0.2f;
            string path = $"Assets/Resources/Enemy/{name}.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) AssetDatabase.DeleteAsset(path);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
        }

        private static void BuildOvaryPrefab()
        {
            GameObject root = new("SpiderOvary", typeof(SpriteRenderer), typeof(CircleCollider2D), typeof(SpiderOvary));
            CircleCollider2D collider = root.GetComponent<CircleCollider2D>();
            collider.isTrigger = false;
            collider.radius = 0.55f;
            SerializedObject serialized = new(root.GetComponent<SpiderOvary>());
            serialized.FindProperty("stages").arraySize = 3;
            for (int i = 0; i < 3; i++) serialized.FindProperty("stages").GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Art/Sprites/Summon/spider_ovary_{i + 1}.png");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            string path = "Assets/Resources/Enemy/SpiderOvary.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) AssetDatabase.DeleteAsset(path);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
        }

        private static void BuildChestSpriteCopy()
        {
            string path = "Assets/Resources/Drops/treasure_chest.png";
            if (!File.Exists(path)) AssetDatabase.CopyAsset("Assets/Art/Sprites/Drop/treasure_chest.png", path);
        }

        private static List<Spec> CreateSpecs() => new()
        {
            S(EnemyArchetype.Baby,"Baby",true,false,(0,0,.2f),(1,7,2.5f),(8,16,3.5f),(0,0,.2f)),
            S(EnemyArchetype.SkeletonMan,"SkeletonMan",true,false,(1,6,1f),(0,0,.2f),(13,18,1.5f),(7,12,1f)),
            S(EnemyArchetype.LittleSatan,"LittleSatan",true,false,(2,8,1f),(0,0,.2f),(16,24,3f),(9,15,1.5f)),
            S(EnemyArchetype.Satan,"Satan",true,false,(0,0,.2f),(1,10,3f),(5,10,3f),(1,4,1f)),
            S(EnemyArchetype.MeatMountain,"MeatMountain",true,false,(0,0,.2f),(1,5,3f),(6,15,2.5f),(6,15,2f)),
            S(EnemyArchetype.Skeleton,"Skeleton",false,true,(1,6,1f),(0,0,.2f),(0,0,.2f),(7,12,1f)),
            S(EnemyArchetype.DoubleWhite,"DoubleWhite",false,true,(1,8,1f),(0,0,.2f),(0,0,.2f),(0,0,.2f)),
            S(EnemyArchetype.GreenBubble,"GreenBubble",false,true,(1,8,1f),(9,14,1f),(15,20,.6f),(0,0,.2f)),
            S(EnemyArchetype.BigMeatballs,"BigMeatballs",false,true,(1,8,1f),(0,0,.2f),(9,19,1f),(0,0,.2f)),
            S(EnemyArchetype.HomeSpider,"HomeSpider",false,true,(1,8,1f),(0,0,.2f),(9,15,1f),(0,0,.2f)),
            S(EnemyArchetype.BigSpider,"BigSpider",false,true,(2,8,1.5f),(9,15,.6f),(0,0,.2f),(0,0,.2f)),
            S(EnemyArchetype.Gloomy,"Gloomy",false,true,(9,15,1f),(1,8,.8f),(1,8,.8f),(16,30,2f)),
            S(EnemyArchetype.Bat,"Bat",false,false,(2,7,.8f),(0,0,.2f),(0,0,.2f),(0,0,.2f)),
            S(EnemyArchetype.Fly,"Fly",false,false,(2,7,.8f),(0,0,.2f),(0,0,.2f),(0,0,.2f)),
            S(EnemyArchetype.GroundWorm,"GroundWorm",false,false,(0,0,.2f),(1,8,.5f),(1,8,.5f),(1,8,.5f)),
            S(EnemyArchetype.Meatballs,"Meatballs",false,false,(1,7,.9f),(0,0,.2f),(0,0,.2f),(0,0,.2f)),
            S(EnemyArchetype.BloodBag,"BloodBag",false,false,(1,8,1f),(0,0,.2f),(0,0,.2f),(0,0,.2f)),
            S(EnemyArchetype.Spider,"Spider",false,false,(2,8,.9f),(9,13,.5f),(14,16,.5f),(0,0,.2f)),
            S(EnemyArchetype.Mushroom,"Mushroom",false,false,(2,9,1f),(0,0,.2f),(0,0,.2f),(0,0,.2f)),
            S(EnemyArchetype.White,"White",false,false,(1,7,.9f),(0,0,.2f),(0,0,.2f),(0,0,.2f))
        };

        private static Spec S(EnemyArchetype archetype, string folder, bool boss, bool elite,
            (int, int, float) move, (int, int, float) skillA, (int, int, float) skillB, (int, int, float) special) => new()
            {
                Archetype = archetype,
                Folder = folder,
                Boss = boss,
                Elite = elite,
                Move = move,
                SkillA = skillA,
                SkillB = skillB,
                Special = special
            };

        private static int NaturalIndex(string name)
        {
            string digits = new(name.Reverse().TakeWhile(char.IsDigit).Reverse().ToArray());
            return int.TryParse(digits, out int value) ? value : 0;
        }

        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/'); string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static void DeleteGeneratedAsset(string path)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) != null || AssetDatabase.IsValidFolder(path))
                AssetDatabase.DeleteAsset(path);
        }
    }
}
#endif
