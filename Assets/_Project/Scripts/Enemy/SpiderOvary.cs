using System.Collections;
using UnityEngine;

namespace DevouringBeast
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CircleCollider2D))]
    public sealed class SpiderOvary : MonoBehaviour
    {
        [SerializeField] private Sprite[] stages;
        private SpriteRenderer _renderer;
        private Collider2D _collider;
        private float _health = 30f;

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            _collider = GetComponent<Collider2D>();
            _collider.isTrigger = false;
            if (_collider is CircleCollider2D circle) circle.radius = 0.55f;
            StartCoroutine(LifeRoutine());
        }

        public void TakeDamage(float damage)
        {
            if (!isActiveAndEnabled) return;
            _health -= Mathf.Max(0f, damage);
            if (_health <= 0f) Destroy(gameObject);
        }

        private IEnumerator LifeRoutine()
        {
            for (int stage = 0; stage < 3; stage++)
            {
                if (_renderer != null && stages != null && stage < stages.Length) _renderer.sprite = stages[stage];
                Vector3 origin = transform.position;
                for (float elapsed = 0f; elapsed < 4f; elapsed += Time.deltaTime)
                {
                    transform.position = elapsed >= 3f ? origin + Random.insideUnitSphere * 0.06f : origin;
                    yield return null;
                }
                transform.position = origin;
            }

            float roll = Random.value;
            EnemyArchetype type = roll < 0.9f ? EnemyArchetype.Spider : roll < 0.95f
                ? EnemyArchetype.BigSpider : EnemyArchetype.HomeSpider;
            int count = type == EnemyArchetype.Spider ? Random.Range(2, 5) : 1;
            for (int i = 0; i < count; i++)
                WaveManager.Instance?.SpawnSummoned(type, (Vector2)transform.position + Random.insideUnitCircle);
            Destroy(gameObject);
        }
    }
}
