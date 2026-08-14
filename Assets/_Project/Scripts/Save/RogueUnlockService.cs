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
        private const string LittleSatanDefeatedKey = "rogue.progress.littleSatanDefeated";
        private const string SatanDefeatedKey = "rogue.progress.satanDefeated";

        public static bool IsUnlocked(RogueSkillId id)
        {
            if (GameManager.Existing != null && GameManager.Existing.IsTestMode)
            {
                if (id == RogueSkillId.FaithDemon || id == RogueSkillId.FaithAngel ||
                    id == RogueSkillId.FaithPope || id == RogueSkillId.FaithWitch)
                    return true;
            }
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
            if (GameManager.Existing != null && GameManager.Existing.IsTestMode) return;
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

        public static void RecordEnemyDefeated(EnemyArchetype archetype)
        {
            if (GameManager.Existing != null && GameManager.Existing.IsTestMode) return;
            if (archetype == EnemyArchetype.LittleSatan)
                PlayerPrefs.SetInt(LittleSatanDefeatedKey, 1);
            else if (archetype == EnemyArchetype.Satan)
                PlayerPrefs.SetInt(SatanDefeatedKey, 1);
            else
                return;
            PlayerPrefs.Save();
            if (PlayerPrefs.GetInt(LittleSatanDefeatedKey, 0) != 0 &&
                PlayerPrefs.GetInt(SatanDefeatedKey, 0) != 0)
                Unlock(RogueSkillId.FaithDemon);
        }

        public static void ResetForTesting()
        {
            PlayerPrefs.DeleteKey(DemonKey);
            PlayerPrefs.DeleteKey(AngelKey);
            PlayerPrefs.DeleteKey(PopeKey);
            PlayerPrefs.DeleteKey(WitchKey);
            PlayerPrefs.DeleteKey(LittleSatanDefeatedKey);
            PlayerPrefs.DeleteKey(SatanDefeatedKey);
            PlayerPrefs.Save();
        }
    }
}
