using UnityEngine;

namespace DevouringBeast
{
    /// <summary>
    /// 肉鸽技能 ScriptableObject 数据
    /// </summary>
    [CreateAssetMenu(menuName = "DevouringBeast/Skill Data", fileName = "SkillData")]
    public class RogueSkillData : ScriptableObject
    {
        [Header("基础信息")]
        public string skillName;
        public ItemTag tag;
        [TextArea(2, 5)]
        public string description;

        [Header("等级")]
        public int maxLevel = 5;
        [HideInInspector] public int currentLevel = 0;

        [Header("前置条件")]
        public RogueSkillData prerequisite;

        [Header("升级参数（索引=等级-1）")]
        public float[] levelValues;

        public bool IsMaxLevel => currentLevel >= maxLevel;
        public float CurrentValue => currentLevel > 0 && levelValues != null && currentLevel <= levelValues.Length
            ? levelValues[currentLevel - 1]
            : 0f;
    }
}
