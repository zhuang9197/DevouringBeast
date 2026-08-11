using System.Collections;
using UnityEngine;

namespace DevouringBeast
{
    [DisallowMultipleComponent]
    public sealed class PlayerStatusEffects : MonoBehaviour
    {
        private PlayerHealth _health;
        private Coroutine _poisonRoutine;

        public static PlayerStatusEffects Ensure(PlayerHealth health)
        {
            if (health == null) return null;
            PlayerStatusEffects effects = health.GetComponent<PlayerStatusEffects>();
            return effects != null ? effects : health.gameObject.AddComponent<PlayerStatusEffects>();
        }

        private void Awake() => _health = GetComponent<PlayerHealth>();

        public void ApplyPoison(int damage, float interval, float duration)
        {
            if (_poisonRoutine != null) StopCoroutine(_poisonRoutine);
            _poisonRoutine = StartCoroutine(PoisonRoutine(Mathf.Max(1, damage), interval, duration));
        }

        private IEnumerator PoisonRoutine(int damage, float interval, float duration)
        {
            float remaining = duration;
            while (remaining > 0f)
            {
                float wait = Mathf.Min(Mathf.Max(0.1f, interval), remaining);
                yield return new WaitForSeconds(wait);
                remaining -= wait;
                if (_health == null || _health.IsDead) break;
                _health.TakeDamage(damage);
            }
            _poisonRoutine = null;
        }
    }
}
