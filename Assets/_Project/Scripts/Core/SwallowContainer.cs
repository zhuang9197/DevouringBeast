using System;
using System.Collections.Generic;
using UnityEngine;

namespace DevouringBeast
{
    /// <summary>
    /// 吞噬容器 — MonoBehaviour 组件，记录口中物品、质量、标签占比
    /// </summary>
    public class SwallowContainer : MonoBehaviour
    {
        [field: SerializeField] public float RequiredMass { get; set; } = 100f;
        [field: SerializeField] public float CurrentMass { get; set; } = 0f;

        /// <summary>各标签累积质量</summary>
        [field: SerializeField] public SerializableTagDict TagMasses { get; set; } = new SerializableTagDict();

        /// <summary>当前口内物品列表</summary>
        public List<InhaleableItem> Items { get; private set; } = new List<InhaleableItem>();

        public bool HasItems => Items.Count > 0;

        /// <summary>升级检查事件（质量变化时触发）</summary>
        public event Action OnLevelUpCheck;

        public void AddItem(InhaleableItem item)
        {
            Items.Add(item);
            CurrentMass += item.Mass;
            TagMasses.Add(item.Tag, item.Mass);
            OnLevelUpCheck?.Invoke();
        }

        public List<InhaleableItem> ClearItems()
        {
            var items = new List<InhaleableItem>(Items);
            Items.Clear();
            return items;
        }

        public void Consume()
        {
            Items.Clear();
        }

        /// <summary>通知外部进行升级检查</summary>
        public void CheckAndNotify()
        {
            OnLevelUpCheck?.Invoke();
        }

        public void ResetForLevelUp()
        {
            CurrentMass = 0f;
            TagMasses.Clear();
            RequiredMass *= 1.5f;
        }

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
        [SerializeField] private List<ItemTag> _keys = new List<ItemTag>();
        [SerializeField] private List<float> _values = new List<float>();

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
            ItemTag best = ItemTag.None;
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
