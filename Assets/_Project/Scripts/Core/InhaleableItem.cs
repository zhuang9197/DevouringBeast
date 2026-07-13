using UnityEngine;

namespace DevouringBeast
{
    /// <summary>
    /// 可吸入物品 — 挂载在可吸入物体上的组件
    /// </summary>
    public class InhaleableItem : MonoBehaviour
    {
        [field: SerializeField] public ItemTag Tag { get; set; } = ItemTag.None;
        [field: SerializeField] public float Mass { get; set; } = 10f;
        [field: SerializeField] public float AliveInhaleThreshold { get; set; } = 50f;
        [field: SerializeField] public float DeadInhaleThreshold { get; set; } = 10f;

        /// <summary>是否存活（影响吸入阈值）</summary>
        public bool IsAlive { get; set; } = true;

        /// <summary>当前生效的吸入阈值</summary>
        public float CurrentThreshold => IsAlive ? AliveInhaleThreshold : DeadInhaleThreshold;

        /// <summary>被吸入时调用</summary>
        public void OnInhaled()
        {
            // 通知 EnemyBase 死亡（触发 OnDeath 事件，让 WaveManager 计数减少）
            var enemy = GetComponent<EnemyBase>();
            if (enemy != null && !enemy.IsDead)
            {
                enemy.TakeDamage(float.MaxValue);
            }
            gameObject.SetActive(false);
        }

        /// <summary>被吐出时调用</summary>
        public void OnSpitOut(Vector2 position)
        {
            transform.position = position;
            gameObject.SetActive(true);
        }
    }
}
