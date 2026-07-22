using System;
using System.Collections.Generic;
using UnityEngine;

namespace DevouringBeast
{
    /// <summary>
    /// Maintains pooled, update-free world items. A spatial hash keeps random placement
    /// evenly distributed without pairwise distance checks.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnvironmentItemSpawner : MonoBehaviour
    {
        private enum ItemKind { BigStone, Stone, Mushroom, RiceBall }

        [Header("Sprites")]
        [SerializeField] private Sprite bigStoneSprite;
        [SerializeField] private Sprite stoneSprite;
        [SerializeField] private Sprite mushroomSprite;
        [SerializeField] private Sprite riceBallSprite;

        [Header("Population")]
        [SerializeField, Min(0)] private int bigStoneTarget = 10;
        [SerializeField, Min(0)] private int stoneTarget = 100;
        [SerializeField, Min(0)] private int mushroomTarget = 120;
        [SerializeField, Min(0)] private int riceBallTarget = 120;
        [SerializeField, Min(1f)] private float bigStoneRefreshSeconds = 30f;
        [SerializeField, Min(1f)] private float stoneRefreshSeconds = 20f;
        [SerializeField, Min(1f)] private float foodRefreshSeconds = 8f;

        [Header("Distribution")]
        [SerializeField, Min(0.5f)] private float minimumSpacing = 1.5f;
        [SerializeField, Min(0f)] private float boundsPadding = 1f;
        [SerializeField, Min(1)] private int placementAttempts = 24;

        private readonly Dictionary<ItemKind, Stack<WorldItemPoolMember>> _pools = new();
        private readonly Dictionary<ItemKind, HashSet<WorldItemPoolMember>> _active = new();
        private readonly Dictionary<Vector2Int, WorldItemPoolMember> _occupiedCells = new();
        private readonly Dictionary<WorldItemPoolMember, Vector2Int> _memberCells = new();
        private Transform _poolRoot;
        private float _nextBigStoneRefresh;
        private float _nextStoneRefresh;
        private float _nextFoodRefresh;

        private void Awake()
        {
            foreach (ItemKind kind in Enum.GetValues(typeof(ItemKind)))
            {
                _pools[kind] = new Stack<WorldItemPoolMember>();
                _active[kind] = new HashSet<WorldItemPoolMember>();
            }
            GameObject root = new("EnvironmentItemPool");
            root.transform.SetParent(transform, false);
            _poolRoot = root.transform;
        }

        private void Start()
        {
            Refill(ItemKind.BigStone, bigStoneTarget);
            Refill(ItemKind.Stone, stoneTarget);
            Refill(ItemKind.Mushroom, mushroomTarget);
            Refill(ItemKind.RiceBall, riceBallTarget);
            ScheduleNextRefreshes();
        }

        private void Update()
        {
            if (!GameManager.Instance.IsPlaying) return;
            if (Time.time >= _nextBigStoneRefresh)
            {
                Refill(ItemKind.BigStone, bigStoneTarget);
                _nextBigStoneRefresh = Time.time + bigStoneRefreshSeconds;
            }
            if (Time.time >= _nextStoneRefresh)
            {
                Refill(ItemKind.Stone, stoneTarget);
                _nextStoneRefresh = Time.time + stoneRefreshSeconds;
            }
            if (Time.time >= _nextFoodRefresh)
            {
                Refill(ItemKind.Mushroom, mushroomTarget);
                Refill(ItemKind.RiceBall, riceBallTarget);
                _nextFoodRefresh = Time.time + foodRefreshSeconds;
            }
        }

        private void ScheduleNextRefreshes()
        {
            _nextBigStoneRefresh = Time.time + bigStoneRefreshSeconds;
            _nextStoneRefresh = Time.time + stoneRefreshSeconds;
            _nextFoodRefresh = Time.time + foodRefreshSeconds;
        }

        private void Refill(ItemKind kind, int target)
        {
            int missing = Mathf.Max(0, target - _active[kind].Count);
            for (int i = 0; i < missing; i++)
            {
                if (!TryFindPosition(out Vector2 position)) break;
                Spawn(kind, position);
            }
        }

        private void Spawn(ItemKind kind, Vector2 position)
        {
            WorldItemPoolMember member = _pools[kind].Count > 0
                ? _pools[kind].Pop() : CreateMember(kind);
            member.transform.position = position;
            member.gameObject.SetActive(true);
            member.Item.ResetForReuse();
            _active[kind].Add(member);
            Vector2Int cell = ToCell(position);
            _occupiedCells[cell] = member;
            _memberCells[member] = cell;
        }

        private WorldItemPoolMember CreateMember(ItemKind kind)
        {
            GameObject go = new(kind.ToString(), typeof(SpriteRenderer),
                typeof(CircleCollider2D), typeof(InhaleableItem), typeof(WorldItemPoolMember));
            go.transform.SetParent(_poolRoot, false);
            int layer = LayerMask.NameToLayer("inhaleableLayer");
            if (layer >= 0) go.layer = layer;

            SpriteRenderer renderer = go.GetComponent<SpriteRenderer>();
            renderer.sprite = GetSprite(kind);
            renderer.sortingOrder = 2;
            CircleCollider2D collider = go.GetComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = kind == ItemKind.BigStone ? 0.55f : 0.35f;

            InhaleableItem item = go.GetComponent<InhaleableItem>();
            item.Tag = ItemTag.Normal;
            item.Mass = GetMass(kind);
            item.DeadInhaleThreshold = item.Mass;
            item.IsAlive = false;
            float scale = kind == ItemKind.BigStone ? 1.25f : kind == ItemKind.Stone ? 0.9f : 0.65f;
            item.SetRestingScale(Vector3.one * scale);

            WorldItemPoolMember member = go.GetComponent<WorldItemPoolMember>();
            member.Configure((int)kind, item, HandleRelease);
            return member;
        }

        private void HandleRelease(WorldItemPoolMember member)
        {
            ItemKind kind = (ItemKind)member.Kind;
            if (!_active[kind].Remove(member)) return;
            if (_memberCells.TryGetValue(member, out Vector2Int cell))
            {
                _memberCells.Remove(member);
                if (_occupiedCells.TryGetValue(cell, out WorldItemPoolMember occupant) && occupant == member)
                    _occupiedCells.Remove(cell);
            }
            member.Item.ResetForReuse();
            member.gameObject.SetActive(false);
            _pools[kind].Push(member);
        }

        private bool TryFindPosition(out Vector2 position)
        {
            Vector2 min = MapBounds.Instance != null ? MapBounds.Instance.Min : new Vector2(-20f, -20f);
            Vector2 max = MapBounds.Instance != null ? MapBounds.Instance.Max : new Vector2(20f, 20f);
            min += Vector2.one * boundsPadding;
            max -= Vector2.one * boundsPadding;
            for (int attempt = 0; attempt < placementAttempts; attempt++)
            {
                Vector2 candidate = new(UnityEngine.Random.Range(min.x, max.x),
                    UnityEngine.Random.Range(min.y, max.y));
                Vector2Int cell = ToCell(candidate);
                bool occupied = false;
                for (int y = -1; y <= 1 && !occupied; y++)
                    for (int x = -1; x <= 1; x++)
                        if (_occupiedCells.TryGetValue(cell + new Vector2Int(x, y), out WorldItemPoolMember nearby) &&
                            ((Vector2)nearby.transform.position - candidate).sqrMagnitude < minimumSpacing * minimumSpacing)
                        {
                            occupied = true;
                            break;
                        }
                if (occupied) continue;
                position = candidate;
                return true;
            }
            position = default;
            return false;
        }

        private Vector2Int ToCell(Vector2 position) => new(
            Mathf.FloorToInt(position.x / minimumSpacing),
            Mathf.FloorToInt(position.y / minimumSpacing));

        private Sprite GetSprite(ItemKind kind) => kind switch
        {
            ItemKind.BigStone => bigStoneSprite,
            ItemKind.Stone => stoneSprite,
            ItemKind.Mushroom => mushroomSprite,
            _ => riceBallSprite
        };

        private static float GetMass(ItemKind kind) => kind switch
        {
            ItemKind.BigStone => 50f,
            ItemKind.Stone => 10f,
            _ => 1f
        };
    }

    [DisallowMultipleComponent]
    public sealed class WorldItemPoolMember : MonoBehaviour
    {
        private Action<WorldItemPoolMember> _release;
        public int Kind { get; private set; }
        public InhaleableItem Item { get; private set; }

        public void Configure(int kind, InhaleableItem item, Action<WorldItemPoolMember> release)
        {
            Kind = kind;
            Item = item;
            _release = release;
        }

        public void Release() => _release?.Invoke(this);
    }
}
