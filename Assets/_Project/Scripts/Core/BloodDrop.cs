using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DevouringBeast
{
    [RequireComponent(typeof(SpriteRenderer), typeof(Collider2D))]
    public sealed class BloodDrop : MonoBehaviour
    {
        private const int SmallPrewarmCount = 12;
        private const int BigPrewarmCount = 4;
        private const int DropSortingOrder = 6;
        private static readonly Queue<BloodDrop> SmallPool = new();
        private static readonly Queue<BloodDrop> BigPool = new();
        private static readonly HashSet<BloodDrop> ActiveDrops = new();
        private static GameObject _smallPrefab;
        private static GameObject _bigPrefab;
        private static Sprite _smallSprite;
        private static Sprite _bigSprite;
        private static Transform _poolRoot;

        private SpriteRenderer _renderer;
        private Collider2D _collider;
        private bool _isBig;
        private bool _isPooled;
        private int _remainingHeal;

        public int HealAmount => _remainingHeal;

        private void Awake()
        {
            int dropLayer = LayerMask.NameToLayer("Drops");
            if (dropLayer >= 0) gameObject.layer = dropLayer;
            _renderer = GetComponent<SpriteRenderer>();
            _renderer.sortingOrder = DropSortingOrder;
            _collider = GetComponent<Collider2D>();
            _collider.isTrigger = true;
            InhaleableItem legacyInhaleable = GetComponent<InhaleableItem>();
            if (legacyInhaleable != null) legacyInhaleable.enabled = false;
            GroundShadow.Ensure(gameObject);
        }

        private void OnEnable()
        {
            if (_renderer != null) _renderer.enabled = true;
            if (_collider != null) _collider.enabled = true;
            ActiveDrops.Add(this);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
            PlayerHealth health = other.GetComponent<PlayerHealth>();
            if (health == null) health = other.GetComponentInParent<PlayerHealth>();
            if (health == null || health.CurrentHealth >= health.MaxHealth) return;

            int restored = Mathf.Min(_remainingHeal, health.MaxHealth - health.CurrentHealth);
            health.Heal(restored);
            _remainingHeal -= restored;
            if (_remainingHeal <= 0)
            {
                Release();
                return;
            }

            _isBig = false;
            _remainingHeal = 1;
            if (_renderer != null && _smallSprite != null) _renderer.sprite = _smallSprite;
        }

        public static void Spawn(Vector3 position, bool big)
        {
            EnsurePool();
            Queue<BloodDrop> pool = big ? BigPool : SmallPool;
            BloodDrop drop = null;
            while (pool.Count > 0 && drop == null) drop = pool.Dequeue();
            if (drop == null) drop = Create(big);
            if (drop == null) return;

            drop._isBig = big;
            drop._isPooled = false;
            drop._remainingHeal = big ? 2 : 1;
            drop.transform.SetParent(null);
            SceneManager.MoveGameObjectToScene(drop.gameObject, SceneManager.GetActiveScene());
            drop.transform.SetPositionAndRotation(position, Quaternion.identity);
            if (drop._renderer != null)
                drop._renderer.sprite = big ? _bigSprite : _smallSprite;
            drop.gameObject.SetActive(true);
            GroundShadow.Ensure(drop.gameObject).BeginLanding(0.35f);
        }

        public void Release()
        {
            if (_isPooled) return;
            _isPooled = true;
            ActiveDrops.Remove(this);
            gameObject.SetActive(false);
            if (_poolRoot != null) transform.SetParent(_poolRoot, false);
            (_isBig ? BigPool : SmallPool).Enqueue(this);
        }

        public static void ReleaseFloorDrops()
        {
            if (ActiveDrops.Count == 0) return;
            List<BloodDrop> snapshot = new(ActiveDrops);
            foreach (BloodDrop drop in snapshot)
                if (drop != null) drop.Release();
            ActiveDrops.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            SmallPool.Clear();
            BigPool.Clear();
            ActiveDrops.Clear();
            _smallPrefab = null;
            _bigPrefab = null;
            _smallSprite = null;
            _bigSprite = null;
            _poolRoot = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void WarmUp()
        {
            EnsurePool();
            Prewarm(false, SmallPrewarmCount);
            Prewarm(true, BigPrewarmCount);
        }

        private static void EnsurePool()
        {
            if (_smallPrefab == null) _smallPrefab = Resources.Load<GameObject>("Drops/Blood");
            if (_bigPrefab == null) _bigPrefab = Resources.Load<GameObject>("Drops/BigBlood");
            if (_smallPrefab != null) _smallSprite = _smallPrefab.GetComponent<SpriteRenderer>()?.sprite;
            if (_bigPrefab != null) _bigSprite = _bigPrefab.GetComponent<SpriteRenderer>()?.sprite;
            if (_poolRoot != null) return;
            GameObject root = new("[BloodDropPool]");
            Object.DontDestroyOnLoad(root);
            _poolRoot = root.transform;
        }

        private static void Prewarm(bool big, int targetCount)
        {
            Queue<BloodDrop> pool = big ? BigPool : SmallPool;
            while (pool.Count < targetCount)
            {
                BloodDrop drop = Create(big);
                if (drop == null) break;
                drop._isBig = big;
                drop._isPooled = true;
                drop.gameObject.SetActive(false);
                pool.Enqueue(drop);
            }
        }

        private static BloodDrop Create(bool big)
        {
            GameObject prefab = big ? _bigPrefab : _smallPrefab;
            if (prefab == null) return null;
            BloodDrop drop = Instantiate(prefab, _poolRoot).GetComponent<BloodDrop>();
            drop._isBig = big;
            return drop;
        }
    }
}
