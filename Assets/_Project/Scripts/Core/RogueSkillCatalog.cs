using System;
using System.Collections.Generic;
using UnityEngine;

namespace DevouringBeast
{
    public enum RogueSchool { Normal, Poison, Fire, Evolution, Superpower, Faith }

    public enum RogueSkillId
    {
        NormalFast, NormalSuction, NormalPower,
        PoisonDeadly, PoisonNumb, PoisonErode, PoisonWarp, PoisonLegacy,
        FirePyroblast, FirePyroblastFlame, FirePyroblastScope, FireBottle, FireLegacy,
        EvolutionWing, EvolutionBigMouth, EvolutionCharged, EvolutionMoreMouth,
        EvolutionMoreMouthMore, EvolutionMoreMouthPower,
        SuperSplit, SuperSplitMore, SuperPiece,
        FaithAngel, FaithDemon, FaithPope, FaithWitch,
        Chef, HotDogLover, SushiMaster,
        DemonFear, DemonContempt, DemonKing,
        PopeBelief, PopePray, PopeBaptism,
        WitchClaw, WitchDeterrence, WitchRoar
    }

    [Serializable]
    public sealed class RogueSkillDefinition
    {
        public RogueSkillId id;
        public RogueSchool school;
        public string displayName;
        [TextArea(2, 5)] public string description;
        public string iconName;
        [Tooltip("0 表示无上限")] public int maxLevel;
        public RogueSkillId[] prerequisites = Array.Empty<RogueSkillId>();
        public bool requiresMaxPrerequisites;
        public bool mythic;
        [Range(0f, 1f)] public float appearanceProbability = 1f;
        public bool IsMaxLevel(int level) => maxLevel > 0 && level >= maxLevel;
    }

    [CreateAssetMenu(menuName = "DevouringBeast/Rogue Skill Catalog", fileName = "RogueSkillCatalog")]
    public sealed class RogueSkillCatalog : ScriptableObject
    {
        public List<RogueSkillDefinition> skills = new();
        public Sprite rogueSelectionBackground;
        public Sprite buttonBackground;
        public Sprite poisoningIcon, burnIcon, slowdownIcon, dizzinessIcon, erosionIcon;
        public List<Sprite> skillIcons = new();
        public Sprite beastFront, beastBack, beastSide;
        public Sprite[] beastFrontRoll = Array.Empty<Sprite>();
        public Sprite[] beastBackRoll = Array.Empty<Sprite>();
        public Sprite[] beastSideRoll = Array.Empty<Sprite>();
        public Sprite believerFront, believerSide, believerBack;
        public Sprite darkBelieverFront, darkBelieverSide, darkBelieverBack;
        [Header("Gameplay UI")]
        public Sprite progressBar, progressFill, joystick, suckButton, spitButton, swallowButton;
        public Sprite healthBar, healthFill;
        public Sprite healthFull, healthHalf, healthEmpty;

        private Dictionary<RogueSkillId, RogueSkillDefinition> _byId;
        private Dictionary<string, Sprite> _iconsByName;

        public RogueSkillDefinition Get(RogueSkillId id)
        {
            EnsureCache(); _byId.TryGetValue(id, out RogueSkillDefinition value); return value;
        }
        public Sprite GetIcon(string iconName)
        {
            EnsureCache(); _iconsByName.TryGetValue(iconName ?? string.Empty, out Sprite sprite); return sprite;
        }
        private void EnsureCache()
        {
            if (_byId == null)
            {
                _byId = new(); foreach (RogueSkillDefinition skill in skills) if (skill != null) _byId[skill.id] = skill;
            }
            if (_iconsByName == null)
            {
                _iconsByName = new(StringComparer.OrdinalIgnoreCase);
                foreach (Sprite icon in skillIcons) if (icon != null) _iconsByName[icon.name] = icon;
            }
        }
    }
}
