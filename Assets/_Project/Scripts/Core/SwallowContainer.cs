using System;
using System.Collections.Generic;
using UnityEngine;

namespace DevouringBeast
{
    /// <summary>
    /// 吞噬容器 — MonoBehaviour 组件
    /// 口中物品（暂存） + 已吞噬升级质量（仅 Consume 时才累积）
    /// </summary>
    public class SwallowContainer : MonoBehaviour
    {
        private PlayerHealth _playerHealth;
        private PlayerInhale _playerInhale;
        [Header("升级进度")]
        [SerializeField, Min(1f)] private float initialLevelRequirement = 100f;
        [SerializeField, Min(1.01f)] private float levelGrowthMultiplier = 1.1f;
        [field: SerializeField] public float RequiredMass { get; set; } = 100f;
        [field: SerializeField] public float CurrentMass { get; set; } = 0f;
        [field: SerializeField] public int CurrentLevel { get; private set; } = 1;

        /// <summary>当前口内物品列表（吸入后暂存，吞噬或吐出时清空）</summary>
        public List<InhaleableItem> Items { get; private set; } = new List<InhaleableItem>();

        public bool HasItems => Items.Count > 0;
        public bool CanConsume => HasItems && (_playerInhale == null || !_playerInhale.IsInhaling);

        /// <summary>升级检查事件（吞噬累积后触发）</summary>
        public event Action OnLevelUpCheck;
        public event Action<float, float, int> OnProgressChanged;

        private void Awake()
        {
            _playerHealth = GetComponent<PlayerHealth>();
            _playerInhale = GetComponent<PlayerInhale>();
            RequiredMass = GetRequiredMassForLevel(CurrentLevel);
        }

        /// <summary>
        /// 吸入物品 — 仅暂存到口中，不累积升级质量
        /// </summary>
        public void AddItem(InhaleableItem item)
        {
            Items.Add(item);
        }

        /// <summary>
        /// 吐出 — 清空口中物品，不累积升级质量
        /// </summary>
        public List<InhaleableItem> ClearItems()
        {
            var items = new List<InhaleableItem>(Items);
            Items.Clear();
            return items;
        }

        /// <summary>
        /// 吞噬 — 将口中物品的质量累积到 CurrentMass
        /// </summary>
        public float Consume(bool deferLevelUpCheck = false)
        {
            if (!CanConsume) return 0f;
            float consumedMass = 0f;
            foreach (var item in Items)
            {
                if (item == null) continue;
                FoodItem food = item.GetComponent<FoodItem>();
                float reward = food != null
                    ? food.Consume(_playerHealth, GetComponent<PlayerController>(), RogueSkillManager.Active)
                    : item.Mass;
                CurrentMass += reward;
                consumedMass += reward;
                item.ReleaseFromMouth();
            }
            Items.Clear();
            if (!deferLevelUpCheck) OnLevelUpCheck?.Invoke();
            NotifyProgress();
            return consumedMass;
        }

        /// <summary>通知外部进行升级检查</summary>
        public void CheckAndNotify()
        {
            OnLevelUpCheck?.Invoke();
            NotifyProgress();
        }

        public void AddProgress(float amount)
        {
            if (amount <= 0f) return;
            CurrentMass += amount;
            OnLevelUpCheck?.Invoke();
            NotifyProgress();
        }

        public void ResetForLevelUp()
        {
            CurrentMass = Mathf.Max(0f, CurrentMass - RequiredMass);
            CurrentLevel++;
            RequiredMass = GetRequiredMassForLevel(CurrentLevel);
            NotifyProgress();
        }

        public float GetRequiredMassForLevel(int level)
        {
            level = Mathf.Max(1, level);
            if (level == 1) return 35f;
            if (level == 2) return 50f;
            if (level == 3) return 65f;
            if (level == 4) return 80f;
            if (level == 5) return 100f;
            float required = Mathf.Max(1f, initialLevelRequirement);
            float growth = Mathf.Max(1.01f, levelGrowthMultiplier);
            for (int currentLevel = 6; currentLevel <= level; currentLevel++)
                required = (float)decimal.Ceiling((decimal)required * (decimal)growth);
            return required;
        }

        private void NotifyProgress() => OnProgressChanged?.Invoke(CurrentMass, RequiredMass, CurrentLevel);

        public bool CanLevelUp => CurrentMass >= RequiredMass;

        public void ResetForTesting()
        {
            Items.Clear();
            CurrentLevel = 1;
            CurrentMass = 0f;
            RequiredMass = GetRequiredMassForLevel(CurrentLevel);
            NotifyProgress();
        }

    }
}
