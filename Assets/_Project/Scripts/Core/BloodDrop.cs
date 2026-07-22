using System.Collections;
using UnityEngine;

namespace DevouringBeast
{
    [RequireComponent(typeof(SpriteRenderer), typeof(Collider2D), typeof(InhaleableItem))]
    public sealed class BloodDrop : MonoBehaviour
    {
        [SerializeField, Min(1)] private int healAmount = 1;
        [SerializeField, Min(1f)] private float lifetime = 20f;
        [SerializeField, Min(0f)] private float blinkDuration = 5f;
        [SerializeField, Min(0.03f)] private float blinkInterval = 0.12f;

        private SpriteRenderer _renderer;
        private InhaleableItem _item;

        public int HealAmount => healAmount;

        private void Awake()
        {
            int inhaleableLayer = LayerMask.NameToLayer("inhaleableLayer");
            if (inhaleableLayer >= 0)
                gameObject.layer = inhaleableLayer;

            _renderer = GetComponent<SpriteRenderer>();
            _item = GetComponent<InhaleableItem>();
            _item.Tag = ItemTag.Normal;
            _item.Mass = 1f;
            _item.DeadInhaleThreshold = 1f;
            _item.IsAlive = false;
        }

        private void OnEnable()
        {
            if (_renderer != null) _renderer.enabled = true;
            StartCoroutine(LifetimeRoutine());
        }

        private IEnumerator LifetimeRoutine()
        {
            yield return new WaitForSeconds(Mathf.Max(0f, lifetime - blinkDuration));
            float endTime = Time.time + blinkDuration;
            while (Time.time < endTime)
            {
                if (_renderer != null) _renderer.enabled = !_renderer.enabled;
                yield return new WaitForSeconds(blinkInterval);
            }
            Destroy(gameObject);
        }

        public static void Spawn(Vector3 position, bool big)
        {
            GameObject prefab = Resources.Load<GameObject>(big ? "Drops/BigBlood" : "Drops/Blood");
            if (prefab != null) Instantiate(prefab, position, Quaternion.identity);
        }
    }
}
