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
        public float elapsedSeconds;
        public int healthSpent;
        public List<RogueSkillSaveEntry> rogueSkills = new List<RogueSkillSaveEntry>();
    }

    [Serializable]
    public sealed class CompletedRunData
    {
        public int slotIndex;
        public string displayName;
        public long completedTicks;
        public float clearTimeSeconds;
        public int healthSpent;
    }

    public static class SaveGameService
    {
        public const int InitialSlotCount = 3;
        public const int MaximumSlotCount = 5;
        private const string SlotPrefix = "save.slot.";
        private const string ActiveSlotKey = "save.activeSlot";
        private const string HistoryKey = "save.history";
        private static double _sessionStartedAt = -1d;

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
            if (slotIndex < 0 || slotIndex >= MaximumSlotCount) return null;
            string json = PlayerPrefs.GetString(SlotPrefix + slotIndex, string.Empty);
            return string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<SaveSlotData>(json);
        }

        public static SaveSlotData[] GetAllSlots()
        {
            SaveSlotData[] result = new SaveSlotData[GetVisibleSlotCount()];
            for (int i = 0; i < result.Length; i++) result[i] = GetSlot(i);
            return result;
        }

        public static int GetVisibleSlotCount()
        {
            int count = InitialSlotCount;
            while (count < MaximumSlotCount)
            {
                bool full = true;
                for (int i = 0; i < count; i++)
                    if (GetSlot(i) == null) { full = false; break; }
                if (!full) break;
                count++;
            }
            return count;
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
            CommitElapsedTime(data);
            data.updatedTicks = DateTime.UtcNow.Ticks;
            Write(data);
        }

        public static void SaveRogueSkills(List<RogueSkillSaveEntry> skills)
        {
            SaveSlotData data = GetActiveSlot();
            if (data == null) return;
            data.rogueSkills = skills ?? new List<RogueSkillSaveEntry>();
            CommitElapsedTime(data);
            data.updatedTicks = DateTime.UtcNow.Ticks;
            Write(data);
        }

        public static void ResetActiveRun()
        {
            SaveSlotData data = GetActiveSlot();
            if (data == null) return;
            long now = DateTime.UtcNow.Ticks;
            data.completedWave = 0;
            data.elapsedSeconds = 0f;
            data.healthSpent = 0;
            data.rogueSkills = new List<RogueSkillSaveEntry>();
            data.createdTicks = now;
            data.updatedTicks = now;
            if (_sessionStartedAt >= 0d) _sessionStartedAt = Time.realtimeSinceStartupAsDouble;
            Write(data);
        }

        public static void RecordHealthSpent(int amount)
        {
            if (amount <= 0) return;
            SaveSlotData data = GetActiveSlot();
            if (data == null) return;
            data.healthSpent += amount;
            CommitElapsedTime(data);
            data.updatedTicks = DateTime.UtcNow.Ticks;
            Write(data);
        }

        public static float GetElapsedSeconds(SaveSlotData data)
        {
            if (data == null) return 0f;
            float elapsed = Mathf.Max(0f, data.elapsedSeconds);
            if (_sessionStartedAt >= 0d && data.slotIndex == GetActiveSlotIndex())
                elapsed += Mathf.Max(0f, (float)(Time.realtimeSinceStartupAsDouble - _sessionStartedAt));
            return elapsed;
        }

        public static void BeginRunSession()
        {
            if (_sessionStartedAt < 0d && GetActiveSlot() != null)
                _sessionStartedAt = Time.realtimeSinceStartupAsDouble;
        }

        public static void EndRunSession()
        {
            if (_sessionStartedAt < 0d) return;
            SaveSlotData data = GetActiveSlot();
            if (data != null)
            {
                CommitElapsedTime(data, false);
                data.updatedTicks = DateTime.UtcNow.Ticks;
                Write(data);
            }
            _sessionStartedAt = -1d;
        }

        public static IReadOnlyList<CompletedRunData> GetHistory()
        {
            string json = PlayerPrefs.GetString(HistoryKey, string.Empty);
            if (string.IsNullOrEmpty(json)) return Array.Empty<CompletedRunData>();
            HistoryContainer container = JsonUtility.FromJson<HistoryContainer>(json);
            return container?.runs ?? new List<CompletedRunData>();
        }

        public static CompletedRunData CompleteActiveRun()
        {
            SaveSlotData data = GetActiveSlot();
            if (data == null) return null;
            CommitElapsedTime(data, false);
            data.updatedTicks = DateTime.UtcNow.Ticks;
            CompletedRunData history = new CompletedRunData
            {
                slotIndex = data.slotIndex,
                displayName = data.displayName,
                completedTicks = data.updatedTicks,
                clearTimeSeconds = data.elapsedSeconds,
                healthSpent = data.healthSpent
            };
            List<CompletedRunData> runs = new(GetHistory());
            runs.Add(history);
            PlayerPrefs.SetString(HistoryKey, JsonUtility.ToJson(new HistoryContainer { runs = runs }));
            PlayerPrefs.DeleteKey(SlotPrefix + data.slotIndex);
            PlayerPrefs.SetInt(ActiveSlotKey, -1);
            PlayerPrefs.Save();
            _sessionStartedAt = -1d;
            return history;
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

        private static void CommitElapsedTime(SaveSlotData data, bool continueSession = true)
        {
            if (data == null || _sessionStartedAt < 0d || data.slotIndex != GetActiveSlotIndex()) return;
            double now = Time.realtimeSinceStartupAsDouble;
            data.elapsedSeconds += Mathf.Max(0f, (float)(now - _sessionStartedAt));
            _sessionStartedAt = continueSession ? now : -1d;
        }
    }

    [Serializable]
    internal sealed class HistoryContainer
    {
        public List<CompletedRunData> runs = new();
    }
}
