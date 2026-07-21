using System;
using System.Collections.Generic;
using UnityEngine;

namespace DevouringBeast
{
    /// <summary>
    /// 吞噬容器 — MonoBehaviour 组件
    /// 口中物品（暂存） + 已吞噬累积（质量/标签占比，仅 Consume 时才累积）
    /// </summary>
    public class SwallowContainer : MonoBehaviour
    {
        private PlayerHealth _playerHealth;
        private PlayerInhale _playerInhale;
        [Header("升级进度")]
        [SerializeField, Min(1f)] private float levelOneRequirement = 30f;
        [SerializeField, Min(1f)] private float levelTwoRequirement = 50f;
        [SerializeField, Min(1f)] private float levelThreeRequirement = 75f;
        [SerializeField, Min(1.01f)] private float laterLevelGrowthMultiplier = 1.35f;
        [field: SerializeField] public float RequiredMass { get; set; } = 30f;
        [field: SerializeField] public float CurrentMass { get; set; } = 0f;
        [field: SerializeField] public int CurrentLevel { get; private set; } = 1;

        /// <summary>各标签累积质量（仅吞噬后累积）</summary>
        [field: SerializeField] public SerializableTagDict TagMasses { get; set; } = new SerializableTagDict();

        /// <summary>当前口内物品列表（吸入后暂存，吞噬或吐出时清空）</summary>
        public List<InhaleableItem> Items { get; private set; } = new List<InhaleableItem>();

        /// <summary>口内物品的标签占比（仅用于膨胀显示，不影响颜色）</summary>
        public SerializableTagDict MouthTagMasses { get; private set; } = new SerializableTagDict();

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
        /// 吸入物品 — 仅暂存到口中，不累积质量/标签
        /// </summary>
        public void AddItem(InhaleableItem item)
        {
            Items.Add(item);
            MouthTagMasses.Add(item.Tag, item.Mass);
        }

        /// <summary>
        /// 吐出 — 清空口中物品，不累积质量/标签
        /// </summary>
        public List<InhaleableItem> ClearItems()
        {
            var items = new List<InhaleableItem>(Items);
            Items.Clear();
            MouthTagMasses.Clear();
            return items;
        }

        /// <summary>
        /// 吞噬 — 将口中物品的质量和标签累积到 CurrentMass/TagMasses
        /// 触发升级检查和颜色变化
        /// </summary>
        public void Consume()
        {
            if (!CanConsume) return;
            foreach (var item in Items)
            {
                if (item == null) continue;
                CurrentMass += item.Mass;
                TagMasses.Add(item.Tag, item.Mass);
                BloodDrop bloodDrop = item.GetComponent<BloodDrop>();
                if (bloodDrop != null && _playerHealth != null)
                    _playerHealth.Heal(bloodDrop.HealAmount);
                item.ReleaseFromMouth();
            }
            Items.Clear();
            MouthTagMasses.Clear();
            OnLevelUpCheck?.Invoke();
            NotifyProgress();
        }

        /// <summary>通知外部进行升级检查</summary>
        public void CheckAndNotify()
        {
            OnLevelUpCheck?.Invoke();
            NotifyProgress();
        }

        public void AddProgress(float amount, ItemTag tag = ItemTag.Normal)
        {
            if (amount <= 0f) return;
            CurrentMass += amount;
            TagMasses.Add(tag, amount);
            OnLevelUpCheck?.Invoke();
            NotifyProgress();
        }

        public void ResetForLevelUp()
        {
            float massBeforeLevelUp = CurrentMass;
            CurrentMass = Mathf.Max(0f, CurrentMass - RequiredMass);
            if (CurrentMass <= 0f || massBeforeLevelUp <= 0f)
                TagMasses.Clear();
            else
                TagMasses.Scale(CurrentMass / massBeforeLevelUp);
            CurrentLevel++;
            RequiredMass = GetRequiredMassForLevel(CurrentLevel);
            NotifyProgress();
        }

        public float GetRequiredMassForLevel(int level)
        {
            level = Mathf.Max(1, level);
            if (level == 1) return Mathf.Max(1f, levelOneRequirement);
            if (level == 2) return Mathf.Max(levelOneRequirement + 1f, levelTwoRequirement);
            if (level == 3) return Mathf.Max(levelTwoRequirement + 1f, levelThreeRequirement);

            float required = Mathf.Max(levelTwoRequirement + 1f, levelThreeRequirement);
            float growth = Mathf.Max(1.01f, laterLevelGrowthMultiplier);
            for (int currentLevel = 4; currentLevel <= level; currentLevel++)
                required = Mathf.Ceil(required * growth);
            return required;
        }

        private void NotifyProgress() => OnProgressChanged?.Invoke(CurrentMass, RequiredMass, CurrentLevel);

        public bool CanLevelUp => CurrentMass >= RequiredMass;

        public ItemTag GetDominantTag()
        {
            return TagMasses.GetDominantTag();
        }
    }

    /// <summary>
    /// 可序列化的标签-质量字典
    /// </summary>
    [Serializable]
    public class SerializableTagDict
    {
        [SerializeField] private List<ItemTag> _keys = new();
        [SerializeField] private List<float> _values = new();

        private Dictionary<ItemTag, float> _dict;

        private void EnsureDict()
        {
            if (_dict == null)
            {
                _dict = new Dictionary<ItemTag, float>();
                for (int i = 0; i < Mathf.Min(_keys.Count, _values.Count); i++)
                    _dict[_keys[i]] = _values[i];
            }
        }

        public void Add(ItemTag tag, float mass)
        {
            EnsureDict();
            _dict.TryGetValue(tag, out float current);
            _dict[tag] = current + mass;
            SyncLists();
        }

        public float GetRatio(ItemTag tag)
        {
            EnsureDict();
            float total = 0f;
            foreach (var v in _dict.Values) total += v;
            if (total <= 0f) return 0f;
            _dict.TryGetValue(tag, out float m);
            return m / total;
        }

        public ItemTag GetDominantTag()
        {
            EnsureDict();
            ItemTag best = ItemTag.Normal;
            float bestMass = 0f;
            foreach (var kv in _dict)
            {
                if (kv.Value > bestMass)
                {
                    bestMass = kv.Value;
                    best = kv.Key;
                }
            }
            return best;
        }

        public void Clear()
        {
            _dict?.Clear();
            _keys.Clear();
            _values.Clear();
        }

        public void Scale(float multiplier)
        {
            EnsureDict();
            multiplier = Mathf.Max(0f, multiplier);
            if (multiplier <= 0f)
            {
                Clear();
                return;
            }

            var keys = new List<ItemTag>(_dict.Keys);
            foreach (ItemTag key in keys)
                _dict[key] *= multiplier;
            SyncLists();
        }

        private void SyncLists()
        {
            _keys.Clear();
            _values.Clear();
            if (_dict == null) return;
            foreach (var kv in _dict)
            {
                _keys.Add(kv.Key);
                _values.Add(kv.Value);
            }
        }
    }
}
