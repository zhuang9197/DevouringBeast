using System;
using System.Collections.Generic;
using UnityEngine;

namespace DevouringBeast
{
    [DisallowMultipleComponent]
    public sealed class RogueSkillManager : MonoBehaviour
    {
        [SerializeField] private RogueSkillCatalog catalog;
        [SerializeField] private VoidEventChannel onLevelUp;

        private readonly Dictionary<RogueSkillId, int> _levels = new();
        private readonly List<RogueSkillDefinition> _pendingChoices = new(3);
        private SwallowContainer _container;
        private PlayerController _controller;
        private PlayerSpit _spit;
        private PlayerInhale _inhale;
        private RogueSelectionUI _selectionUI;
        private RogueSkillId? _faith;
        private bool _choiceOpen;
        private int _witchProgress;
        [SerializeField, Min(1)] private int witchSwallowsRequired = 3;

        public RogueSkillCatalog Catalog => catalog;
        public bool IsChoiceOpen => _choiceOpen;
        public bool HasFaith => _faith.HasValue;
        public RogueSkillId? ActiveFaith => _faith;
        public event Action<IReadOnlyList<RogueSkillDefinition>> OnSkillChoiceRequired;
        public event Action<RogueSkillId, int> OnSkillLevelChanged;
        public event Action<float, bool> OnWitchProgressChanged;
        public float WitchProgressNormalized => Mathf.Clamp01((float)_witchProgress / Mathf.Max(1, witchSwallowsRequired));

        private void Awake()
        {
            if (catalog == null) catalog = Resources.Load<RogueSkillCatalog>("Rogue/RogueSkillCatalog");
            _container = GetComponent<SwallowContainer>();
            _controller = GetComponent<PlayerController>();
            _spit = GetComponent<PlayerSpit>();
            _inhale = GetComponent<PlayerInhale>();
        }

        private void Start()
        {
            if (_container != null) _container.OnLevelUpCheck += CheckLevelUp;
            EnemyBase.OnAnyEnemyDeath += HandleEnemyDeath;
            RestoreFromActiveSave();
        }

        private void OnDestroy()
        {
            if (_container != null) _container.OnLevelUpCheck -= CheckLevelUp;
            EnemyBase.OnAnyEnemyDeath -= HandleEnemyDeath;
            if (_choiceOpen && GameManager.Instance.CurrentState == GameState.RogueChoosing)
                GameManager.Instance.ExitRogueSelection();
        }

        private void CheckLevelUp()
        {
            if (!GameManager.Instance.IsPlaying || _choiceOpen || _container == null || !_container.CanLevelUp || catalog == null) return;
            RogueSchool preferred = MapSchool(_container.GetDominantTag());
            List<RogueSkillDefinition> choices = GetAvailableChoices(preferred, 3);
            if (choices.Count == 0) choices = GetAvailableChoices(RogueSchool.Normal, 3);
            if (choices.Count == 0) return;

            _pendingChoices.Clear();
            _pendingChoices.AddRange(choices);
            _choiceOpen = true;
            AudioManager.Instance.PlaySfx(AudioCue.LevelUp);
            GameManager.Instance.EnterRogueSelection();
            onLevelUp?.RaiseEvent();
            _selectionUI = RogueSelectionUI.Show(this, catalog, choices);
            OnSkillChoiceRequired?.Invoke(choices);
        }

        public void SelectSkill(RogueSkillDefinition skill)
        {
            if (!_choiceOpen || skill == null || !_pendingChoices.Contains(skill)) return;
            AddSkill(skill.id);
            _container.ResetForLevelUp();
            _pendingChoices.Clear();
            _choiceOpen = false;
            GameManager.Instance.ExitRogueSelection();
            if (_selectionUI != null) Destroy(_selectionUI.gameObject);
            _selectionUI = null;
            SaveProgress();
            if (_container.CanLevelUp) _container.CheckAndNotify();
        }

        public void AddSkill(RogueSkillId id, int amount = 1, bool apply = true)
        {
            RogueSkillDefinition definition = catalog != null ? catalog.Get(id) : null;
            if (definition == null || amount <= 0) return;
            int current = GetLevel(id);
            int next = definition.maxLevel <= 0 ? current + amount : Mathf.Min(definition.maxLevel, current + amount);
            if (next == current) return;

            if (definition.mythic && !_faith.HasValue)
            {
                _faith = id;
                if (id == RogueSkillId.FaithAngel) ClearNonFaithSkills();
            }

            _levels[id] = next;
            if (apply) ApplyAllPlayerModifiers();
            OnSkillLevelChanged?.Invoke(id, next);
        }

        private void ClearNonFaithSkills()
        {
            List<RogueSkillId> remove = new();
            foreach (RogueSkillId id in _levels.Keys)
            {
                RogueSkillDefinition definition = catalog.Get(id);
                if (definition != null && definition.school != RogueSchool.Faith) remove.Add(id);
            }
            foreach (RogueSkillId id in remove) _levels.Remove(id);
        }

        public void AddSkill(RogueSkillData legacySkill)
        {
            if (legacySkill == null || catalog == null) return;
            foreach (RogueSkillDefinition definition in catalog.skills)
            {
                if (definition != null && string.Equals(definition.displayName, legacySkill.skillName, StringComparison.OrdinalIgnoreCase))
                {
                    AddSkill(definition.id);
                    return;
                }
            }
        }

        public void SelectSkill(RogueSkillData legacySkill) => AddSkill(legacySkill);

        public List<RogueSkillData> GetAvailableChoices(ItemTag tag, int count)
        {
            // 旧 UI 的兼容入口；新 UI 使用 RogueSkillDefinition。
            return new List<RogueSkillData>();
        }

        public int GetLevel(RogueSkillId id) => _levels.TryGetValue(id, out int level) ? level : 0;
        public bool Has(RogueSkillId id) => GetLevel(id) > 0;
        public int GetOwnedSkillLevel(string nameFragment)
        {
            if (string.IsNullOrWhiteSpace(nameFragment) || catalog == null) return 0;
            foreach (RogueSkillDefinition skill in catalog.skills)
                if (skill != null && skill.displayName.IndexOf(nameFragment, StringComparison.OrdinalIgnoreCase) >= 0)
                    return GetLevel(skill.id);
            return 0;
        }

        public EnergyBallShotSnapshot CreateEnergyBallSnapshot(float damage, float speed, float maxDistance,
            float damageMultiplier = 1f, bool isSplitProjectile = false)
        {
            return new EnergyBallShotSnapshot(damage * damageMultiplier, speed, maxDistance,
                new Dictionary<RogueSkillId, int>(_levels), isSplitProjectile);
        }

        public List<RogueSkillDefinition> GetAvailableChoices(RogueSchool preferred, int count)
        {
            if (_faith == RogueSkillId.FaithAngel)
            {
                RogueSkillDefinition angel = catalog.Get(RogueSkillId.FaithAngel);
                return angel != null && CanOffer(angel)
                    ? new List<RogueSkillDefinition> { angel }
                    : new List<RogueSkillDefinition>();
            }
            List<RogueSkillDefinition> preferredPool = new();
            List<RogueSkillDefinition> normalPool = new();
            foreach (RogueSkillDefinition skill in catalog.skills)
            {
                if (!CanOffer(skill)) continue;
                if (skill.school == preferred) preferredPool.Add(skill);
                if (skill.school == RogueSchool.Normal) normalPool.Add(skill);
            }

            Shuffle(preferredPool);
            Shuffle(normalPool);
            List<RogueSkillDefinition> result = new(count);
            AddUnique(result, preferredPool, count);
            AddUnique(result, normalPool, count);
            return result;
        }

        private bool CanOffer(RogueSkillDefinition skill)
        {
            if (skill == null || skill.IsMaxLevel(GetLevel(skill.id))) return false;
            if (_faith.HasValue)
            {
                if (_faith.Value == RogueSkillId.FaithAngel)
                    return skill.id == RogueSkillId.FaithAngel;
                if (skill.school == RogueSchool.Faith)
                    return skill.id == _faith.Value;
            }
            foreach (RogueSkillId prerequisite in skill.prerequisites)
            {
                int level = GetLevel(prerequisite);
                if (level <= 0) return false;
                if (skill.requiresMaxPrerequisites)
                {
                    RogueSkillDefinition prerequisiteData = catalog.Get(prerequisite);
                    if (prerequisiteData != null && !prerequisiteData.IsMaxLevel(level)) return false;
                }
            }
            return true;
        }

        private List<RogueSkillDefinition> AllAvailable()
        {
            List<RogueSkillDefinition> result = new();
            foreach (RogueSkillDefinition skill in catalog.skills) if (CanOffer(skill)) result.Add(skill);
            Shuffle(result);
            return result;
        }

        private static void AddUnique(List<RogueSkillDefinition> target, List<RogueSkillDefinition> source, int count)
        {
            for (int i = 0; i < source.Count && target.Count < count; i++)
                if (!target.Contains(source[i])) target.Add(source[i]);
        }

        private static void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        private void ApplyAllPlayerModifiers()
        {
            if (_controller != null)
            {
                _controller.SkillMoveSpeedMultiplier = 1f + GetLevel(RogueSkillId.NormalFast) * 0.01f;
                _controller.HasInhaleWalkSkill = Has(RogueSkillId.EvolutionWing);
                _controller.SetWitchFormEnabled(Has(RogueSkillId.FaithWitch), GetLevel(RogueSkillId.FaithWitch), catalog);
            }
            OnWitchProgressChanged?.Invoke(WitchProgressNormalized, Has(RogueSkillId.FaithWitch));
            if (_inhale != null)
            {
                _inhale.SkillSuctionMultiplier = (1f + GetLevel(RogueSkillId.NormalSuction) * 0.01f) *
                    (Has(RogueSkillId.FaithDemon) ? 2f + 0.1f * GetLevel(RogueSkillId.FaithDemon) : 1f);
                _inhale.SkillDamageMultiplier = _inhale.SkillSuctionMultiplier;
                _inhale.BonusInhaleDuration = Has(RogueSkillId.EvolutionBigMouth)
                    ? 3f + Mathf.Max(0, GetLevel(RogueSkillId.EvolutionBigMouth) - 1) * 2f : 0f;
                _inhale.DamageOnlyMode = Has(RogueSkillId.FaithDemon);
            }
            if (_spit != null) _spit.RefreshSkillModifiers();
        }

        private static RogueSchool MapSchool(ItemTag tag) => tag switch
        {
            ItemTag.Poison => RogueSchool.Poison,
            ItemTag.Fire => RogueSchool.Fire,
            ItemTag.Evolution => RogueSchool.Evolution,
            ItemTag.Superpower => RogueSchool.Superpower,
            ItemTag.Faith => RogueSchool.Faith,
            _ => RogueSchool.Normal
        };

        private void RestoreFromActiveSave()
        {
            SaveSlotData save = SaveGameService.GetActiveSlot();
            if (save?.rogueSkills != null)
            {
                foreach (RogueSkillSaveEntry entry in save.rogueSkills)
                    if (Enum.TryParse(entry.id, out RogueSkillId id)) AddSkill(id, entry.level, false);
            }
            ApplyAllPlayerModifiers();
        }

        public void SaveProgress()
        {
            List<RogueSkillSaveEntry> entries = new();
            foreach (var pair in _levels)
                entries.Add(new RogueSkillSaveEntry { id = pair.Key.ToString(), level = pair.Value });
            SaveGameService.SaveRogueSkills(entries);
        }

        public void NotifySwallow()
        {
            if (!Has(RogueSkillId.FaithWitch)) return;
            _witchProgress++;
            OnWitchProgressChanged?.Invoke(WitchProgressNormalized, true);
            if (_witchProgress < witchSwallowsRequired) return;
            _witchProgress = 0;
            _controller?.EnterBeastForm();
            OnWitchProgressChanged?.Invoke(0f, true);
        }

        private void HandleEnemyDeath(EnemyBase enemy)
        {
            if (!_faith.HasValue || _container == null) return;
            if (_faith.Value != RogueSkillId.FaithAngel && _faith.Value != RogueSkillId.FaithDemon) return;
            float progress = enemy != null && enemy.Data != null ? Mathf.Max(1f, enemy.Data.killMass) : 5f;
            _container.AddProgress(progress);
        }
    }
}
