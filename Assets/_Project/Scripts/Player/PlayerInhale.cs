using System;
using System.Collections.Generic;
using UnityEngine;

namespace DevouringBeast
{
    /// <summary>
    /// PlayerInhale — 玩家吸入逻辑
    /// 处理锥形范围检测、进度条、吸力计算
    /// </summary>
    public class PlayerInhale : MonoBehaviour
    {
        [Header("吸入参数")]
        [SerializeField] private float inhaleAngle = 60f;
        [SerializeField] private float inhaleRadius = 5f;
        [SerializeField] private float maxInhaleDuration = 3f;
        [SerializeField] private float maxSuctionForce = 100f;
        [SerializeField] private float suctionRampTime = 1.5f;
        [SerializeField, Min(0.05f)] private float intakeDistance = 0.65f;
        [SerializeField, Min(0.1f)] private float minimumPullSpeed = 1f;
        [SerializeField, Min(0.1f)] private float maximumPullSpeed = 16f;
        [SerializeField, Min(0f)] private float suctionMassSpeedFactor = 0.35f;
        [SerializeField, Range(1f, 1.5f)] private float aliveEnemyMaxSpeedBoost = 1.25f;

        [Header("层级")]
        [SerializeField] private LayerMask inhaleableLayer;

        [Header("事件")]
        [SerializeField] private VoidEventChannel onInhaleStart;
        [SerializeField] private VoidEventChannel onInhaleStop;
        [SerializeField] private VoidEventChannel onItemInhaled;

        // 组件引用
        private PlayerController _playerController;
        private SwallowContainer _container;

        // 状态
        private bool _isInhaling;
        private float _inhaleTimer;
        private float _currentSuctionForce;
        private bool _suctionMaxed;
        private readonly List<InhaleableItem> _detectedItems = new(8);
        private float _skillSuctionMultiplier = 1f;
        private float _skillDamageMultiplier = 1f;
        private float _bonusInhaleDuration;
        private bool _damageOnlyMode;

        // 属性
        public bool IsInhaling => _isInhaling;
        public float Progress => _isInhaling ? _inhaleTimer / (maxInhaleDuration + _bonusInhaleDuration) : 0f;
        public float CurrentSuctionForce => _currentSuctionForce;
        public float MaxSuctionForce
        {
            get => maxSuctionForce;
            set => maxSuctionForce = value;
        }
        public float MaxInhaleDuration
        {
            get => maxInhaleDuration;
            set => maxInhaleDuration = value;
        }
        public float SkillSuctionMultiplier { get => _skillSuctionMultiplier; set => _skillSuctionMultiplier = Mathf.Max(0f, value); }
        public float SkillDamageMultiplier { get => _skillDamageMultiplier; set => _skillDamageMultiplier = Mathf.Max(0f, value); }
        public float BonusInhaleDuration { get => _bonusInhaleDuration; set => _bonusInhaleDuration = Mathf.Max(0f, value); }
        public bool DamageOnlyMode { get => _damageOnlyMode; set => _damageOnlyMode = value; }

        public event Action<float> OnProgressChanged;
        public event Action<bool> OnSuctionMaxedChanged; // true=达到最大, false=未达到

        private void Awake()
        {
            _playerController = GetComponent<PlayerController>();
            _container = GetComponent<SwallowContainer>();
            if (_container == null) _container = gameObject.AddComponent<SwallowContainer>();
        }

        private void Update()
        {
            if (!_isInhaling) return;

            _inhaleTimer += Time.deltaTime;

            // 计算吸力：前半段递增，后半段满
            float rampProgress = Mathf.Clamp01(_inhaleTimer / suctionRampTime);
            _currentSuctionForce = maxSuctionForce * _skillSuctionMultiplier * rampProgress;

            // 通知吸力是否达到最大
            bool maxed = rampProgress >= 1f;
            if (maxed != _suctionMaxed)
            {
                _suctionMaxed = maxed;
                _playerController.SetSuctionMaxed(maxed);
                OnSuctionMaxedChanged?.Invoke(maxed);
            }

            OnProgressChanged?.Invoke(Progress);

            // 检测吸入范围内的物品
            DetectItems();

            // 处理吸入/拉近
            ProcessSuction();

            // 进度条耗尽自动停止
            if (_inhaleTimer >= maxInhaleDuration + _bonusInhaleDuration)
            {
                StopInhale();
            }
        }

        /// <summary>
        /// 开始吸入
        /// </summary>
public void StartInhale()
        {
            if (_isInhaling) return;

            _isInhaling = true;
            _inhaleTimer = 0f;
            _currentSuctionForce = 0f;
            _suctionMaxed = false;
            _playerController.IsInhaling = true;
            _playerController.SetSuctionMaxed(false);
            AudioManager.Instance.PlayLoop(AudioCue.Suck);
            onInhaleStart?.RaiseEvent();
        }

        /// <summary>
        /// 停止吸入
        /// </summary>
public void StopInhale()
        {
            if (!_isInhaling) return;

            _isInhaling = false;
            _playerController.IsInhaling = false;
            AudioManager.Instance.StopLoop(AudioCue.Suck);
            onInhaleStop?.RaiseEvent();
            OnProgressChanged?.Invoke(0f);
        }

        /// <summary>
        /// 锥形范围检测可吸入物品
        /// </summary>
        private void DetectItems()
        {
            _detectedItems.Clear();
            var hits = Physics2D.OverlapCircleAll(transform.position, inhaleRadius, inhaleableLayer);

            foreach (var hit in hits)
            {
                var item = hit.GetComponentInParent<InhaleableItem>();
                if (item == null || _detectedItems.Contains(item)) continue;

                // 角度过滤：只检测前方锥形范围
                Vector2 dirToTarget = (item.transform.position - transform.position).normalized;
                float angle = Vector2.Angle(_playerController.FacingDirection, dirToTarget);
                if (angle <= inhaleAngle * 0.5f)
                    _detectedItems.Add(item);
            }
        }

        [Header("吸力伤害")]
        [Tooltip("吸力转为伤害的系数（force * 此值 = 每秒伤害）")]
        [SerializeField] private float suctionDamageMultiplier = 0.5f;

        /// <summary>
        /// 处理吸力：存活敌人转为伤害+拉近，阵亡后吸入
        /// </summary>
        private void ProcessSuction()
        {
            float dt = Time.deltaTime;

            foreach (var item in _detectedItems)
            {
                if (item == null || item.IsBeingInhaled) continue;

                if (item.IsAlive)
                {
                    // 存活：吸力转为伤害
                    var enemy = item.GetComponent<EnemyBase>();
                    if (enemy != null && !enemy.IsDead)
                    {
                        float damage = _currentSuctionForce * suctionDamageMultiplier * _skillDamageMultiplier * dt;
                        enemy.TakeDamage(damage);
                        EnemyAI ai = enemy.GetComponent<EnemyAI>();
                        if (ai != null)
                        {
                            float ratio = _currentSuctionForce / Mathf.Max(1f, item.Mass * 10f);
                            ai.ApplySuctionChaseBoost(Mathf.Lerp(1f, aliveEnemyMaxSpeedBoost, Mathf.Clamp01(ratio)));
                        }
                    }
                    if (_damageOnlyMode) continue;
                }
                else
                {
                    if (_damageOnlyMode) continue;
                    float distance = Vector2.Distance(item.transform.position, transform.position);
                    if (_currentSuctionForce >= item.CurrentThreshold && distance <= intakeDistance)
                    {
                        InhaleItem(item);
                    }
                    else
                    {
                        PullItem(item);
                    }
                }
            }
        }

        private void InhaleItem(InhaleableItem item)
        {
            _container.AddItem(item);
            item.OnInhaled(transform); // 传入玩家位置作为口部
            onItemInhaled?.RaiseEvent();
        }

        private void PullItem(InhaleableItem item)
        {
            float thresholdRatio = _currentSuctionForce / Mathf.Max(1f, item.CurrentThreshold);
            float massRatio = _currentSuctionForce / Mathf.Max(1f, item.Mass);
            float pullStrength = minimumPullSpeed + (thresholdRatio + massRatio) * suctionMassSpeedFactor;
            pullStrength = Mathf.Clamp(pullStrength, minimumPullSpeed, maximumPullSpeed);
            item.transform.position = Vector2.MoveTowards(
                item.transform.position,
                transform.position,
                pullStrength * Time.deltaTime
            );
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying) return;
            Gizmos.color = _isInhaling ? Color.cyan : Color.gray;
            Vector3 dir = _playerController != null
                ? (Vector3)(Vector2)_playerController.FacingDirection
                : Vector3.down;

            Vector3 origin = transform.position;
            float halfAngle = inhaleAngle * 0.5f * Mathf.Deg2Rad;
            Vector3 left = Quaternion.Euler(0, 0, -inhaleAngle * 0.5f) * dir * inhaleRadius;
            Vector3 right = Quaternion.Euler(0, 0, inhaleAngle * 0.5f) * dir * inhaleRadius;

            Gizmos.DrawLine(origin, origin + left);
            Gizmos.DrawLine(origin, origin + right);
            UnityEditor.Handles.DrawWireArc(origin, Vector3.forward, left, inhaleAngle, inhaleRadius);
        }
#endif
    }
}
