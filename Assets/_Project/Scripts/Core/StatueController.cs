using UnityEngine;
using System.Collections.Generic;

namespace DevouringBeast
{
    public enum StatueKind { Angel, Demon, Pope }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer), typeof(Collider2D))]
    public sealed class StatueController : MonoBehaviour
    {
        private StatueKind _kind;
        private Vector2Int _room;
        private FloorMapManager _floor;
        private EnvironmentItemSpawner _food;
        private SpriteRenderer _renderer;
        private Sprite _intactSprite;
        private Sprite _destroyedSprite;
        private StatueBalanceSettings _balance;
        private int _hitCount;
        private int _touchCount;
        private bool _destroyed;
        private bool _busy;
        private Transform _playerInside;
        private bool _usedWhileInside;
        private CircleCollider2D _solidCollider;
        private CircleCollider2D _interactionTrigger;
        private static readonly HashSet<StatueController> ActiveStatues = new();

        public StatueKind Kind => _kind;
        public Vector2Int Room => _room;
        public bool IsDestroyed => _destroyed;

        public void Initialize(StatueKind kind, Vector2Int room, FloorMapManager floor,
            EnvironmentItemSpawner food, Sprite intact, Sprite destroyed)
        {
            _kind = kind;
            _room = room;
            _floor = floor;
            _food = food;
            _intactSprite = intact;
            _destroyedSprite = destroyed;
            _balance = GameBalance.Current?.Statues;
            _renderer = GetComponent<SpriteRenderer>();
            _renderer.sprite = intact;
            _renderer.sortingOrder = 4;
            _solidCollider = GetComponent<CircleCollider2D>();
            _solidCollider.isTrigger = false;
            _interactionTrigger = gameObject.AddComponent<CircleCollider2D>();
            _interactionTrigger.isTrigger = true;
            ScaleVisual();
            GroundShadow.Ensure(gameObject);
        }

        private void OnEnable() => ActiveStatues.Add(this);

        private void OnDisable() => ActiveStatues.Remove(this);

        private void Update()
        {
            if (_kind != StatueKind.Pope || _destroyed || _renderer == null) return;
            bool glow = _food != null && _food.IsCurrentRoom(_room) && _food.ShouldCurrentPopeGlow();
            if (!glow)
            {
                _renderer.color = Color.white;
                return;
            }
            float pulse = 0.65f + Mathf.PingPong(Time.unscaledTime * 1.2f, 0.35f);
            _renderer.color = new Color(1f, pulse, 0.35f, 1f);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other == null || _destroyed) return;
            PlayerHealth health = other.GetComponentInParent<PlayerHealth>();
            if (health != null)
            {
                Transform playerRoot = health.transform;
                if (_playerInside == playerRoot) return;
                _playerInside = playerRoot;
                _usedWhileInside = false;
                if (IsIntentionalFrontContact(health)) TryUse(health);
                return;
            }

            if (_kind == StatueKind.Pope) TryConsumeOffering(other);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (_destroyed || _usedWhileInside) return;
            PlayerHealth health = other != null ? other.GetComponentInParent<PlayerHealth>() : null;
            if (health != null && health.transform == _playerInside && IsIntentionalFrontContact(health))
                TryUse(health);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            PlayerHealth health = other != null ? other.GetComponentInParent<PlayerHealth>() : null;
            if (health != null && health.transform == _playerInside)
            {
                _playerInside = null;
                _usedWhileInside = false;
            }
        }

        private bool IsIntentionalFrontContact(PlayerHealth health)
        {
            if (health == null) return false;
            PlayerController controller = health.GetComponent<PlayerController>();
            if (controller == null || controller.MoveDirection.sqrMagnitude <= 0.01f) return false;

            Vector2 playerPosition = health.transform.position;
            Vector2 statueToPlayer = (playerPosition - (Vector2)transform.position).normalized;
            Vector2 playerToStatue = -statueToPlayer;
            float threshold = _balance != null ? _balance.frontContactDot : 0.55f;
            bool isOnFrontSide = Vector2.Dot(Vector2.down, statueToPlayer) >= threshold;
            bool isMovingTowardStatue = Vector2.Dot(controller.MoveDirection.normalized, playerToStatue) >= threshold;
            return isOnFrontSide && isMovingTowardStatue;
        }

        private void TryUse(PlayerHealth health)
        {
            if (_busy || health == null || health.IsInvincible) return;
            int cost = _balance != null ? Mathf.Max(1, _balance.healthCost) : 1;
            if (health.CurrentHealth <= cost) return;

            switch (_kind)
            {
                case StatueKind.Angel:
                    if (RogueSkillManager.Active == null || !RogueSkillManager.Active.RequestBasicStatueChoice()) return;
                    health.TrySpendHealth(cost);
                    _usedWhileInside = true;
                    break;
                case StatueKind.Pope:
                    if (!health.TrySpendHealth(cost)) return;
                    _food?.AddFoodToCurrentRoom(_balance != null ? _balance.popeFoodPerHealth : 3);
                    _usedWhileInside = true;
                    break;
                case StatueKind.Demon:
                    int nextTouch = _touchCount + 1;
                    _busy = true;
                    if (_floor == null || !_floor.TryStartDemonChallenge(nextTouch, OnDemonChallengeCleared))
                    {
                        _busy = false;
                        return;
                    }
                    if (!health.TrySpendHealth(cost))
                    {
                        _busy = false;
                        return;
                    }
                    _touchCount = nextTouch;
                    if (_touchCount >= 36) SetDestroyed();
                    _usedWhileInside = true;
                    break;
            }
        }

        private void OnDemonChallengeCleared()
        {
            _busy = false;
        }

        private void TryConsumeOffering(Collider2D other)
        {
            InhaleableItem item = other.GetComponentInParent<InhaleableItem>();
            if (item == null || item.GetComponent<FoodItem>() != null || item.IsAlive || item.IsBeingInhaled) return;
            int amount = _balance != null ? _balance.popeFoodPerOffering : 1;
            _food?.AddFoodToCurrentRoom(Mathf.Max(1, amount));
            item.ReleaseFromMouth();
        }

        public bool ReceiveAttack()
        {
            if (_kind != StatueKind.Angel || _destroyed) return false;
            _hitCount++;
            int required = _balance != null ? Mathf.Max(1, _balance.angelBreakHits) : 100;
            if (_hitCount >= required) BreakAngelStatue();
            return true;
        }

        public void DestroyWhenLeavingStartRoom()
        {
            if (_kind == StatueKind.Angel && !_destroyed) SetDestroyed();
        }

        private void BreakAngelStatue()
        {
            SetDestroyed();
            int drops = _balance != null ? Mathf.Max(1, _balance.angelHeartDrops) : 3;
            for (int i = 0; i < drops; i++)
            {
                float angle = i * Mathf.PI * 2f / drops;
                BloodDrop.Spawn(transform.position + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * 1.2f, true);
            }
        }

        private void SetDestroyed()
        {
            _destroyed = true;
            if (_renderer != null && _destroyedSprite != null) _renderer.sprite = _destroyedSprite;
            ScaleVisual();
        }

        private void ScaleVisual()
        {
            if (_renderer == null || _renderer.sprite == null) return;
            float targetHeight = _balance != null ? Mathf.Max(0.5f, _balance.visualHeight) : 3f;
            float spriteHeight = Mathf.Max(0.01f, _renderer.sprite.bounds.size.y);
            transform.localScale = Vector3.one * (targetHeight / spriteHeight);
            float radius = Mathf.Max(0.25f, _renderer.sprite.bounds.extents.x * 0.75f);
            if (_solidCollider != null) _solidCollider.radius = radius;
            if (_interactionTrigger != null) _interactionTrigger.radius = radius + 0.45f;
        }

        public static Vector2 ConstrainMovement(Collider2D mover, Vector2 current, Vector2 target)
        {
            if (mover == null || target == current) return target;
            Vector2 movement = target - current;
            float moverRadius = Mathf.Max(mover.bounds.extents.x, mover.bounds.extents.y);
            float closestFraction = 1f;

            foreach (StatueController statue in ActiveStatues)
            {
                if (statue == null || !statue.isActiveAndEnabled || statue._solidCollider == null ||
                    !statue._solidCollider.enabled) continue;
                float statueRadius = Mathf.Max(statue._solidCollider.bounds.extents.x,
                    statue._solidCollider.bounds.extents.y);
                float combinedRadius = moverRadius + statueRadius;
                Vector2 offset = current - (Vector2)statue.transform.position;
                float a = Vector2.Dot(movement, movement);
                float b = 2f * Vector2.Dot(offset, movement);
                float c = Vector2.Dot(offset, offset) - combinedRadius * combinedRadius;
                if (c <= 0f) continue;
                float discriminant = b * b - 4f * a * c;
                if (discriminant < 0f) continue;
                float hitFraction = (-b - Mathf.Sqrt(discriminant)) / (2f * a);
                if (hitFraction >= 0f && hitFraction <= closestFraction)
                    closestFraction = Mathf.Max(0f, hitFraction - 0.01f);
            }

            return current + movement * closestFraction;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => ActiveStatues.Clear();
    }
}
