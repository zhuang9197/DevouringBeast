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
        public static WaveManager Instance { get; private set; }
        public enum Phase { Interlude, Spawning, Fighting }

        [Header("配置")]
        
        [SerializeField] private EnemyPrefabCatalog prefabCatalog;

[SerializeField] private WaveConfig config;

        [Header("生成")]
        [SerializeField] private EnemySpawner spawner;
        [SerializeField] private Transform[] spawnPoints;
        [SerializeField, Min(16)] private int maxPooledEnemies = 160;
        [SerializeField, Min(0.1f), Tooltip("极小敌人自动放大后的最小视觉宽高（取较大边）")]
        private float minimumEnemyVisualSize = 1.25f;

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
        private readonly Dictionary<GameObject, Queue<EnemyPoolMember>> _enemyPools = new();
        private readonly Dictionary<GameObject, Vector3> _enemySpawnScales = new();
        private readonly HashSet<EnemyPoolMember> _activeEnemies = new();
        private Transform _enemyPoolRoot;
        private int _pooledEnemyCount;

        private void Awake()
        {
            Instance = this;
        }

private void Start()
        {
            GameObject poolRoot = new GameObject("EnemyPool");
            poolRoot.transform.SetParent(transform, false);
            _enemyPoolRoot = poolRoot.transform;
            LoadPrefabs();
            SaveGameService.Initialize();
            SaveSlotData activeSave = SaveGameService.GetActiveSlot();
            _currentWave = activeSave != null ? Mathf.Max(0, activeSave.completedWave) : 0;
            _waveTimer = 3f;
            _maxTimer = 3f;
            _currentPhase = Phase.Interlude;
            AudioManager.Instance.PlayBgm(BgmTrack.Battle);
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
            int nextWave = _currentWave + 1;
            EmpowerSurvivingEnemies(nextWave);
            _currentWave = nextWave;
            AudioManager.Instance.SetBattleWave(_currentWave);
            _enemiesRemaining = CountLivingActiveEnemies();
            _allSpawned = false;
            _resetTriggered = false;
            _currentPhase = Phase.Spawning;

            int tier = config.GetTier(_currentWave);
            int count = config.GetEnemyCount(_currentWave);
            float healthMul = config.GetHealthMultiplier(_currentWave);
            float speedMul = config.GetSpeedMultiplier(_currentWave);

            // 生成普通怪
            var tierPrefabs = GetTierPrefabs(tier);
            for (int i = 0; i < count; i++)
            {
                SpawnEnemy(tierPrefabs, healthMul, config.GetAttackDamage(_currentWave, EnemyType.Normal), speedMul, EnemyType.Normal);
            }

            // 每5波生成精英
            if (_currentWave % 5 == 0 && _elitePrefabs != null && _elitePrefabs.Length > 0)
            {
                int eliteCount = config.elitePer5Waves;
                for (int i = 0; i < eliteCount; i++)
                {
                    SpawnEnemy(_elitePrefabs,
                        healthMul * config.eliteHealthMul,
                        config.GetAttackDamage(_currentWave, EnemyType.Elite),
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
                        config.GetAttackDamage(_currentWave, EnemyType.Boss),
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
            
            SaveGameService.SaveCompletedWave(_currentWave);
onWaveCleared?.RaiseEvent();
            Debug.Log($"[WaveManager] Wave {_currentWave} cleared!");
        }

        private void SpawnEnemy(GameObject[] prefabs, float healthMul, int attackDamage, float speedMul, EnemyType type)
        {
            if (prefabs == null || prefabs.Length == 0) return;
            if (spawnPoints == null || spawnPoints.Length == 0) return;

            var prefab = prefabs[Random.Range(0, prefabs.Length)];
            var point = spawnPoints[Random.Range(0, spawnPoints.Length)];

            EnemyPoolMember poolMember = AcquireEnemy(prefab, point.position);
            var enemy = poolMember.gameObject;

            // 确保 EnemyBase 存在
            var enemyBase = enemy.GetComponent<EnemyBase>();
            if (enemyBase == null)
                enemyBase = enemy.AddComponent<EnemyBase>();
            poolMember.Bind(prefab, ReleaseEnemy);

            // 创建临时 EnemyData
            var data = enemyBase.GetOrCreateRuntimeData();
            data.enemyType = type;
            data.maxHealth = 100f * healthMul;
            data.attackDamage = Mathf.Max(1, attackDamage);
            data.moveSpeed = 3f * speedMul;
            data.attackRange = 1.5f;
            data.attackCooldown = 0.9f;
            data.detectRange = 10f;

            // 从预制体的 InhaleableItem 读取 Tag 和 Mass，不覆盖
            var prefabItem = prefab.GetComponent<InhaleableItem>();
            if (prefabItem != null)
            {
                data.tag = prefabItem.Tag;
                data.killMass = prefabItem.Mass;
                data.deadMass = prefabItem.Mass * 0.3f; // 阵亡质量约为存活时的30%
            }
            else
            {
                data.tag = ItemTag.Normal;
                data.killMass = 20f;
                data.deadMass = 5f;
            }
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

        private EnemyPoolMember AcquireEnemy(GameObject prefab, Vector3 position)
        {
            if (!_enemyPools.TryGetValue(prefab, out Queue<EnemyPoolMember> queue))
            {
                queue = new Queue<EnemyPoolMember>();
                _enemyPools.Add(prefab, queue);
            }

            EnemyPoolMember member = null;
            while (queue.Count > 0 && member == null)
            {
                member = queue.Dequeue();
                _pooledEnemyCount = Mathf.Max(0, _pooledEnemyCount - 1);
            }
            if (member == null)
            {
                GameObject instance = Instantiate(prefab);
                member = instance.GetComponent<EnemyPoolMember>();
                if (member == null) member = instance.AddComponent<EnemyPoolMember>();
            }

            member.Bind(prefab, ReleaseEnemy);
            member.SetSpawnScale(GetEnemySpawnScale(prefab));
            member.MarkSpawned();
            Transform parent = spawner != null ? spawner.EnemiesParent : transform;
            member.transform.SetParent(parent, false);
            member.transform.SetPositionAndRotation(position, Quaternion.identity);
            member.gameObject.SetActive(true);
            _activeEnemies.Add(member);
            return member;
        }

        private void ReleaseEnemy(EnemyPoolMember member)
        {
            if (member == null) return;
            EnemyBase enemy = member.Enemy;
            if (enemy != null) enemy.OnDeath -= OnEnemyKilled;
            _activeEnemies.Remove(member);
            member.RestoreSpawnScale();
            member.transform.SetParent(_enemyPoolRoot, false);
            member.gameObject.SetActive(false);

            if (_pooledEnemyCount >= maxPooledEnemies || member.SourcePrefab == null)
            {
                Destroy(member.gameObject);
                return;
            }
            if (!_enemyPools.TryGetValue(member.SourcePrefab, out Queue<EnemyPoolMember> queue))
            {
                queue = new Queue<EnemyPoolMember>();
                _enemyPools.Add(member.SourcePrefab, queue);
            }
            queue.Enqueue(member);
            _pooledEnemyCount++;
        }

        private Vector3 GetEnemySpawnScale(GameObject prefab)
        {
            if (prefab == null) return Vector3.one;
            if (_enemySpawnScales.TryGetValue(prefab, out Vector3 cachedScale))
                return cachedScale;

            Vector3 baseScale = prefab.transform.localScale;
            Bounds visualBounds = default;
            bool hasBounds = false;
            SpriteRenderer[] renderers = prefab.GetComponentsInChildren<SpriteRenderer>(true);
            foreach (SpriteRenderer renderer in renderers)
            {
                if (renderer == null || renderer.sprite == null) continue;
                Bounds spriteBounds = renderer.sprite.bounds;
                Vector3[] corners =
                {
                    new(spriteBounds.min.x, spriteBounds.min.y, 0f),
                    new(spriteBounds.min.x, spriteBounds.max.y, 0f),
                    new(spriteBounds.max.x, spriteBounds.min.y, 0f),
                    new(spriteBounds.max.x, spriteBounds.max.y, 0f)
                };
                foreach (Vector3 corner in corners)
                {
                    Vector3 rootLocal = prefab.transform.InverseTransformPoint(renderer.transform.TransformPoint(corner));
                    if (!hasBounds)
                    {
                        visualBounds = new Bounds(rootLocal, Vector3.zero);
                        hasBounds = true;
                    }
                    else visualBounds.Encapsulate(rootLocal);
                }
            }

            float visualSize = hasBounds
                ? Mathf.Max(visualBounds.size.x * Mathf.Abs(baseScale.x), visualBounds.size.y * Mathf.Abs(baseScale.y))
                : minimumEnemyVisualSize;
            float multiplier = visualSize > 0.001f && visualSize < minimumEnemyVisualSize
                ? minimumEnemyVisualSize / visualSize
                : 1f;
            Vector3 spawnScale = new(baseScale.x * multiplier, baseScale.y * multiplier, baseScale.z);
            _enemySpawnScales[prefab] = spawnScale;
            return spawnScale;
        }

        private void ReleaseAllActiveEnemies()
        {
            if (_activeEnemies.Count == 0) return;
            List<EnemyPoolMember> snapshot = new List<EnemyPoolMember>(_activeEnemies);
            for (int i = 0; i < snapshot.Count; i++)
            {
                EnemyPoolMember member = snapshot[i];
                if (member == null) continue;
                InhaleableItem item = member.GetComponent<InhaleableItem>();
                if (item != null && item.IsStoredInMouth) continue;
                member.Release();
            }
        }

        private void EmpowerSurvivingEnemies(int nextWave)
        {
            foreach (EnemyPoolMember member in _activeEnemies)
            {
                if (member == null || member.IsReleased || member.Enemy == null || member.Enemy.IsDead) continue;
                member.Enemy.EmpowerForNextWave(config, nextWave);
            }
        }

        private int CountLivingActiveEnemies()
        {
            int count = 0;
            foreach (EnemyPoolMember member in _activeEnemies)
                if (member != null && !member.IsReleased && member.Enemy != null && !member.Enemy.IsDead) count++;
            return count;
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
            if (prefabCatalog == null)
                prefabCatalog = Resources.Load<EnemyPrefabCatalog>("System/EnemyPrefabCatalog");

            _tierPrefabs.Clear();
            if (prefabCatalog == null)
            {
                Debug.LogError("[WaveManager] EnemyPrefabCatalog is missing.");
                for (int i = 0; i < 7; i++) _tierPrefabs.Add(System.Array.Empty<GameObject>());
                _elitePrefabs = System.Array.Empty<GameObject>();
                _bossPrefabs = System.Array.Empty<GameObject>();
                return;
            }

            for (int tier = 1; tier <= 7; tier++)
                _tierPrefabs.Add(prefabCatalog.GetTier(tier) ?? System.Array.Empty<GameObject>());

            _elitePrefabs = prefabCatalog.elitePrefabs ?? System.Array.Empty<GameObject>();
            _bossPrefabs = prefabCatalog.bossPrefabs ?? System.Array.Empty<GameObject>();
        }

        private void NotifyUI()
        {
            OnWaveInfoChanged?.Invoke(_currentWave, _enemiesRemaining, Mathf.Max(0, _waveTimer), _maxTimer);
        }

        public int CurrentWave => _currentWave;
        public int EnemiesRemaining => _enemiesRemaining;
        public float Timer => Mathf.Max(0, _waveTimer);
        public float MaxTimer => _maxTimer;
        public int ActiveEnemyObjectCount => _activeEnemies.Count;

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
        public int PooledEnemyObjectCount => _pooledEnemyCount;
    }
}
