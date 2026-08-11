using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.UI;

namespace DevouringBeast
{
    public enum RoomKind { Normal, Elite, Boss }

    /// <summary>Runs one combat encounter for the currently entered room.</summary>
    public sealed class WaveManager : MonoBehaviour
    {
        private const string EnemyContentLabel = "EnemyContent_All";

        public static WaveManager Instance { get; private set; }
        public enum Phase { Idle, Spawning, Fighting, Cleared }

        [Header("配置")]
        [SerializeField] private WaveConfig config;

        [Header("生成")]
        [SerializeField, Min(16)] private int maxPooledEnemies = 160;
        [SerializeField, Min(1)] private int spawnBatchSize = 1;
        [SerializeField, Min(0.1f)] private float minimumEnemyVisualSize = 1.25f;

        [Header("事件")]
        [SerializeField] private VoidEventChannel onWaveStart;
        [SerializeField] private VoidEventChannel onWaveCleared;

        public Action<int, int, float, float> OnWaveInfoChanged;

        private readonly List<GameObject[]> _tierPrefabs = new();
        private readonly Dictionary<EnemyArchetype, EnemyContentDefinition> _contentByArchetype = new();
        private readonly Dictionary<GameObject, EnemyData> _dataByPrefab = new();
        private readonly Dictionary<string, SpriteAtlas> _loadedAtlases = new();
        private readonly Dictionary<string, List<Action<SpriteAtlas>>> _pendingAtlasRequests = new();
        private readonly List<AsyncOperationHandle<EnemyContentDefinition>> _contentHandles = new();
        private readonly Dictionary<GameObject, Queue<EnemyPoolMember>> _enemyPools = new();
        private readonly Dictionary<GameObject, Vector3> _enemySpawnScales = new();
        private readonly HashSet<EnemyPoolMember> _activeEnemies = new();
        private readonly HashSet<EnemyBase> _activeBosses = new();
        private GameObject[] _elitePrefabs = Array.Empty<GameObject>();
        private GameObject[] _bossPrefabs = Array.Empty<GameObject>();
        private Transform _enemyPoolRoot;
        private Coroutine _spawnRoutine;
        private Action _roomCleared;
        private Vector2 _roomCenter;
        private Vector2 _roomSize;
        private int _currentFloor = 1;
        private int _enemiesRemaining;
        private int _pooledEnemyCount;
        private float _roomTimer;
        private float _maxTimer;
        private float _nextCrisisEmpowerTime;
        private float _crisisTimeScale = 1f;
        private float _crisisElapsedUnscaledTime;
        private float _bossMaxHealthTotal;
        private bool _allSpawned;
        private bool _isCrisis;
        private Phase _currentPhase = Phase.Idle;
        private Image _crisisOverlay;

        public bool IsReady { get; private set; }
        public RoomKind CurrentRoomKind { get; private set; }
        public int CurrentWave => _currentFloor;
        public int CurrentFloor => _currentFloor;
        public int EnemiesRemaining => _enemiesRemaining;
        public float Timer => Mathf.Max(0f, _roomTimer);
        public float MaxTimer => _maxTimer;
        public bool IsCrisis => _isCrisis;
        public float GameplayTimeScale => _isCrisis ? _crisisTimeScale : 1f;
        public bool ShouldShowBossHealth => CurrentRoomKind == RoomKind.Boss &&
            (_currentPhase == Phase.Spawning || _currentPhase == Phase.Fighting);
        public float BossHealthPercent
        {
            get
            {
                if (_bossMaxHealthTotal <= 0f) return 1f;
                float current = 0f;
                foreach (EnemyBase boss in _activeBosses)
                    if (boss != null) current += Mathf.Max(0f, boss.CurrentHealth);
                return Mathf.Clamp01(current / _bossMaxHealthTotal);
            }
        }
        public int ActiveEnemyObjectCount => _activeEnemies.Count;
        public bool IsEncounterActive => _currentPhase == Phase.Spawning || _currentPhase == Phase.Fighting;
        public int PooledEnemyObjectCount => _pooledEnemyCount;
        public int ActiveLivingEnemyCount
        {
            get
            {
                int count = 0;
                foreach (EnemyPoolMember member in _activeEnemies)
                    if (member != null && member.Enemy != null && !member.Enemy.IsDead) count++;
                return count;
            }
        }

        public bool HasLivingArchetype(EnemyArchetype archetype)
        {
            foreach (EnemyPoolMember member in _activeEnemies)
            {
                EnemyActor actor = member != null ? member.GetComponent<EnemyActor>() : null;
                if (actor != null && !actor.GetComponent<EnemyBase>().IsDead && actor.Archetype == archetype) return true;
            }
            return false;
        }

        public bool HasOtherLivingEnemy(EnemyBase excluded, bool excludeYellow)
        {
            foreach (EnemyPoolMember member in _activeEnemies)
            {
                EnemyBase enemy = member != null ? member.Enemy : null;
                if (enemy == null || enemy == excluded || enemy.IsDead) continue;
                if (excludeYellow && enemy.GetComponent<EnemyActor>()?.IsYellowVariant == true) continue;
                return true;
            }
            return false;
        }

        private void Awake()
        {
            Instance = this;
            SpriteAtlasManager.atlasRequested += OnAtlasRequested;
        }

        private IEnumerator Start()
        {
            GameObject poolRoot = new("EnemyPool");
            poolRoot.transform.SetParent(transform, false);
            _enemyPoolRoot = poolRoot.transform;
            CreateCrisisOverlay();
            yield return LoadEnemyContent();
            IsReady = _contentByArchetype.Count > 0;
            if (!IsReady)
                Debug.LogError("[WaveManager] No Addressable enemy content was loaded. Rebuild enemy content and Addressables.");
        }

        private void Update()
        {
            if (!IsReady || !GameManager.Instance.IsPlaying || _currentPhase != Phase.Fighting) return;

            if (!_isCrisis)
            {
                _roomTimer = Mathf.Max(0f, _roomTimer - Time.deltaTime);
                if (_roomTimer <= 0f) EnterCrisis();
            }
            else
            {
                UpdateCrisisSpeed();
                if (Time.time >= _nextCrisisEmpowerTime)
                {
                    _nextCrisisEmpowerTime = Time.time + Mathf.Max(1f, config.crisisEmpowerInterval);
                    EmpowerLivingEnemies();
                }
                UpdateCrisisOverlay();
            }

            NotifyUI();
            if (_allSpawned && _enemiesRemaining <= 0) CompleteRoom();
        }

        public void BeginRoom(RoomKind roomKind, int floor, Vector2 center, Vector2 size, Action roomCleared)
        {
            StopEncounter(false);
            CurrentRoomKind = roomKind;
            _currentFloor = Mathf.Max(1, floor);
            _roomCenter = center;
            _roomSize = size;
            _roomCleared = roomCleared;
            _allSpawned = false;
            _isCrisis = false;
            _enemiesRemaining = 0;
            _activeBosses.Clear();
            _bossMaxHealthTotal = 0f;
            _roomTimer = config.GetRoomTimer(roomKind);
            _maxTimer = _roomTimer;
            SetCrisisOverlay(false);
            _currentPhase = Phase.Spawning;
            AudioManager.Instance.SetBattleWave(roomKind == RoomKind.Boss ? 10 : 1);
            _spawnRoutine = StartCoroutine(BeginRoomWhenReadyRoutine());
            NotifyUI();
        }

        private IEnumerator BeginRoomWhenReadyRoutine()
        {
            while (!IsReady) yield return null;
            yield return SpawnRoomRoutine();
        }

        public void LeaveClearedRoom()
        {
            StopEncounter(false);
            _currentPhase = Phase.Idle;
            _enemiesRemaining = 0;
            _roomTimer = 0f;
            _maxTimer = 0f;
            NotifyUI();
        }

        public bool BeginStatueEncounter(int floor, Vector2 center, Vector2 size,
            int normalCount, int eliteCount, int bossCount, Action cleared)
        {
            if (!IsReady || IsEncounterActive) return false;
            StopEncounter(false);
            _currentFloor = Mathf.Max(1, floor);
            _roomCenter = center;
            _roomSize = size;
            _roomCleared = cleared;
            _allSpawned = false;
            _isCrisis = false;
            _enemiesRemaining = 0;
            _activeBosses.Clear();
            _bossMaxHealthTotal = 0f;
            CurrentRoomKind = bossCount > 0 ? RoomKind.Boss : eliteCount > 0 ? RoomKind.Elite : RoomKind.Normal;
            _roomTimer = config.GetRoomTimer(CurrentRoomKind);
            _maxTimer = _roomTimer;
            SetCrisisOverlay(false);
            _currentPhase = Phase.Spawning;
            AudioManager.Instance.SetBattleWave(CurrentRoomKind == RoomKind.Boss ? 10 : 1);
            _spawnRoutine = StartCoroutine(SpawnStatueEncounterRoutine(
                Mathf.Max(0, normalCount), Mathf.Max(0, eliteCount), Mathf.Max(0, bossCount)));
            NotifyUI();
            return true;
        }

        private IEnumerator SpawnStatueEncounterRoutine(int normalCount, int eliteCount, int bossCount)
        {
            int tier = config.GetTier(_currentFloor);
            float healthMultiplier = config.GetHealthMultiplier(_currentFloor);
            float speedMultiplier = config.GetSpeedMultiplier(_currentFloor);
            int attackDamage = config.GetAttackDamage(_currentFloor);
            if (normalCount > 0)
                yield return SpawnGroup(GetTierPrefabs(tier), normalCount, healthMultiplier,
                    attackDamage, speedMultiplier, 5f, false, true, 0);
            if (eliteCount > 0)
                yield return SpawnGroup(_elitePrefabs, eliteCount, healthMultiplier * config.eliteHealthMul,
                    config.GetAttackDamage(_currentFloor, config.eliteDamageBonus),
                    speedMultiplier * config.eliteSpeedMul, 20f, false, false, 0);
            if (bossCount > 0)
                yield return SpawnGroup(_bossPrefabs, bossCount, healthMultiplier * config.bossHealthMul,
                    config.GetAttackDamage(_currentFloor, config.bossDamageBonus),
                    speedMultiplier * config.bossSpeedMul, 50f, true, false, 0);
            _spawnRoutine = null;
            _allSpawned = true;
            _currentPhase = Phase.Fighting;
            onWaveStart?.RaiseEvent();
            if (_enemiesRemaining <= 0) CompleteRoom();
        }

        public void ResetForFloor()
        {
            StopEncounter(true);
            _currentPhase = Phase.Idle;
            _roomCleared = null;
            SetCrisisOverlay(false);
        }

        private IEnumerator SpawnRoomRoutine()
        {
            int tier = config.GetTier(_currentFloor);
            int normalCount = Mathf.Max(1, config.GetEnemyCount(_currentFloor));
            float healthMultiplier = config.GetHealthMultiplier(_currentFloor);
            float speedMultiplier = config.GetSpeedMultiplier(_currentFloor);
            int spawnedThisFrame = 0;

            if (CurrentRoomKind == RoomKind.Normal)
            {
                yield return SpawnGroup(GetTierPrefabs(tier), normalCount, healthMultiplier,
                    config.GetAttackDamage(_currentFloor), speedMultiplier, 5f, false, true, spawnedThisFrame);
            }
            else if (CurrentRoomKind == RoomKind.Elite)
            {
                yield return SpawnGroup(GetTierPrefabs(tier), Mathf.Max(1, normalCount / 2), healthMultiplier,
                    config.GetAttackDamage(_currentFloor), speedMultiplier, 5f, false, true, spawnedThisFrame);
                yield return SpawnGroup(_elitePrefabs, Mathf.Max(1, config.elitePer5Waves),
                    healthMultiplier * config.eliteHealthMul,
                    config.GetAttackDamage(_currentFloor, config.eliteDamageBonus),
                    speedMultiplier * config.eliteSpeedMul, 20f, false, false, spawnedThisFrame);
            }
            else
            {
                yield return SpawnGroup(GetTierPrefabs(tier), Mathf.Max(1, normalCount / 2), healthMultiplier,
                    config.GetAttackDamage(_currentFloor), speedMultiplier, 5f, false, true, spawnedThisFrame);
                yield return SpawnGroup(_bossPrefabs, Mathf.Max(1, config.bossPer10Waves),
                    healthMultiplier * config.bossHealthMul,
                    config.GetAttackDamage(_currentFloor, config.bossDamageBonus),
                    speedMultiplier * config.bossSpeedMul, 50f, true, false, spawnedThisFrame);
            }

            _spawnRoutine = null;
            _allSpawned = true;
            _currentPhase = Phase.Fighting;
            onWaveStart?.RaiseEvent();
            if (_enemiesRemaining <= 0) CompleteRoom();
        }

        private IEnumerator SpawnGroup(GameObject[] prefabs, int count, float health, int damage,
            float speed, float mass, bool bossUnit, bool playSpawnSmoke, int spawnedThisFrame)
        {
            if (prefabs == null || prefabs.Length == 0) yield break;
            Vector3[] positions = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                positions[i] = bossUnit ? _roomCenter : GetRoomSpawnPosition(i);
                if (playSpawnSmoke) EnemySpawnSmokeEffect.Play(positions[i]);
            }
            if (playSpawnSmoke) yield return new WaitForSeconds(0.5f);
            for (int i = 0; i < count; i++)
            {
                while (!GameManager.Instance.IsPlaying) yield return null;
                SpawnEnemy(prefabs, health, damage, speed, mass, bossUnit, positions[i]);
                if (++spawnedThisFrame >= spawnBatchSize)
                {
                    spawnedThisFrame = 0;
                    yield return null;
                }
            }
        }

        private void SpawnEnemy(GameObject[] prefabs, float healthMul, int attackDamage,
            float speedMul, float mass, bool bossUnit, Vector3 position)
        {
            GameObject prefab = prefabs[UnityEngine.Random.Range(0, prefabs.Length)];
            EnemyPoolMember poolMember = AcquireEnemy(prefab, position);
            EnemyBase enemyBase = poolMember.GetComponent<EnemyBase>();
            if (enemyBase == null) enemyBase = poolMember.gameObject.AddComponent<EnemyBase>();
            poolMember.Bind(prefab, ReleaseEnemy);

            EnemyData template = GetEnemyData(prefab);
            EnemyData data = enemyBase.CreateScaledRuntimeData(template, healthMul, speedMul);
            data.attackDamage = Mathf.Max(data.attackDamage, attackDamage);
            data.attackCooldown = Mathf.Max(0.2f, data.attackCooldown);
            data.detectRange = Mathf.Max(data.detectRange, Mathf.Max(_roomSize.x, _roomSize.y));
            data.massValue = Mathf.Max(data.massValue, mass);

            enemyBase.Initialize(data);
            PlaceEnemy(poolMember, position);
            enemyBase.OnDeath += OnEnemyKilled;
            if (bossUnit)
            {
                _activeBosses.Add(enemyBase);
                _bossMaxHealthTotal += enemyBase.MaxHealth;
            }
            GroundShadow.Ensure(poolMember.gameObject).BeginLanding(0.3f);
            _enemiesRemaining++;
        }

        public EnemyBase SpawnSummoned(EnemyArchetype archetype, Vector2 position)
        {
            if (!IsReady || !_contentByArchetype.TryGetValue(archetype, out EnemyContentDefinition content) ||
                content == null || !content.IsValid) return null;
            GameObject prefab = content.Prefab;
            EnemyPoolMember member = AcquireEnemy(prefab, position);
            member.Bind(prefab, ReleaseEnemy);
            EnemyBase enemy = member.Enemy != null ? member.Enemy : member.GetComponent<EnemyBase>();
            if (enemy == null) return null;
            EnemyData template = content.Data;
            EnemyData data = enemy.CreateScaledRuntimeData(template, config.GetHealthMultiplier(_currentFloor), config.GetSpeedMultiplier(_currentFloor));
            enemy.Initialize(data);
            PlaceEnemy(member, position);
            enemy.OnDeath += OnEnemyKilled;
            _activeEnemies.Add(member);
            _enemiesRemaining++;
            return enemy;
        }

        private Vector3 GetRoomSpawnPosition(int index)
        {
            float halfX = Mathf.Max(2f, _roomSize.x * 0.5f - 4f);
            float halfY = Mathf.Max(2f, _roomSize.y * 0.5f - 3f);
            float angle = (index * 2.39996323f) + UnityEngine.Random.Range(-0.25f, 0.25f);
            float radius = Mathf.Lerp(0.35f, 0.9f, UnityEngine.Random.value);
            return _roomCenter + new Vector2(Mathf.Cos(angle) * halfX * radius, Mathf.Sin(angle) * halfY * radius);
        }

        private void EnterCrisis()
        {
            _isCrisis = true;
            _nextCrisisEmpowerTime = Time.time;
            _crisisTimeScale = Mathf.Clamp(config.crisisTimeScaleStart, 1f,
                Mathf.Max(1f, config.crisisTimeScaleMax));
            _crisisElapsedUnscaledTime = 0f;
            Time.timeScale = _crisisTimeScale;
            SetCrisisOverlay(true);
            UpdateCrisisOverlay();
        }

        private void EmpowerLivingEnemies()
        {
            foreach (EnemyPoolMember member in _activeEnemies)
                if (member != null && !member.IsReleased && member.Enemy != null && !member.Enemy.IsDead)
                    member.Enemy.EmpowerForCrisis(config, _currentFloor);
        }

        private void CompleteRoom()
        {
            if (_currentPhase == Phase.Cleared) return;
            _currentPhase = Phase.Cleared;
            ResetGameplayTimeScale();
            SetCrisisOverlay(false);
            onWaveCleared?.RaiseEvent();
            Action callback = _roomCleared;
            _roomCleared = null;
            callback?.Invoke();
        }

        private void StopEncounter(bool releaseEnemies)
        {
            if (_spawnRoutine != null)
            {
                StopCoroutine(_spawnRoutine);
                _spawnRoutine = null;
            }
            if (releaseEnemies) ReleaseAllActiveEnemies();
            _allSpawned = false;
            ResetGameplayTimeScale();
            _activeBosses.Clear();
            _bossMaxHealthTotal = 0f;
            _roomCleared = null;
            SetCrisisOverlay(false);
        }

        private void ResetGameplayTimeScale()
        {
            _isCrisis = false;
            _crisisTimeScale = 1f;
            if (GameManager.Instance.IsPlaying) Time.timeScale = 1f;
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
            member.transform.SetParent(transform, false);
            member.transform.SetPositionAndRotation(position, Quaternion.identity);
            member.gameObject.SetActive(true);
            Rigidbody2D body = member.GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.position = position;
                body.velocity = Vector2.zero;
                body.angularVelocity = 0f;
            }
            Physics2D.SyncTransforms();
            _activeEnemies.Add(member);
            return member;
        }

        private static void PlaceEnemy(EnemyPoolMember member, Vector3 position)
        {
            if (member == null) return;
            member.transform.SetPositionAndRotation(position, Quaternion.identity);
            Rigidbody2D body = member.GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.position = position;
                body.velocity = Vector2.zero;
                body.angularVelocity = 0f;
            }
            member.GetComponent<EnemyActor>()?.SetSpawnPosition(position);
            Physics2D.SyncTransforms();
        }

        private void ReleaseEnemy(EnemyPoolMember member)
        {
            if (member == null) return;
            EnemyBase enemy = member.Enemy;
            if (enemy != null) enemy.OnDeath -= OnEnemyKilled;
            if (enemy != null) _activeBosses.Remove(enemy);
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
            if (_enemySpawnScales.TryGetValue(prefab, out Vector3 cached)) return cached;
            Vector3 baseScale = prefab.transform.localScale;
            float visualSize = 0f;
            foreach (SpriteRenderer renderer in prefab.GetComponentsInChildren<SpriteRenderer>(true))
                if (renderer != null && renderer.sprite != null)
                    visualSize = Mathf.Max(visualSize, renderer.sprite.bounds.size.x * Mathf.Abs(baseScale.x),
                        renderer.sprite.bounds.size.y * Mathf.Abs(baseScale.y));
            float multiplier = visualSize > 0.001f && visualSize < minimumEnemyVisualSize
                ? minimumEnemyVisualSize / visualSize : 1f;
            Vector3 result = new(baseScale.x * multiplier, baseScale.y * multiplier, baseScale.z);
            _enemySpawnScales[prefab] = result;
            return result;
        }

        private void ReleaseAllActiveEnemies()
        {
            if (_activeEnemies.Count == 0) return;
            List<EnemyPoolMember> snapshot = new(_activeEnemies);
            foreach (EnemyPoolMember member in snapshot)
            {
                if (member == null) continue;
                InhaleableItem item = member.GetComponent<InhaleableItem>();
                if (item != null && item.IsStoredInMouth) continue;
                member.Release();
            }
        }

        private void OnEnemyKilled(EnemyBase enemy)
        {
            enemy.OnDeath -= OnEnemyKilled;
            _enemiesRemaining = Mathf.Max(0, _enemiesRemaining - 1);
        }

        private GameObject[] GetTierPrefabs(int tier)
        {
            int index = Mathf.Clamp(tier - 1, 0, _tierPrefabs.Count - 1);
            return _tierPrefabs.Count == 0 ? Array.Empty<GameObject>() : _tierPrefabs[index];
        }

        private IEnumerator LoadEnemyContent()
        {
            AsyncOperationHandle<IList<IResourceLocation>> locationsHandle =
                Addressables.LoadResourceLocationsAsync(EnemyContentLabel, typeof(EnemyContentDefinition));
            yield return locationsHandle;
            if (locationsHandle.Status != AsyncOperationStatus.Succeeded || locationsHandle.Result == null)
            {
                Debug.LogError($"[WaveManager] Failed to resolve Addressables label '{EnemyContentLabel}'.");
                if (locationsHandle.IsValid()) Addressables.Release(locationsHandle);
                yield break;
            }

            foreach (IResourceLocation location in locationsHandle.Result)
                _contentHandles.Add(Addressables.LoadAssetAsync<EnemyContentDefinition>(location));
            Addressables.Release(locationsHandle);

            foreach (AsyncOperationHandle<EnemyContentDefinition> handle in _contentHandles)
            {
                yield return handle;
                if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null || !handle.Result.IsValid)
                {
                    Debug.LogError($"[WaveManager] Failed to load an enemy content bundle: {handle.OperationException}");
                    continue;
                }

                EnemyContentDefinition content = handle.Result;
                RegisterLoadedAtlas(content.Atlas);
                _contentByArchetype[content.Archetype] = content;
                _dataByPrefab[content.Prefab] = content.Data;
            }

            BuildPrefabLists();
            Debug.Log($"[WaveManager] Loaded {_contentByArchetype.Count} atomic enemy content bundles.");
        }

        private void BuildPrefabLists()
        {
            List<GameObject> minions = new();
            List<GameObject> elites = new();
            List<GameObject> bosses = new();
            foreach (EnemyContentDefinition content in _contentByArchetype.Values)
            {
                switch (content.Category)
                {
                    case EnemyContentCategory.Minion: minions.Add(content.Prefab); break;
                    case EnemyContentCategory.Elite: elites.Add(content.Prefab); break;
                    case EnemyContentCategory.Boss: bosses.Add(content.Prefab); break;
                }
            }

            _tierPrefabs.Clear();
            for (int tier = 1; tier <= 7; tier++)
                _tierPrefabs.Add(minions.ToArray());
            _elitePrefabs = elites.ToArray();
            _bossPrefabs = bosses.ToArray();
        }

        private EnemyData GetEnemyData(GameObject prefab)
        {
            return prefab != null && _dataByPrefab.TryGetValue(prefab, out EnemyData data) ? data : null;
        }

        private void OnAtlasRequested(string tag, Action<SpriteAtlas> register)
        {
            if (register == null || string.IsNullOrEmpty(tag)) return;
            if (_loadedAtlases.TryGetValue(tag, out SpriteAtlas atlas) && atlas != null)
            {
                register(atlas);
                return;
            }

            if (!_pendingAtlasRequests.TryGetValue(tag, out List<Action<SpriteAtlas>> requests))
            {
                requests = new List<Action<SpriteAtlas>>();
                _pendingAtlasRequests.Add(tag, requests);
            }
            requests.Add(register);
        }

        private void RegisterLoadedAtlas(SpriteAtlas atlas)
        {
            if (atlas == null) return;
            string tag = string.IsNullOrEmpty(atlas.tag) ? atlas.name : atlas.tag;
            _loadedAtlases[tag] = atlas;
            _loadedAtlases[atlas.name] = atlas;
            FulfillAtlasRequests(tag, atlas);
            if (!string.Equals(tag, atlas.name, StringComparison.Ordinal))
                FulfillAtlasRequests(atlas.name, atlas);
        }

        private void FulfillAtlasRequests(string tag, SpriteAtlas atlas)
        {
            if (!_pendingAtlasRequests.TryGetValue(tag, out List<Action<SpriteAtlas>> requests)) return;
            _pendingAtlasRequests.Remove(tag);
            foreach (Action<SpriteAtlas> register in requests) register?.Invoke(atlas);
        }

        private void CreateCrisisOverlay()
        {
            GameObject canvasObject = new("CrisisOverlay", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = -1;
            GameObject imageObject = new("RedScreen", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imageObject.transform.SetParent(canvasObject.transform, false);
            RectTransform rect = imageObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            _crisisOverlay = imageObject.GetComponent<Image>();
            _crisisOverlay.color = new Color(0.75f, 0f, 0f, 0f);
            _crisisOverlay.raycastTarget = false;
            imageObject.SetActive(false);
        }

        private void SetCrisisOverlay(bool visible)
        {
            if (_crisisOverlay != null) _crisisOverlay.gameObject.SetActive(visible);
        }

        private void UpdateCrisisOverlay()
        {
            if (_crisisOverlay == null || !_isCrisis || config == null) return;
            float maximumScale = Mathf.Max(1f, config.crisisTimeScaleMax);
            float speedProgress = Mathf.InverseLerp(
                Mathf.Clamp(config.crisisTimeScaleStart, 1f, maximumScale),
                maximumScale,
                _crisisTimeScale);
            float minimumAlpha = Mathf.Clamp(config.crisisOverlayMinAlpha, 0f, 0.5f);
            float maximumAlpha = Mathf.Clamp(config.crisisOverlayMaxAlpha, minimumAlpha, 0.5f);
            float baseAlpha = Mathf.Lerp(minimumAlpha, maximumAlpha, speedProgress);
            float pulseWave = (Mathf.Sin(_crisisElapsedUnscaledTime * Mathf.PI * 2f *
                Mathf.Max(0.1f, config.crisisOverlayPulseFrequency)) + 1f) * 0.5f;
            float warningPeak = Mathf.Pow(pulseWave, Mathf.Max(1f, config.crisisOverlayPulseSharpness));
            float pulse = Mathf.Lerp(Mathf.Clamp01(config.crisisOverlayPulseFloor), 1f, warningPeak);
            _crisisOverlay.color = new Color(0.85f, 0f, 0f, baseAlpha * pulse);
        }

        private void UpdateCrisisSpeed()
        {
            if (!_isCrisis || config == null) return;
            float start = Mathf.Clamp(config.crisisTimeScaleStart, 1f,
                Mathf.Max(1f, config.crisisTimeScaleMax));
            _crisisElapsedUnscaledTime += Time.unscaledDeltaTime;
            float increases = _crisisElapsedUnscaledTime /
                Mathf.Max(0.1f, config.crisisTimeScaleIncreaseInterval);
            _crisisTimeScale = Mathf.Min(
                Mathf.Max(1f, config.crisisTimeScaleMax),
                start + increases * Mathf.Max(0f, config.crisisTimeScaleStep));
            Time.timeScale = _crisisTimeScale;
        }

        private void NotifyUI()
        {
            OnWaveInfoChanged?.Invoke(_currentFloor, _enemiesRemaining, Timer, _maxTimer);
        }

        private void OnDestroy()
        {
            SpriteAtlasManager.atlasRequested -= OnAtlasRequested;
            _pendingAtlasRequests.Clear();
            _loadedAtlases.Clear();
            foreach (AsyncOperationHandle<EnemyContentDefinition> handle in _contentHandles)
                if (handle.IsValid()) Addressables.Release(handle);
            _contentHandles.Clear();
            if (Instance == this) Instance = null;
        }
    }
}
