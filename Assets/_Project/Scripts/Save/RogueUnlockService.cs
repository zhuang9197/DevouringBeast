using UnityEngine;

namespace DevouringBeast
{
    /// <summary>永久保存肉鸽高级流派解锁；本局计数由 RogueSkillManager 管理。</summary>
    public static class RogueUnlockService
    {
        private const string DemonKey = "rogue.unlock.demon";
        private const string AngelKey = "rogue.unlock.angel";
        private const string PopeKey = "rogue.unlock.pope";
        private const string WitchKey = "rogue.unlock.witch";

        public static bool IsUnlocked(RogueSkillId id)
        {
            return id switch
            {
                RogueSkillId.FaithDemon => PlayerPrefs.GetInt(DemonKey, 0) != 0,
                RogueSkillId.FaithAngel => PlayerPrefs.GetInt(AngelKey, 0) != 0,
                RogueSkillId.FaithPope => PlayerPrefs.GetInt(PopeKey, 0) != 0,
                RogueSkillId.FaithWitch => PlayerPrefs.GetInt(WitchKey, 0) != 0,
                _ => true
            };
        }

        public static void Unlock(RogueSkillId id)
        {
            string key = id switch
            {
                RogueSkillId.FaithDemon => DemonKey,
                RogueSkillId.FaithAngel => AngelKey,
                RogueSkillId.FaithPope => PopeKey,
                RogueSkillId.FaithWitch => WitchKey,
                _ => null
            };
            if (string.IsNullOrEmpty(key) || PlayerPrefs.GetInt(key, 0) != 0) return;
            PlayerPrefs.SetInt(key, 1);
            PlayerPrefs.Save();
        }

        public static void ResetForTesting()
        {
            PlayerPrefs.DeleteKey(DemonKey);
            PlayerPrefs.DeleteKey(AngelKey);
            PlayerPrefs.DeleteKey(PopeKey);
            PlayerPrefs.DeleteKey(WitchKey);
            PlayerPrefs.Save();
        }
    }
}
