using System.Collections.Generic;
using UnityEngine;

namespace DevouringBeast
{
    [DisallowMultipleComponent]
    public sealed class EnemyStatusEffects : MonoBehaviour
    {
        [SerializeField, Min(1)] private int erosionMaxStacks = 3;
        [SerializeField] private float iconSwitchInterval = 0.65f;
        [SerializeField] private float iconBlinkSpeed = 7f;
        [SerializeField, Min(0.1f)] private float statusIconScale = 0.95f;
        [SerializeField] private float erosionOrbitSpeed = 120f;
        [SerializeField, Min(0.2f)] private float erosionOrbitRadius = 1.05f;
        [SerializeField, Min(0.1f)] private float erosionIconScale = 0.75f;

        private EnemyBase _enemy;
        private EnemyAI _ai;
        private SpriteRenderer _statusRenderer;
        private readonly List<SpriteRenderer> _erosionRenderers = new();
        private RogueSkillCatalog _catalog;
        private float _poisonDps, _poisonEnd, _poisonTick;
        private int _poisonStacks;
        private float _burnDps, _burnEnd, _burnTick;
        private float _slowPercent, _slowEnd;
        private float _stunEnd;
        private int _erosionStacks;
        private float _nextIconSwitch;
        private int _iconIndex;

        public bool IsPoisoned => Time.time < _poisonEnd;
        public bool IsBurning => Time.time < _burnEnd;
        public bool IsSlowed => Time.time < _slowEnd;
        public bool IsStunned => Time.time < _stunEnd;
        public int ErosionStacks => _erosionStacks;
        public float StatusIconScale => statusIconScale;
        public float ErosionIconScale => erosionIconScale;

        private void Awake()
        {
            _enemy = GetComponent<EnemyBase>();
            _ai = GetComponent<EnemyAI>();
            _catalog = Resources.Load<RogueSkillCatalog>("Rogue/RogueSkillCatalog");
            CreateStatusVisual();
        }

        public static EnemyStatusEffects EnsureFor(EnemyBase enemy)
        {
            EnemyStatusEffects status = enemy.GetComponent<EnemyStatusEffects>();
            return status != null ? status : enemy.gameObject.AddComponent<EnemyStatusEffects>();
        }

        public void ApplyPoison(float dps, float duration)
        {
            if (!CanApply(dps, duration)) return;
            _poisonStacks++;
            _poisonDps += dps;
            _poisonEnd = Time.time + duration;
            _poisonTick = Mathf.Min(_poisonTick <= 0f ? Time.time : _poisonTick, Time.time);
        }

        public void ApplyBurn(float baseDps, float duration, float growthPerHit)
        {
            if (!CanApply(baseDps, duration)) return;
            if (IsBurning) _burnDps *= 1f + Mathf.Max(0f, growthPerHit);
            else
            {
                _burnDps = baseDps;
                _burnEnd = Time.time + duration;
                _burnTick = Time.time;
            }
        }

        public void ApplySlow(float percent, float duration)
        {
            if (!CanApply(percent, duration)) return;
            _slowPercent = Mathf.Max(_slowPercent, Mathf.Clamp(percent, 0f, 0.9f));
            _slowEnd = Mathf.Max(_slowEnd, Time.time + duration);
            RefreshMovementModifier();
        }

        public void ApplyStun(float duration)
        {
            if (_enemy == null || _enemy.IsDead || duration <= 0f) return;
            _stunEnd = Mathf.Max(_stunEnd, Time.time + duration);
            RefreshMovementModifier();
        }

        public float ApplyErosion(float incomingDamage, int requestedMaxStacks,
            float detonationMultiplier, float missingHealthPercent)
        {
            erosionMaxStacks = Mathf.Max(1, requestedMaxStacks);
            if (_erosionStacks >= erosionMaxStacks)
            {
                float missingHealth = Mathf.Max(0f, _enemy.MaxHealth - _enemy.CurrentHealth);
                float bonusDamage = incomingDamage * detonationMultiplier + missingHealth * missingHealthPercent;
                ClearErosion();
                return bonusDamage;
            }
            _erosionStacks++;
            CreateErosionVisual();
            return 0f;
        }

        private bool CanApply(float amount, float duration) =>
            _enemy != null && !_enemy.IsDead && amount > 0f && duration > 0f;

        private void Update()
        {
            if (_enemy == null || _enemy.IsDead) { HideVisuals(); return; }
            TickDamage(ref _poisonTick, _poisonEnd, _poisonDps);
            TickDamage(ref _burnTick, _burnEnd, _burnDps);
            if (!IsSlowed && _slowPercent > 0f) { _slowPercent = 0f; RefreshMovementModifier(); }
            if (!IsStunned && _ai != null && _ai.IsStatusStunned) RefreshMovementModifier();
            UpdateStatusIcon();
            UpdateErosionOrbit();
            if (!IsPoisoned && _poisonStacks > 0) { _poisonStacks = 0; _poisonDps = 0f; }
            if (!IsBurning && _burnDps > 0f) _burnDps = 0f;
        }

        private void TickDamage(ref float nextTick, float endTime, float dps)
        {
            if (Time.time >= endTime || dps <= 0f || Time.time < nextTick) return;
            _enemy.TakeDamage(dps);
            nextTick = Time.time + 1f;
        }

        private void RefreshMovementModifier()
        {
            if (_ai != null) _ai.SetStatusModifiers(1f - _slowPercent, IsStunned);
        }

        private void CreateStatusVisual()
        {
            GameObject icon = new("StatusIcon", typeof(SpriteRenderer));
            icon.transform.SetParent(transform, false);
            icon.transform.localPosition = new Vector3(0f, 1.75f, 0f);
            icon.transform.localScale = Vector3.one * statusIconScale;
            _statusRenderer = icon.GetComponent<SpriteRenderer>();
            _statusRenderer.sortingOrder = 110;
            _statusRenderer.enabled = false;
        }

        private void UpdateStatusIcon()
        {
            if (_catalog == null || _statusRenderer == null) return;
            Sprite[] active = new Sprite[4];
            int count = 0;
            if (IsPoisoned) active[count++] = _catalog.poisoningIcon;
            if (IsBurning) active[count++] = _catalog.burnIcon;
            if (IsSlowed) active[count++] = _catalog.slowdownIcon;
            if (IsStunned) active[count++] = _catalog.dizzinessIcon;
            if (count == 0) { _statusRenderer.enabled = false; return; }
            if (Time.unscaledTime >= _nextIconSwitch)
            {
                _iconIndex = (_iconIndex + 1) % count;
                _nextIconSwitch = Time.unscaledTime + iconSwitchInterval;
            }
            _statusRenderer.sprite = active[_iconIndex % count];
            _statusRenderer.enabled = Mathf.Sin(Time.unscaledTime * iconBlinkSpeed) > -0.35f;
            _statusRenderer.transform.rotation = Quaternion.identity;
        }

        private void CreateErosionVisual()
        {
            if (_catalog == null || _catalog.erosionIcon == null) return;
            GameObject icon = new("Erosion_" + _erosionStacks, typeof(SpriteRenderer));
            icon.transform.SetParent(transform, false);
            icon.transform.localScale = Vector3.one * erosionIconScale;
            SpriteRenderer renderer = icon.GetComponent<SpriteRenderer>();
            renderer.sprite = _catalog.erosionIcon;
            renderer.sortingOrder = 109;
            _erosionRenderers.Add(renderer);
        }

        private void UpdateErosionOrbit()
        {
            int count = _erosionRenderers.Count;
            for (int i = 0; i < count; i++)
            {
                SpriteRenderer renderer = _erosionRenderers[i];
                if (renderer == null) continue;
                float angle = Time.time * erosionOrbitSpeed + i * 360f / Mathf.Max(1, count);
                renderer.transform.localPosition = Quaternion.Euler(0f, 0f, angle) * Vector3.right * erosionOrbitRadius;
                renderer.transform.rotation = Quaternion.identity;
            }
        }

        private void ClearErosion()
        {
            _erosionStacks = 0;
            foreach (SpriteRenderer renderer in _erosionRenderers) if (renderer != null) Destroy(renderer.gameObject);
            _erosionRenderers.Clear();
        }

        private void HideVisuals()
        {
            if (_statusRenderer != null) _statusRenderer.enabled = false;
            foreach (SpriteRenderer renderer in _erosionRenderers) if (renderer != null) renderer.enabled = false;
        }

        public void ResetForReuse()
        {
            _poisonDps = _poisonEnd = _poisonTick = 0f;
            _poisonStacks = 0;
            _burnDps = _burnEnd = _burnTick = 0f;
            _slowPercent = _slowEnd = _stunEnd = 0f;
            _nextIconSwitch = 0f;
            _iconIndex = 0;
            ClearErosion();
            if (_statusRenderer != null) _statusRenderer.enabled = false;
            RefreshMovementModifier();
        }
    }
}
