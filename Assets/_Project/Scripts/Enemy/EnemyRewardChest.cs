using System.Collections.Generic;
using UnityEngine;

namespace DevouringBeast
{
    [DisallowMultipleComponent]
    public sealed class EnemyRewardChest : MonoBehaviour
    {
        private static readonly HashSet<EnemyRewardChest> Active = new();
        private static readonly Queue<EnemyRewardChest> Pool = new();
        private static Sprite _sprite;
        private static Transform _poolRoot;
        private InhaleableItem _item;
        private float _mass;

        public static void Spawn(Vector3 position, float mass)
        {
            EnsurePool();
            EnemyRewardChest chest = null;
            while (Pool.Count > 0 && chest == null) chest = Pool.Dequeue();
            if (chest == null) chest = Create();
            if (chest == null) return;
            chest._mass = Mathf.Max(1f, mass);
            chest._item.Mass = chest._mass;
            chest._item.DeadInhaleThreshold = Mathf.Min(10f, chest._mass);
            chest._item.IsAlive = false;
            chest.transform.SetParent(null, false);
            chest.transform.position = position;
            chest.gameObject.SetActive(true);
            Active.Add(chest);
            GroundShadow.Ensure(chest.gameObject).BeginLanding(0.3f);
        }

        public static void ReleaseFloorChests()
        {
            foreach (EnemyRewardChest chest in new List<EnemyRewardChest>(Active))
            {
                // Scene reload can destroy an active chest before BuildFloor gets here.
                // Unity's fake-null object still remains in the static set, so skip it.
                if (chest != null) chest.Release();
            }
            Active.Clear();
        }

        public void Release()
        {
            if (!Active.Remove(this)) return;
            EnsurePool();
            if (_item != null) _item.ResetForReuse();
            gameObject.SetActive(false);
            transform.SetParent(_poolRoot, false);
            Pool.Enqueue(this);
        }

        private static EnemyRewardChest Create()
        {
            GameObject go = new("EnemyRewardChest", typeof(SpriteRenderer), typeof(CircleCollider2D), typeof(Rigidbody2D),
                typeof(InhaleableItem), typeof(EnemyRewardChest));
            go.layer = Mathf.Max(0, LayerMask.NameToLayer("inhaleableLayer"));
            SpriteRenderer renderer = go.GetComponent<SpriteRenderer>();
            renderer.sprite = _sprite;
            renderer.sortingOrder = 3;
            CircleCollider2D collider = go.GetComponent<CircleCollider2D>();
            collider.isTrigger = false;
            collider.radius = 0.45f;
            Rigidbody2D body = go.GetComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 0f;
            body.drag = 6f;
            body.angularDrag = 6f;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            EnemyRewardChest chest = go.GetComponent<EnemyRewardChest>();
            chest._item = go.GetComponent<InhaleableItem>();
            chest._item.IsAlive = false;
            chest._item.IgnoreSuctionThreshold = true;
            GroundShadow.Ensure(go);
            return chest;
        }

        private static void EnsurePool()
        {
            if (_sprite == null) _sprite = Resources.Load<Sprite>("Drops/treasure_chest");
            if (_poolRoot == null)
            {
                GameObject root = new("EnemyRewardChestPool");
                Object.DontDestroyOnLoad(root);
                _poolRoot = root.transform;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Active.Clear();
            Pool.Clear();
            _sprite = null;
            _poolRoot = null;
        }
    }
}
