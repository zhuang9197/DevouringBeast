using System.Collections;
using UnityEngine;

namespace DevouringBeast
{
    /// <summary>
    /// 可吸入物品 — 挂载在可吸入物体上的组件
    /// 存活时无法直接吸入，吸力会转为伤害；阵亡后吸力≥deadThreshold 才能吸入
    /// </summary>
    public class InhaleableItem : MonoBehaviour
    {
        [field: SerializeField] public float Mass { get; set; } = 10f;

        [Tooltip("阵亡后的吸入阈值（很小）")]
        [field: SerializeField] public float DeadInhaleThreshold { get; set; } = 10f;

        /// <summary>兼容旧代码：存活阈值已废弃</summary>
        [field: SerializeField] public float AliveInhaleThreshold { get; set; } = 0f;

        /// <summary>是否存活</summary>
        public bool IsAlive { get; set; } = true;

        [field: SerializeField] public bool IgnoreSuctionThreshold { get; set; }

        /// <summary>当前吸入阈值：存活时无穷大（无法吸入），阵亡后为 deadThreshold</summary>
        public float CurrentThreshold => IsAlive ? float.MaxValue : DeadInhaleThreshold;

        /// <summary>是否正在被吸入（飞向口部）</summary>
        public bool IsBeingInhaled { get; private set; }
        public bool IsStoredInMouth { get; private set; }
        private Vector3 _restingScale;
        private Collider2D[] _colliders;
        private Collider2D[] _ignoredPlayerColliders;
        private bool _suctionCollisionIgnored;
        private int _lastSuctionFrame = -1;

        private void Awake()
        {
            _restingScale = transform.localScale;
            _colliders = GetComponentsInChildren<Collider2D>(true);
        }

        /// <summary>
        /// Temporarily removes only player-vs-item collision while suction is actively
        /// pulling this item. Pushable physics remains enabled at all other times.
        /// </summary>
        public void PrepareForSuction(Collider2D[] playerColliders)
        {
            _lastSuctionFrame = Time.frameCount;
            if (_suctionCollisionIgnored || playerColliders == null || playerColliders.Length == 0)
                return;

            if (_colliders == null || _colliders.Length == 0)
                _colliders = GetComponentsInChildren<Collider2D>(true);
            _ignoredPlayerColliders = playerColliders;
            foreach (Collider2D itemCollider in _colliders)
            {
                if (itemCollider == null) continue;
                foreach (Collider2D playerCollider in playerColliders)
                {
                    if (playerCollider != null)
                        Physics2D.IgnoreCollision(itemCollider, playerCollider, true);
                }
            }
            _suctionCollisionIgnored = true;
        }

        private void LateUpdate()
        {
            if (_suctionCollisionIgnored && _lastSuctionFrame != Time.frameCount)
                RestoreSuctionCollision();
        }

        private void RestoreSuctionCollision()
        {
            if (!_suctionCollisionIgnored) return;
            if (_colliders == null || _colliders.Length == 0)
                _colliders = GetComponentsInChildren<Collider2D>(true);
            if (_ignoredPlayerColliders != null)
            {
                foreach (Collider2D itemCollider in _colliders)
                {
                    if (itemCollider == null) continue;
                    foreach (Collider2D playerCollider in _ignoredPlayerColliders)
                        if (playerCollider != null) Physics2D.IgnoreCollision(itemCollider, playerCollider, false);
                }
            }
            _ignoredPlayerColliders = null;
            _suctionCollisionIgnored = false;
            _lastSuctionFrame = -1;
        }

        public void SetRestingScale(Vector3 scale)
        {
            _restingScale = scale;
            transform.localScale = scale;
        }

        /// <summary>被吸入时调用 — 启动缩小+飞行动画</summary>
        public void OnInhaled(Transform mouthTransform)
        {
            if (IsBeingInhaled) return;
            IsBeingInhaled = true;
            IsStoredInMouth = true;

            // 通知 EnemyBase 死亡
            var enemy = GetComponent<EnemyBase>();
            if (enemy != null && !enemy.IsDead)
            {
                enemy.TakeDamage(float.MaxValue);
            }

            // 停止 AI 和碰撞
            var ai = GetComponent<EnemyAI>();
            if (ai != null) ai.enabled = false;

            RestoreSuctionCollision();
            Collider2D col = GetComponent<Collider2D>();
            if (col != null) col.enabled = false;
            Rigidbody2D body = GetComponent<Rigidbody2D>();
            if (body != null) body.simulated = false;

            StartCoroutine(InhaleFlyRoutine(mouthTransform));
        }

        /// <summary>
        /// 缩小 + 飞向口部 + 消失
        /// </summary>
        private IEnumerator InhaleFlyRoutine(Transform mouth)
        {
            float duration = 0.4f;
            float elapsed = 0f;
            Vector3 startPos = transform.position;
            Vector3 startScale = transform.localScale;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                // 飞向口部
                if (mouth != null)
                    transform.position = Vector3.Lerp(startPos, mouth.position, t);

                // 逐渐缩小
                transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);

                yield return null;
            }

            // 恢复 scale 供对象池复用
            transform.localScale = _restingScale;
            IsBeingInhaled = false;
            gameObject.SetActive(false);
        }

        public void ReleaseFromMouth()
        {
            IsStoredInMouth = false;
            EnemyPoolMember poolMember = GetComponent<EnemyPoolMember>();
            if (poolMember != null) poolMember.Release();
            else
            {
                WorldItemPoolMember worldItem = GetComponent<WorldItemPoolMember>();
                if (worldItem != null) worldItem.Release();
                else
                {
                    EnemyRewardChest chest = GetComponent<EnemyRewardChest>();
                    if (chest != null)
                    {
                        chest.Release();
                        return;
                    }
                    BloodDrop bloodDrop = GetComponent<BloodDrop>();
                    if (bloodDrop != null) bloodDrop.Release();
                    else Destroy(gameObject);
                }
            }
        }

        public void ResetForReuse()
        {
            StopAllCoroutines();
            RestoreSuctionCollision();
            transform.localScale = _restingScale;
            IsBeingInhaled = false;
            IsStoredInMouth = false;
            Collider2D col = GetComponent<Collider2D>();
            if (col != null) col.enabled = true;
            Rigidbody2D body = GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.simulated = true;
                body.velocity = Vector2.zero;
                body.angularVelocity = 0f;
            }
        }

        private void OnDisable() => RestoreSuctionCollision();
    }
}
