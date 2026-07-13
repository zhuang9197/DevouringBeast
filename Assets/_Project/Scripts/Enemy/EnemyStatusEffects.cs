using UnityEngine;

namespace DevouringBeast
{
    /// <summary>
    /// 敌人身上的轻量状态容器。目前负责合并和刷新能量球施加的中毒效果。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyStatusEffects : MonoBehaviour
    {
        private const float PoisonTickInterval = 0.25f;

        private EnemyBase _enemy;
        private float _poisonDamagePerSecond;
        private float _poisonEndTime;
        private float _nextPoisonTickTime;

        private void Awake()
        {
            _enemy = GetComponent<EnemyBase>();
        }

        public void ApplyPoison(float damagePerSecond, float duration)
        {
            if (_enemy == null || _enemy.IsDead || damagePerSecond <= 0f || duration <= 0f)
                return;

            _poisonDamagePerSecond = Mathf.Max(_poisonDamagePerSecond, damagePerSecond);
            _poisonEndTime = Mathf.Max(_poisonEndTime, Time.time + duration);
            _nextPoisonTickTime = Mathf.Min(_nextPoisonTickTime <= 0f ? Time.time : _nextPoisonTickTime, Time.time);
            enabled = true;
        }

        private void Update()
        {
            if (_enemy == null || _enemy.IsDead || Time.time >= _poisonEndTime)
            {
                ClearPoison();
                return;
            }

            if (Time.time < _nextPoisonTickTime)
                return;

            _enemy.TakeDamage(_poisonDamagePerSecond * PoisonTickInterval);
            _nextPoisonTickTime = Time.time + PoisonTickInterval;
        }

        private void ClearPoison()
        {
            _poisonDamagePerSecond = 0f;
            _poisonEndTime = 0f;
            _nextPoisonTickTime = 0f;
            enabled = false;
        }
    }
}
