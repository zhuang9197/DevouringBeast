using System;
using System.Collections.Generic;
using UnityEngine;

namespace DevouringBeast
{
    public static class SceneNames
    {
        public const string Load = "LoadScene";
        public const string Menu = "MenuScene";
        public const string Game = "GameScene";
    }

    [Serializable]
    public sealed class RogueSkillSaveEntry
    {
        public string id;
        public int level;
    }

    [Serializable]
    public sealed class SaveSlotData
    {
        public int slotIndex;
        public string displayName;
        public int completedWave;
        public long createdTicks;
        public long updatedTicks;
        public List<RogueSkillSaveEntry> rogueSkills = new List<RogueSkillSaveEntry>();
    }

    public static class SaveGameService
    {
        public const int SlotCount = 3;
        private const string SlotPrefix = "save.slot.";
        private const string ActiveSlotKey = "save.activeSlot";

        public static void Initialize()
        {
            if (!PlayerPrefs.HasKey(ActiveSlotKey))
            {
                PlayerPrefs.SetInt(ActiveSlotKey, -1);
                PlayerPrefs.Save();
            }
        }

        public static SaveSlotData GetSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= SlotCount) return null;
            string json = PlayerPrefs.GetString(SlotPrefix + slotIndex, string.Empty);
            return string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<SaveSlotData>(json);
        }

        public static SaveSlotData[] GetAllSlots()
        {
            SaveSlotData[] result = new SaveSlotData[SlotCount];
            for (int i = 0; i < SlotCount; i++) result[i] = GetSlot(i);
            return result;
        }

        public static SaveSlotData CreateNewGame(int slotIndex)
        {
            long now = DateTime.UtcNow.Ticks;
            SaveSlotData data = new SaveSlotData
            {
                slotIndex = slotIndex,
                displayName = "存档 " + (slotIndex + 1),
                completedWave = 0,
                createdTicks = now,
                updatedTicks = now
            };
            Write(data);
            SetActiveSlot(slotIndex);
            return data;
        }

        public static void SaveCompletedWave(int wave)
        {
            SaveSlotData data = GetActiveSlot();
            if (data == null) return;
            data.completedWave = Mathf.Max(data.completedWave, wave);
            data.updatedTicks = DateTime.UtcNow.Ticks;
            Write(data);
        }

        public static void SaveRogueSkills(List<RogueSkillSaveEntry> skills)
        {
            SaveSlotData data = GetActiveSlot();
            if (data == null) return;
            data.rogueSkills = skills ?? new List<RogueSkillSaveEntry>();
            data.updatedTicks = DateTime.UtcNow.Ticks;
            Write(data);
        }

        public static void ResetActiveRun()
        {
            SaveSlotData data = GetActiveSlot();
            if (data == null) return;
            data.completedWave = 0;
            data.rogueSkills = new List<RogueSkillSaveEntry>();
            data.updatedTicks = DateTime.UtcNow.Ticks;
            Write(data);
        }

        public static void DeleteSlot(int slotIndex)
        {
            PlayerPrefs.DeleteKey(SlotPrefix + slotIndex);
            if (GetActiveSlotIndex() == slotIndex) PlayerPrefs.SetInt(ActiveSlotKey, -1);
            PlayerPrefs.Save();
        }

        public static void SetActiveSlot(int slotIndex)
        {
            PlayerPrefs.SetInt(ActiveSlotKey, slotIndex);
            PlayerPrefs.Save();
        }

        public static int GetActiveSlotIndex() { return PlayerPrefs.GetInt(ActiveSlotKey, -1); }
        public static SaveSlotData GetActiveSlot() { return GetSlot(GetActiveSlotIndex()); }

        private static void Write(SaveSlotData data)
        {
            PlayerPrefs.SetString(SlotPrefix + data.slotIndex, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }
    }
}
