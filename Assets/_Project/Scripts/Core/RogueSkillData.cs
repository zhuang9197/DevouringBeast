using UnityEngine;

namespace DevouringBeast
{
    /// <summary>旧版技能资源兼容类型。新系统使用 RogueSkillCatalog，保留此类以维持已有资源 GUID。</summary>
    [CreateAssetMenu(menuName = "DevouringBeast/Legacy Skill Data", fileName = "SkillData")]
    public sealed class RogueSkillData : ScriptableObject
    {
        public string skillName;
        public ItemTag tag;
        [TextArea(2, 5)] public string description;
        public int maxLevel = 5;
        [HideInInspector] public int currentLevel;
        public RogueSkillData prerequisite;
        public float[] levelValues;
    }
}
