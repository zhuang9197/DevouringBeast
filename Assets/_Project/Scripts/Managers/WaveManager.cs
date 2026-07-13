using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DevouringBeast
{
    /// <summary>
    /// WaveManager — 波次管理器
    /// 管理波次状态机：倒计时 → 生成 → 战斗 → 清敌 → 下一波
    /// </summary>
    public class WaveManager : MonoBehaviour
    {
        public enum Phase { Interlude, Spawning, Fighting }

        [Header("配置")]
        [SerializeField] private WaveConfig config;

        [Header("生成")]
        [SerializeField] private EnemySpawner spawner;
        [SerializeField] private Transform[] spawnPoints;

        [Header("事件")]
        [SerializeField] private VoidEventChannel onWaveStart;
        [SerializeField] private VoidEventChannel onWaveCleared;

        [Header("UI 更新（供 WaveUI 订阅）")]
        public System.Action<int, int, float, float> OnWaveInfoChanged; // (wave, enemiesRemaining, timer, maxTimer)

        private int _currentWave;
        private int _enemiesRemaining;
        private float _waveTimer;
        private float _maxTimer;
        private Phase _currentPhase = Phase.Interlude;
        private bool _allSpawned;
        private bool _resetTriggered;

        // 预制体缓存（按等级分组）
        private List<GameObject[]> _tierPrefabs = new();
        private GameObject[] _elitePrefabs;
        private GameObject[] _bossPrefabs;

        private void Start()
        {
            LoadPrefabs();
            _currentWave = 0;
            _waveTimer = 3f; // 首波3秒后开始
            _maxTimer = 3f;
            _currentPhase = Phase.Interlude;
        }

        private void Update()
        {
            if (!GameManager.Instance.IsPlaying) return;

            switch (_currentPhase)
            {
                case Phase.Interlude:
                    _waveTimer -= Time.deltaTime;
                    NotifyUI();
                    if (_waveTimer <= 0f)
                    {
                        StartNextWave();
                    }
                    break;

                case Phase.Fighting:
                    _waveTimer -= Time.deltaTime;
                    NotifyUI();

                    // 清敌重置：所有敌人都被消灭且倒计时>3s时，重置为3s
                    if (_allSpawned && _enemiesRemaining <= 0 && !_resetTriggered && _waveTimer > config.clearResetTimer)
                    {
                        _waveTimer = config.clearResetTimer;
                        _resetTriggered = true;
                        _maxTimer = config.clearResetTimer;
                    }

                    // 倒计时耗尽：直接进入下一波（场上残留敌人继续存在）
                    if (_waveTimer <= 0f)
                    {
                        EndWave();
                    }
                    break;
            }
        }

        private void StartNextWave()
        {
            _currentWave++;
            _enemiesRemaining = 0;
            _allSpawned = false;
            _resetTriggered = false;
            _currentPhase = Phase.Spawning;

            int tier = config.GetTier(_currentWave);
            int count = config.GetEnemyCount(_currentWave);
            float healthMul = config.GetHealthMultiplier(_currentWave);
            float damageMul = config.GetDamageMultiplier(_currentWave);
            float speedMul = config.GetSpeedMultiplier(_currentWave);

            // 生成普通怪
            var tierPrefabs = GetTierPrefabs(tier);
            for (int i = 0; i < count; i++)
            {
                SpawnEnemy(tierPrefabs, healthMul, damageMul, speedMul, EnemyType.Normal);
            }

            // 每5波生成精英
            if (_currentWave % 5 == 0 && _elitePrefabs != null && _elitePrefabs.Length > 0)
            {
                int eliteCount = config.elitePer5Waves;
                for (int i = 0; i < eliteCount; i++)
                {
                    SpawnEnemy(_elitePrefabs,
                        healthMul * config.eliteHealthMul,
                        damageMul * config.eliteDamageMul,
                        speedMul * config.eliteSpeedMul,
                        EnemyType.Elite);
                }
            }

            // 每10波生成Boss
            if (_currentWave % 10 == 0 && _bossPrefabs != null && _bossPrefabs.Length > 0)
            {
                int bossCount = config.bossPer10Waves;
                for (int i = 0; i < bossCount; i++)
                {
                    SpawnEnemy(_bossPrefabs,
                        healthMul * config.bossHealthMul,
                        damageMul * config.bossDamageMul,
                        speedMul * config.bossSpeedMul,
                        EnemyType.Boss);
                }
            }

            _allSpawned = true;
            _currentPhase = Phase.Fighting;
            _waveTimer = config.GetWaveTimer(_currentWave);
            _maxTimer = _waveTimer;

            onWaveStart?.RaiseEvent();
            Debug.Log($"[WaveManager] Wave {_currentWave} started — {count} enemies, tier={tier}");
        }

        private void EndWave()
        {
            _currentPhase = Phase.Interlude;
            _waveTimer = 3f;
            _maxTimer = 3f;
            _resetTriggered = false;
            onWaveCleared?.RaiseEvent();
            Debug.Log($"[WaveManager] Wave {_currentWave} cleared!");
        }

        private void SpawnEnemy(GameObject[] prefabs, float healthMul, float damageMul, float speedMul, EnemyType type)
        {
            if (prefabs == null || prefabs.Length == 0) return;
            if (spawnPoints == null || spawnPoints.Length == 0) return;

            var prefab = prefabs[Random.Range(0, prefabs.Length)];
            var point = spawnPoints[Random.Range(0, spawnPoints.Length)];

            var enemy = Instantiate(prefab, point.position, Quaternion.identity);

            // 确保 EnemyBase 存在
            var enemyBase = enemy.GetComponent<EnemyBase>();
            if (enemyBase == null)
                enemyBase = enemy.AddComponent<EnemyBase>();

            // 创建临时 EnemyData
            var data = ScriptableObject.CreateInstance<EnemyData>();
            data.enemyType = type;
            data.maxHealth = 100f * healthMul;
            data.attackDamage = 10f * damageMul;
            data.moveSpeed = 3f * speedMul;
            data.attackRange = 1.5f;
            data.attackCooldown = 1.5f;
            data.detectRange = 10f;
            data.tag = ItemTag.None;
            data.killMass = 20f;
            data.deadMass = 5f;
            data.aliveInhaleThreshold = 50f;
            data.deadInhaleThreshold = 10f;

            // 从预制体获取 AnimatorController
            var anim = enemy.GetComponentInChildren<Animator>();
            if (anim != null && anim.runtimeAnimatorController != null)
                data.animatorController = anim.runtimeAnimatorController;

            enemyBase.Initialize(data);
            enemyBase.OnDeath += OnEnemyKilled;

            // 动态创建头顶血条
            CreateHealthBar(enemy, enemyBase);

            _enemiesRemaining++;
        }

        private void CreateHealthBar(GameObject enemy, EnemyBase enemyBase)
        {
            EnemyHealthBar.EnsureFor(enemyBase);
        }

        private void OnEnemyKilled(EnemyBase enemy)
        {
            enemy.OnDeath -= OnEnemyKilled;
            _enemiesRemaining = Mathf.Max(0, _enemiesRemaining - 1);
        }

        private GameObject[] GetTierPrefabs(int tier)
        {
            int idx = Mathf.Min(tier - 1, _tierPrefabs.Count - 1);
            if (idx < 0 || idx >= _tierPrefabs.Count) return null;
            return _tierPrefabs[idx];
        }

        private void LoadPrefabs()
        {
            // 按等级加载预制体 (tier 1: 1-10, tier 2: 11-20, ..., tier 7: 61-80)
            for (int tier = 1; tier <= 7; tier++)
            {
                var list = new List<GameObject>();
                int start = (tier - 1) * 10 + 1;
                int end = tier * 10;
                for (int i = start; i <= end; i++)
                {
                    var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                        "Assets/Art/Enemy/Prefabs/Character (" + i + ").prefab");
                    if (prefab != null) list.Add(prefab);
                }
                _tierPrefabs.Add(list.ToArray());
            }

            // 精英怪 (81-90)
            var eliteList = new List<GameObject>();
            for (int i = 81; i <= 90; i++)
            {
                var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Art/Enemy/Prefabs/Character (" + i + ").prefab");
                if (prefab != null) eliteList.Add(prefab);
            }
            _elitePrefabs = eliteList.ToArray();

            // Boss (91-100)
            var bossList = new List<GameObject>();
            for (int i = 91; i <= 100; i++)
            {
                var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Art/Enemy/Prefabs/Character (" + i + ").prefab");
                if (prefab != null) bossList.Add(prefab);
            }
            _bossPrefabs = bossList.ToArray();
        }

        private void NotifyUI()
        {
            OnWaveInfoChanged?.Invoke(_currentWave, _enemiesRemaining, Mathf.Max(0, _waveTimer), _maxTimer);
        }

        public int CurrentWave => _currentWave;
        public int EnemiesRemaining => _enemiesRemaining;
        public float Timer => Mathf.Max(0, _waveTimer);
        public float MaxTimer => _maxTimer;
    }
}
