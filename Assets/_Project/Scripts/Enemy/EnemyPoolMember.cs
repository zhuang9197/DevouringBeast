using System;
using UnityEngine;

namespace DevouringBeast
{
    [DisallowMultipleComponent]
    public sealed class EnemyPoolMember : MonoBehaviour
    {
        private Action<EnemyPoolMember> _release;
        private bool _released;
        private Vector3 _spawnScale = Vector3.one;
        private InhaleableItem _inhaleableItem;

        public GameObject SourcePrefab { get; private set; }
        public EnemyBase Enemy { get; private set; }
        public bool IsReleased => _released;

        private void Awake()
        {
            Enemy = GetComponent<EnemyBase>();
            _inhaleableItem = GetComponent<InhaleableItem>();
            _spawnScale = transform.localScale;
        }

        public void Bind(GameObject sourcePrefab, Action<EnemyPoolMember> release)
        {
            SourcePrefab = sourcePrefab;
            _release = release;
            _released = false;
            if (Enemy == null) Enemy = GetComponent<EnemyBase>();
            if (_inhaleableItem == null) _inhaleableItem = GetComponent<InhaleableItem>();
        }

        public void SetSpawnScale(Vector3 scale)
        {
            _spawnScale = scale;
            RestoreSpawnScale();
        }

        public void RestoreSpawnScale()
        {
            transform.localScale = _spawnScale;
            _inhaleableItem?.SetRestingScale(_spawnScale);
        }

        public void MarkSpawned()
        {
            _released = false;
            RestoreSpawnScale();
        }

        public void Release()
        {
            if (_released) return;
            _released = true;
            RestoreSpawnScale();
            _release?.Invoke(this);
        }
    }
}
