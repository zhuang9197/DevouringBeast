using System;
using System.Collections.Generic;
using UnityEngine;

namespace DevouringBeast
{
    /// <summary>Owns the finite food budget and spawn clock for every room on a floor.</summary>
    [DisallowMultipleComponent]
    public sealed class EnvironmentItemSpawner : MonoBehaviour
    {
        private static readonly FoodKind[] DefaultFoods = { FoodKind.RiceBall, FoodKind.Baozi };

        [Header("Food Sprites")]
        [SerializeField] private Sprite riceBallSprite;
        [SerializeField] private Sprite baoziSprite;
        [SerializeField] private Sprite hotDogSprite;
        [SerializeField] private Sprite sushiSprite;

        private sealed class RoomFoodState
        {
            public Vector2 Center;
            public Vector2 Size;
            public bool Cleared;
            public int Remaining;
            public float NextRefreshTime;
            public readonly HashSet<WorldItemPoolMember> Active = new();
            public readonly Dictionary<Vector2Int, WorldItemPoolMember> OccupiedCells = new();
        }

        public static EnvironmentItemSpawner Instance { get; private set; }

        private int _initialFoodPerRoom;
        private int _refreshBatchSize;
        private int _maxActiveFood;
        private float _combatRefreshSeconds;
        private float _clearedRefreshSeconds;
        private float _popeGuaranteeRefreshSeconds;
        private float _minimumSpacing;
        private float _boundsPadding;
        private int _placementAttempts;
        private float _landingDuration;
        private float _foodColliderRadius;
        private float _foodWorldScale;
        private FoodBalanceSettings _balance;

        private readonly Dictionary<Vector2Int, RoomFoodState> _rooms = new();
        private readonly Dictionary<FoodKind, Stack<WorldItemPoolMember>> _pools = new();
        private readonly Dictionary<WorldItemPoolMember, Vector2Int> _memberRooms = new();
        private readonly Dictionary<WorldItemPoolMember, Vector2Int> _memberCells = new();
        private readonly List<FoodKind> _spawnChoices = new(4);
        private Transform _poolRoot;
        private Vector2Int _currentRoom;
        private bool _hasCurrentRoom;
        private bool _testMode;

        public int CurrentRoomRemaining => _hasCurrentRoom && _rooms.TryGetValue(_currentRoom, out RoomFoodState state)
            ? state.Remaining : 0;
        public event Action<int> CurrentRoomFoodChanged;

        private void Awake()
        {
            Instance = this;
            _balance = GameBalance.Current?.Food;
            if (_balance != null)
            {
                _initialFoodPerRoom = _balance.initialFoodPerRoom;
                _refreshBatchSize = Mathf.Max(1, _balance.refreshBatchSize);
                _maxActiveFood = Mathf.Max(1, _balance.maxActiveFood);
                _combatRefreshSeconds = _balance.refreshSeconds;
                _clearedRefreshSeconds = _balance.clearedRefreshSeconds;
                _popeGuaranteeRefreshSeconds = Mathf.Max(0.1f, _balance.popeGuaranteeRefreshSeconds);
                _minimumSpacing = _balance.minimumSpacing;
                _boundsPadding = _balance.boundsPadding;
                _placementAttempts = _balance.placementAttempts;
                _landingDuration = _balance.landingDuration;
                _foodColliderRadius = _balance.colliderRadius;
                _foodWorldScale = _balance.worldScale;
            }
            foreach (FoodKind kind in Enum.GetValues(typeof(FoodKind)))
                _pools[kind] = new Stack<WorldItemPoolMember>();
            GameObject root = new("FoodPool");
            root.transform.SetParent(transform, false);
            _poolRoot = root.transform;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (!_hasCurrentRoom || !GameManager.Instance.IsPlaying) return;
            if (!_rooms.TryGetValue(_currentRoom, out RoomFoodState state)) return;

            if (state.Remaining > 0)
            {
                if (Time.time < state.NextRefreshTime) return;
                int capacity = Mathf.Max(0, _maxActiveFood - state.Active.Count);
                int requested = Mathf.Min(_refreshBatchSize, _testMode ? capacity :
                    Mathf.Min(state.Remaining, capacity));
                int spawned = 0;
                for (int i = 0; i < requested; i++)
                {
                    if (!TrySpawn(_currentRoom, state)) break;
                    spawned++;
                }

                if (!_testMode) state.Remaining -= spawned;
                NotifyCurrentRoom(state);
                state.NextRefreshTime = Time.time + (_testMode ? 0.1f : GetRefreshSeconds(state));
            }
            else if (!state.Cleared && state.Active.Count == 0)
            {
                if (Time.time < state.NextRefreshTime) return;
                TrySpawn(_currentRoom, state);
                state.NextRefreshTime = Time.time + _popeGuaranteeRefreshSeconds;
            }
        }

        public void SetCurrentRoom(Vector2Int room, Vector2 center, Vector2 size, bool cleared)
        {
            _currentRoom = room;
            _hasCurrentRoom = true;
            if (!_rooms.TryGetValue(room, out RoomFoodState state))
            {
                state = new RoomFoodState
                {
                    Center = center,
                    Size = size,
                    Cleared = cleared,
                    Remaining = _testMode ? _maxActiveFood : Mathf.Max(0, _initialFoodPerRoom)
                };
                _rooms.Add(room, state);
            }
            else
            {
                state.Center = center;
                state.Size = size;
                state.Cleared = cleared;
            }
            state.NextRefreshTime = _testMode ? Time.time : Time.time + GetRefreshSeconds(state);
            NotifyCurrentRoom(state);
        }

        public void SetCurrentRoomCleared(bool cleared)
        {
            if (!_hasCurrentRoom || !_rooms.TryGetValue(_currentRoom, out RoomFoodState state)) return;
            state.Cleared = cleared;
            state.NextRefreshTime = Time.time + GetRefreshSeconds(state);
            NotifyCurrentRoom(state);
        }

        public int AddFoodToCurrentRoom(int amount)
        {
            if (amount <= 0 || !_hasCurrentRoom || !_rooms.TryGetValue(_currentRoom, out RoomFoodState state)) return 0;
            state.Remaining += amount;
            state.NextRefreshTime = Time.time + GetRefreshSeconds(state);
            NotifyCurrentRoom(state);
            return state.Remaining;
        }

        public void EnableTestMode()
        {
            _testMode = true;
            if (!_hasCurrentRoom || !_rooms.TryGetValue(_currentRoom, out RoomFoodState state)) return;
            state.Remaining = _maxActiveFood;
            state.NextRefreshTime = Time.time;
            NotifyCurrentRoom(state);
        }

        public bool ShouldCurrentPopeGlow()
        {
            return _hasCurrentRoom && _rooms.TryGetValue(_currentRoom, out RoomFoodState state) &&
                !state.Cleared && state.Remaining == 0 && state.Active.Count == 0;
        }

        public bool IsCurrentRoom(Vector2Int room) => _hasCurrentRoom && _currentRoom == room;

        public void ResetForFloor()
        {
            foreach (RoomFoodState room in _rooms.Values)
            {
                List<WorldItemPoolMember> snapshot = new(room.Active);
                foreach (WorldItemPoolMember member in snapshot) HandleRelease(member);
            }
            _rooms.Clear();
            _memberRooms.Clear();
            _memberCells.Clear();
            _hasCurrentRoom = false;
            CurrentRoomFoodChanged?.Invoke(0);
        }

        private bool TrySpawn(Vector2Int roomKey, RoomFoodState state)
        {
            if (!TryFindPosition(state, out Vector2 position, out Vector2Int cell)) return false;
            WorldItemPoolMember member = Spawn(roomKey, state, ChooseFood(), position, cell);
            return member != null;
        }

        private float GetRefreshSeconds(RoomFoodState state)
        {
            return Mathf.Max(0.1f, state.Cleared ? _clearedRefreshSeconds : _combatRefreshSeconds);
        }

        private void NotifyCurrentRoom(RoomFoodState state)
        {
            if (_hasCurrentRoom && _rooms.TryGetValue(_currentRoom, out RoomFoodState current) && current == state)
                CurrentRoomFoodChanged?.Invoke(state.Remaining);
        }

        private FoodKind ChooseFood()
        {
            _spawnChoices.Clear();
            _spawnChoices.AddRange(DefaultFoods);
            RogueSkillManager skills = RogueSkillManager.Active;
            if (skills != null && skills.Has(RogueSkillId.HotDogLover)) _spawnChoices.Add(FoodKind.HotDog);
            if (skills != null && skills.Has(RogueSkillId.SushiMaster)) _spawnChoices.Add(FoodKind.Sushi);
            return _spawnChoices[UnityEngine.Random.Range(0, _spawnChoices.Count)];
        }

        private WorldItemPoolMember Spawn(Vector2Int roomKey, RoomFoodState state, FoodKind kind,
            Vector2 position, Vector2Int cell)
        {
            WorldItemPoolMember member = _pools[kind].Count > 0 ? _pools[kind].Pop() : CreateMember(kind);
            if (member == null) return null;
            member.transform.SetPositionAndRotation(position, Quaternion.identity);
            member.gameObject.SetActive(true);
            member.Item.ResetForReuse();
            member.Food.Configure(kind, GetMass(kind));
            GroundShadow.Ensure(member.gameObject).BeginLanding(_landingDuration);
            state.Active.Add(member);
            state.OccupiedCells[cell] = member;
            _memberRooms[member] = roomKey;
            _memberCells[member] = cell;
            return member;
        }

        private WorldItemPoolMember CreateMember(FoodKind kind)
        {
            GameObject go = new(kind.ToString(), typeof(SpriteRenderer), typeof(CircleCollider2D), typeof(Rigidbody2D),
                typeof(InhaleableItem), typeof(FoodItem), typeof(WorldItemPoolMember));
            go.transform.SetParent(_poolRoot, false);
            int layer = LayerMask.NameToLayer("inhaleableLayer");
            if (layer >= 0) go.layer = layer;
            SpriteRenderer renderer = go.GetComponent<SpriteRenderer>();
            renderer.sprite = GetSprite(kind);
            renderer.sortingOrder = 2;
            CircleCollider2D collider = go.GetComponent<CircleCollider2D>();
            collider.isTrigger = false;
            collider.radius = _foodColliderRadius;
            Rigidbody2D body = go.GetComponent<Rigidbody2D>();
            ConfigurePushableBody(body);
            go.transform.localScale = Vector3.one * _foodWorldScale;
            InhaleableItem item = go.GetComponent<InhaleableItem>();
            item.SetRestingScale(go.transform.localScale);
            FoodItem food = go.GetComponent<FoodItem>();
            food.Configure(kind, GetMass(kind));
            WorldItemPoolMember member = go.GetComponent<WorldItemPoolMember>();
            member.Configure((int)kind, item, food, HandleRelease);
            GroundShadow.Ensure(go);
            return member;
        }

        private static void ConfigurePushableBody(Rigidbody2D body)
        {
            if (body == null) return;
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 0f;
            body.drag = 6f;
            body.angularDrag = 6f;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }

        private void HandleRelease(WorldItemPoolMember member)
        {
            if (member == null) return;
            if (_memberRooms.TryGetValue(member, out Vector2Int roomKey) && _rooms.TryGetValue(roomKey, out RoomFoodState room))
            {
                room.Active.Remove(member);
                if (_memberCells.TryGetValue(member, out Vector2Int cell) &&
                    room.OccupiedCells.TryGetValue(cell, out WorldItemPoolMember occupant) && occupant == member)
                    room.OccupiedCells.Remove(cell);
            }
            _memberRooms.Remove(member);
            _memberCells.Remove(member);
            member.Item.ResetForReuse();
            member.gameObject.SetActive(false);
            _pools[(FoodKind)member.Kind].Push(member);
        }

        private bool TryFindPosition(RoomFoodState room, out Vector2 position, out Vector2Int cell)
        {
            Vector2 half = room.Size * 0.5f - Vector2.one * _boundsPadding;
            for (int attempt = 0; attempt < _placementAttempts; attempt++)
            {
                Vector2 candidate = room.Center + new Vector2(
                    UnityEngine.Random.Range(-half.x, half.x), UnityEngine.Random.Range(-half.y, half.y));
                Vector2Int candidateCell = ToLocalCell(room, candidate);
                bool occupied = false;
                for (int y = -1; y <= 1 && !occupied; y++)
                for (int x = -1; x <= 1; x++)
                {
                    if (!room.OccupiedCells.TryGetValue(candidateCell + new Vector2Int(x, y), out WorldItemPoolMember nearby)) continue;
                    if (((Vector2)nearby.transform.position - candidate).sqrMagnitude >= _minimumSpacing * _minimumSpacing) continue;
                    occupied = true;
                    break;
                }
                if (occupied) continue;
                position = candidate;
                cell = candidateCell;
                return true;
            }
            position = default;
            cell = default;
            return false;
        }

        private Vector2Int ToLocalCell(RoomFoodState room, Vector2 position)
        {
            Vector2 local = position - room.Center;
            return new Vector2Int(Mathf.FloorToInt(local.x / _minimumSpacing), Mathf.FloorToInt(local.y / _minimumSpacing));
        }

        private Sprite GetSprite(FoodKind kind) => kind switch
        {
            FoodKind.Baozi => baoziSprite,
            FoodKind.HotDog => hotDogSprite,
            FoodKind.Sushi => sushiSprite,
            _ => riceBallSprite
        };

        private float GetMass(FoodKind kind) => _balance != null ? _balance.GetMass(kind) : 0f;
    }

    [DisallowMultipleComponent]
    public sealed class WorldItemPoolMember : MonoBehaviour
    {
        private Action<WorldItemPoolMember> _release;
        public int Kind { get; private set; }
        public InhaleableItem Item { get; private set; }
        public FoodItem Food { get; private set; }

        public void Configure(int kind, InhaleableItem item, FoodItem food, Action<WorldItemPoolMember> release)
        {
            Kind = kind;
            Item = item;
            Food = food;
            _release = release;
        }

        public void Release() => _release?.Invoke(this);
    }
}
