using UnityEngine;

namespace DevouringBeast
{
    /// <summary>
    /// EnemySpawner — 简化为生成点容器
    /// 实际生成逻辑由 WaveManager 管理
    /// </summary>
    public class EnemySpawner : MonoBehaviour
    {
        [Header("生成点")]
        [SerializeField] private Transform[] spawnPoints;

        [Header("敌人物体父节点")]
        [SerializeField] private Transform enemiesParent;

        public Transform[] SpawnPoints => spawnPoints;
        public Transform EnemiesParent => enemiesParent != null ? enemiesParent : transform;
    }
}
