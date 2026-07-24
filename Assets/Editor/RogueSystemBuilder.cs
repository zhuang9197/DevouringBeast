#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DevouringBeast.Editor
{
    public static class RogueSystemBuilder
    {
        private const string CatalogPath = "Assets/Resources/Rogue/RogueSkillCatalog.asset";

        [MenuItem("Tools/Devouring Beast/Build Rogue System")]
        public static void Build()
        {
            EnsureFolder("Assets/Resources/Rogue");
            RogueSkillCatalog catalog = AssetDatabase.LoadAssetAtPath<RogueSkillCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<RogueSkillCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            catalog.skills = CreateDefinitions();
            catalog.rogueSelectionBackground = LoadSprite("Assets/Art/Sprites/UI/rogue_select.png");
            catalog.buttonBackground = LoadSprite("Assets/Art/Sprites/UI/border.png");
            catalog.poisoningIcon = LoadSprite("Assets/Art/Sprites/Rogue/status/poisoning.png");
            catalog.burnIcon = LoadSprite("Assets/Art/Sprites/Rogue/status/burn.png");
            catalog.slowdownIcon = LoadSprite("Assets/Art/Sprites/Rogue/status/slowdown.png");
            catalog.dizzinessIcon = LoadSprite("Assets/Art/Sprites/Rogue/status/dizziness.png");
            catalog.erosionIcon = LoadSprite("Assets/Art/Sprites/Rogue/status/erosion.png");
            catalog.skillIcons = AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Sprites/Rogue/Skills/skills.png")
                .OfType<Sprite>().OrderBy(sprite => sprite.name).ToList();
            catalog.beastFront = LoadSprite("Assets/Art/Sprites/Player/beast_front.png");
            catalog.beastBack = LoadSprite("Assets/Art/Sprites/Player/beast_back.png");
            catalog.beastSide = LoadSprite("Assets/Art/Sprites/Player/beast_side.png");
            catalog.beastFrontRoll = LoadSprites("Assets/Art/Sprites/Player/Atlas/beast_front_roll.png");
            catalog.beastBackRoll = LoadSprites("Assets/Art/Sprites/Player/Atlas/beast_back_roll.png");
            catalog.beastSideRoll = LoadSprites("Assets/Art/Sprites/Player/Atlas/beast_side_roll.png");
            catalog.progressBar = LoadSubSprite("Assets/Art/Sprites/UI/UI_Fixed.png", "progress_bar");
            catalog.progressFill = LoadSubSprite("Assets/Art/Sprites/UI/UI_Fixed.png", "progress_fill");
            catalog.joystick = LoadSubSprite("Assets/Art/Sprites/UI/UI_Fixed.png", "joystick");
            catalog.suckButton = LoadSubSprite("Assets/Art/Sprites/UI/UI_Fixed.png", "suck");
            catalog.spitButton = LoadSubSprite("Assets/Art/Sprites/UI/UI_Fixed.png", "spit");
            catalog.swallowButton = LoadSubSprite("Assets/Art/Sprites/UI/UI_Fixed.png", "swallow");
            catalog.healthBar = LoadSprite("Assets/Art/Sprites/UI/health_bar.png");
            catalog.healthFill = LoadSprite("Assets/Art/Sprites/UI/health_fill.png");
            EditorUtility.SetDirty(catalog);

            UnityEngine.SceneManagement.Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");
            RogueSkillManager manager = UnityEngine.Object.FindFirstObjectByType<RogueSkillManager>();
            if (manager != null)
            {
                SerializedObject serialized = new(manager);
                serialized.FindProperty("catalog").objectReferenceValue = catalog;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(manager);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            BuildSkillCardPrefab(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[RogueSystemBuilder] Built {catalog.skills.Count} skills and assigned GameScene catalog.");
        }

        private static List<RogueSkillDefinition> CreateDefinitions() => new()
        {
            D(RogueSkillId.NormalFast,RogueSchool.Normal,"跑快快","提升 1% 移动速度；每级额外提升 1%。","normal_fast",0),
            D(RogueSkillId.NormalSuction,RogueSchool.Normal,"大喇叭","提升 1% 吸力和 1% 吸力伤害；每级额外提升 1%。","normal_suction",0),
            D(RogueSkillId.NormalPower,RogueSchool.Normal,"吐吐吐","提升 1% 吐出的能量球伤害；每级额外提升 1%。","normal_power",0),
            D(RogueSkillId.PoisonDeadly,RogueSchool.Poison,"致命毒素","命中增加一层中毒，每层每秒造成 5 点伤害，持续 5 秒；每级每层伤害 +1。","poison_deadly",5),
            D(RogueSkillId.PoisonNumb,RogueSchool.Poison,"神经毒素","命中有 30% 概率眩晕 1 秒；每级概率 +10%。","poison_numb",5),
            D(RogueSkillId.PoisonErode,RogueSchool.Poison,"侵蚀毒素","命中增加侵蚀；3 层后再次命中引爆，造成三倍伤害和已损生命值伤害。","poison_erode",5),
            D(RogueSkillId.PoisonWarp,RogueSchool.Poison,"蜘蛛病毒","命中减速 10%；每级减速效果 +5%。","poison_warp",5),
            D(RogueSkillId.PoisonLegacy,RogueSchool.Poison,"原子危机-毒","分裂的小能量球也能附带毒系肉鸽效果。","poison_legacy",1,true,true,RogueSkillId.PoisonDeadly,RogueSkillId.SuperSplitMore),
            D(RogueSkillId.FirePyroblast,RogueSchool.Fire,"炎爆球","命中伤害提升至 120%，并造成 30% 范围爆炸伤害。","fire_pyroblast",1),
            D(RogueSkillId.FirePyroblastFlame,RogueSchool.Fire,"炎爆球-炎","每级提升 10% 炎爆球爆炸伤害。","fire_pyroblast_flame",5,false,false,RogueSkillId.FirePyroblast),
            D(RogueSkillId.FirePyroblastScope,RogueSchool.Fire,"炎爆球-爆","每级提升 10% 炎爆球爆炸范围。","fire_pyroblast_scope",5,false,false,RogueSkillId.FirePyroblast),
            D(RogueSkillId.FireBottle,RogueSchool.Fire,"燃烧瓶","命中灼烧敌人；重复命中提升 10% 灼烧伤害但不延长时间。","fire_bottle",5),
            D(RogueSkillId.FireLegacy,RogueSchool.Fire,"原子危机-火","分裂的小能量球也能附带火系肉鸽效果。","fire_legacy",1,true,true,RogueSkillId.FireBottle,RogueSkillId.SuperSplitMore),
            D(RogueSkillId.EvolutionWing,RogueSchool.Evolution,"翅膀","吸入时可以移动，但不能转向。","evo_wing",1),
            D(RogueSkillId.EvolutionBigMouth,RogueSchool.Evolution,"大嘴","提升 3 秒吸入时间；之后每级提升 2 秒。","evo_bigmouth",3),
            D(RogueSkillId.EvolutionCharged,RogueSchool.Evolution,"尖嘴","吐出可以蓄力，首级额外 10% 伤害，每级再提升 10%。","evo_charged",3),
            D(RogueSkillId.EvolutionMoreMouth,RogueSchool.Evolution,"多嘴","一次吐出两颗能量球，每颗造成 60% 伤害并附带肉鸽效果。","evo_moremouth",1),
            D(RogueSkillId.EvolutionMoreMouthMore,RogueSchool.Evolution,"多嘴-多","每级额外增加 1 颗能量球。","evo_moremouth_more",2,false,false,RogueSkillId.EvolutionMoreMouth),
            D(RogueSkillId.EvolutionMoreMouthPower,RogueSchool.Evolution,"多嘴-强","每级提升 10% 多嘴能量球伤害。","evo_moremouth_power",5,false,false,RogueSkillId.EvolutionMoreMouth),
            D(RogueSkillId.SuperSplit,RogueSchool.Superpower,"分裂","命中分裂为两个造成 30% 伤害的小能量球，默认不附带肉鸽效果。","super_split",1),
            D(RogueSkillId.SuperSplitMore,RogueSchool.Superpower,"分裂-裂变","每级额外增加 1 颗分裂小能量球。","super_split_more",3,false,false,RogueSkillId.SuperSplit),
            D(RogueSkillId.SuperPiece,RogueSchool.Superpower,"穿透","可穿透敌人；每次穿透伤害降低 20%，最多降低 60%。升级降低衰减。","super_piece",4),
            D(RogueSkillId.FaithAngel,RogueSchool.Faith,"天使","无消耗连续吐出；击杀升级不受种类池限制；排除吸入类技能。","faith_angel",0,false,true),
            D(RogueSkillId.FaithDemon,RogueSchool.Faith,"恶魔","吸力伤害大幅提高但不能吸入物体；击杀升级不受种类池限制；排除吐出类技能。","faith_demon",0,false,true),
            D(RogueSkillId.FaithPope,RogueSchool.Faith,"教皇","吞噬变为教化，同时吐出附带肉鸽效果且强化的能量球。","faith_pope",0,false,true),
            D(RogueSkillId.FaithWitch,RogueSchool.Faith,"女巫","吞噬累积通灵进度，叠满后变身野兽吞吞滚动攻击；变身减伤 20%，升级提升减伤与滚动伤害。","faith_witch",0,false,true)
        };

        private static RogueSkillDefinition D(RogueSkillId id, RogueSchool school, string name, string description,
            string icon, int maxLevel, bool requireMax = false, bool mythic = false, params RogueSkillId[] prerequisites)
        {
            return new RogueSkillDefinition { id=id, school=school, displayName=name, description=description,
                iconName=icon, maxLevel=maxLevel, prerequisites=prerequisites ?? Array.Empty<RogueSkillId>(),
                requiresMaxPrerequisites=requireMax, mythic=mythic };
        }

        private static Sprite LoadSprite(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);
        private static Sprite LoadSubSprite(string path, string name) => AssetDatabase.LoadAllAssetsAtPath(path)
            .OfType<Sprite>().FirstOrDefault(sprite => sprite.name == name);
        private static Sprite[] LoadSprites(string path) => AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
            .OrderBy(sprite => NaturalIndex(sprite.name)).ToArray();
        private static int NaturalIndex(string name)
        {
            string digits = new(name.Reverse().TakeWhile(char.IsDigit).Reverse().ToArray());
            return int.TryParse(digits, out int value) ? value : 0;
        }
        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/'); string current = parts[0];
            for (int i=1;i<parts.Length;i++) { string next=current+"/"+parts[i]; if(!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current,parts[i]); current=next; }
        }

        private static void BuildSkillCardPrefab(RogueSkillCatalog catalog)
        {
            EnsureFolder("Assets/_Project/Prefabs/UI");
            GameObject card = new("RogueSkillCard", typeof(RectTransform), typeof(UnityEngine.UI.Image),
                typeof(UnityEngine.UI.Button), typeof(UIButtonAudio));
            RectTransform rect = card.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(360f, 560f);
            UnityEngine.UI.Image background = card.GetComponent<UnityEngine.UI.Image>();
            background.sprite = catalog.buttonBackground;
            background.type = UnityEngine.UI.Image.Type.Sliced;
            background.color = new Color(1f, 0.96f, 0.82f, 0.98f);
            CreatePrefabImage("Icon", card.transform, new Vector2(0.2f,0.57f), new Vector2(0.8f,0.95f));
            CreatePrefabText("Name", card.transform, new Vector2(0.06f,0.43f), new Vector2(0.94f,0.58f), 30);
            CreatePrefabText("Level", card.transform, new Vector2(0.08f,0.32f), new Vector2(0.92f,0.44f), 25);
            CreatePrefabText("Description", card.transform, new Vector2(0.08f,0.06f), new Vector2(0.92f,0.32f), 21);
            PrefabUtility.SaveAsPrefabAsset(card, "Assets/_Project/Prefabs/UI/RogueSkillCard.prefab");
            UnityEngine.Object.DestroyImmediate(card);
        }

        private static void CreatePrefabImage(string name, Transform parent, Vector2 min, Vector2 max)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(UnityEngine.UI.Image));
            go.transform.SetParent(parent, false); SetAnchors(go.GetComponent<RectTransform>(), min, max);
            go.GetComponent<UnityEngine.UI.Image>().preserveAspect = true;
        }

        private static void CreatePrefabText(string name, Transform parent, Vector2 min, Vector2 max, int size)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(UnityEngine.UI.Text));
            go.transform.SetParent(parent, false); SetAnchors(go.GetComponent<RectTransform>(), min, max);
            UnityEngine.UI.Text text = go.GetComponent<UnityEngine.UI.Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize = size;
            text.alignment = UnityEngine.TextAnchor.MiddleCenter; text.raycastTarget = false;
        }

        private static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin=min; rect.anchorMax=max; rect.offsetMin=rect.offsetMax=Vector2.zero;
        }
    }
}
#endif
