using System;
using System.Collections.Generic;
using UnityEngine;

namespace DevouringBeast
{
    [DisallowMultipleComponent]
    public sealed class RogueSkillManager : MonoBehaviour
    {
        public static RogueSkillManager Active { get; private set; }

        [SerializeField] private RogueSkillCatalog catalog;
        [SerializeField] private VoidEventChannel onLevelUp;

        private readonly Dictionary<RogueSkillId, int> _levels = new();
        private readonly List<RogueSkillDefinition> _pendingChoices = new(3);
        private SwallowContainer _container;
        private PlayerController _controller;
        private PlayerSpit _spit;
        private PlayerInhale _inhale;
        private PlayerBaseAttributes _baseAttributes;
        private RogueSelectionUI _selectionUI;
        private RogueSkillId? _faith;
        private bool _choiceOpen;
        private bool _choiceFromStatue;
        private int _witchProgress;
        [SerializeField, Min(1)] private int witchSwallowsRequired = 3;
        private const float FaithKillMassMultiplier = 2f;

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
            Active = this;
            if (catalog == null) catalog = Resources.Load<RogueSkillCatalog>("Rogue/RogueSkillCatalog");
            _container = GetComponent<SwallowContainer>();
            _controller = GetComponent<PlayerController>();
            _spit = GetComponent<PlayerSpit>();
            _inhale = GetComponent<PlayerInhale>();
            _baseAttributes = GetComponent<PlayerBaseAttributes>();
        }

        private void Start()
        {
            if (_container != null) _container.OnLevelUpCheck += CheckLevelUp;
            EnemyBase.OnAnyEnemyDeath += HandleEnemyDeath;
            RestoreFromActiveSave();
        }

        private void OnDestroy()
        {
            if (Active == this) Active = null;
            if (_container != null) _container.OnLevelUpCheck -= CheckLevelUp;
            EnemyBase.OnAnyEnemyDeath -= HandleEnemyDeath;
            GameManager game = GameManager.Existing;
            if (_choiceOpen && game != null && game.CurrentState == GameState.RogueChoosing)
                game.ExitRogueSelection();
        }

        private void CheckLevelUp()
        {
            if (!GameManager.Instance.IsPlaying || _choiceOpen || _container == null || !_container.CanLevelUp || catalog == null) return;
            List<RogueSkillDefinition> choices = GetRandomChoices(3);
            if (choices.Count == 0) return;

            _pendingChoices.Clear();
            _pendingChoices.AddRange(choices);
            _choiceOpen = true;
            _choiceFromStatue = false;
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
            if (!_choiceFromStatue) _container.ResetForLevelUp();
            _pendingChoices.Clear();
            _choiceOpen = false;
            bool wasStatueChoice = _choiceFromStatue;
            _choiceFromStatue = false;
            GameManager.Instance.ExitRogueSelection();
            if (_selectionUI != null) Destroy(_selectionUI.gameObject);
            _selectionUI = null;
            SaveProgress();
            if (!wasStatueChoice && _container.CanLevelUp) _container.CheckAndNotify();
        }

        public bool RequestBasicStatueChoice()
        {
            if (!GameManager.Instance.IsPlaying || _choiceOpen || catalog == null) return false;
            List<RogueSkillDefinition> choices = GetBasicStatueChoices();
            if (choices.Count == 0) return false;
            _pendingChoices.Clear();
            _pendingChoices.AddRange(choices);
            _choiceOpen = true;
            _choiceFromStatue = true;
            AudioManager.Instance.PlaySfx(AudioCue.LevelUp);
            GameManager.Instance.EnterRogueSelection();
            _selectionUI = RogueSelectionUI.Show(this, catalog, choices);
            OnSkillChoiceRequired?.Invoke(choices);
            return true;
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
            }

            _levels[id] = next;
            if (apply) ApplyAllPlayerModifiers();
            OnSkillLevelChanged?.Invoke(id, next);
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

        public EnergyBallShotSnapshot CreateEnergyBallSnapshot(float baseDamage, float spatMass,
            float extraDamageMultiplier, float fullDamageMultiplier, float speed, float maxDistance)
        {
            return new EnergyBallShotSnapshot(baseDamage, spatMass, extraDamageMultiplier,
                fullDamageMultiplier, speed, maxDistance,
                new Dictionary<RogueSkillId, int>(_levels));
        }

        public List<RogueSkillDefinition> GetRandomChoices(int count)
        {
            List<RogueSkillDefinition> result = AllAvailable();
            if (result.Count > count) result.RemoveRange(count, result.Count - count);
            return result;
        }

        private bool CanOffer(RogueSkillDefinition skill)
        {
            if (skill == null || skill.IsMaxLevel(GetLevel(skill.id))) return false;
            if (IsBasicStatueSkill(skill.id)) return false;
            if (!IsUsefulForActiveFaith(skill.id)) return false;
            if (_faith.HasValue)
            {
                if (_faith.Value == RogueSkillId.FaithAngel)
                {
                    if (skill.school == RogueSchool.Faith && skill.id != RogueSkillId.FaithAngel)
                        return false;
                }
                else if (skill.school == RogueSchool.Faith && skill.id != _faith.Value)
                    return false;
            }
            foreach (RogueSkillId prerequisite in skill.prerequisites ?? Array.Empty<RogueSkillId>())
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

        private List<RogueSkillDefinition> GetBasicStatueChoices()
        {
            List<RogueSkillDefinition> choices = new(3);
            AddBasicChoice(RogueSkillId.NormalFast, choices);
            AddBasicChoice(RogueSkillId.NormalSuction, choices);
            AddBasicChoice(RogueSkillId.NormalPower, choices);
            Shuffle(choices);
            return choices;
        }

        private void AddBasicChoice(RogueSkillId id, List<RogueSkillDefinition> choices)
        {
            RogueSkillDefinition skill = catalog.Get(id);
            if (skill != null && !skill.IsMaxLevel(GetLevel(id)))
                choices.Add(skill);
        }

        private static bool IsBasicStatueSkill(RogueSkillId id)
        {
            return id == RogueSkillId.NormalFast || id == RogueSkillId.NormalSuction || id == RogueSkillId.NormalPower;
        }

        private bool IsUsefulForActiveFaith(RogueSkillId id)
        {
            if (_faith == RogueSkillId.FaithAngel)
            {
                return id != RogueSkillId.NormalSuction &&
                    id != RogueSkillId.EvolutionWing &&
                    id != RogueSkillId.EvolutionBigMouth;
            }

            if (_faith == RogueSkillId.FaithDemon)
            {
                switch (id)
                {
                    case RogueSkillId.NormalPower:
                    case RogueSkillId.PoisonDeadly:
                    case RogueSkillId.PoisonNumb:
                    case RogueSkillId.PoisonErode:
                    case RogueSkillId.PoisonWarp:
                    case RogueSkillId.PoisonLegacy:
                    case RogueSkillId.FirePyroblast:
                    case RogueSkillId.FirePyroblastFlame:
                    case RogueSkillId.FirePyroblastScope:
                    case RogueSkillId.FireBottle:
                    case RogueSkillId.FireLegacy:
                    case RogueSkillId.EvolutionCharged:
                    case RogueSkillId.EvolutionMoreMouth:
                    case RogueSkillId.EvolutionMoreMouthMore:
                    case RogueSkillId.EvolutionMoreMouthPower:
                    case RogueSkillId.SuperSplit:
                    case RogueSkillId.SuperSplitMore:
                    case RogueSkillId.SuperPiece:
                        return false;
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
                _controller.SkillMoveSpeedMultiplier = 1f;
                _controller.HasInhaleWalkSkill = Has(RogueSkillId.EvolutionWing);
                _controller.SetWitchFormEnabled(Has(RogueSkillId.FaithWitch), GetLevel(RogueSkillId.FaithWitch), catalog);
            }
            OnWitchProgressChanged?.Invoke(WitchProgressNormalized, Has(RogueSkillId.FaithWitch));
            if (_inhale != null)
            {
                _inhale.SkillSuctionMultiplier = Has(RogueSkillId.FaithDemon)
                    ? 2f + 0.1f * GetLevel(RogueSkillId.FaithDemon) : 1f;
                _inhale.SkillDamageMultiplier = _inhale.SkillSuctionMultiplier;
                _inhale.BonusInhaleDuration = Has(RogueSkillId.EvolutionBigMouth)
                    ? 3f + Mathf.Max(0, GetLevel(RogueSkillId.EvolutionBigMouth) - 1) * 2f : 0f;
                _inhale.DamageOnlyMode = Has(RogueSkillId.FaithDemon) || Has(RogueSkillId.FaithAngel);
                _inhale.ExternalInterruptImmune = Has(RogueSkillId.FaithDemon);
            }
            if (_baseAttributes != null)
            {
                _baseAttributes.BonusMoveSpeed = GetLevel(RogueSkillId.NormalFast);
                _baseAttributes.BonusSuction = GetLevel(RogueSkillId.NormalSuction);
                _baseAttributes.BonusEnergyBallBaseDamage =
                    GetLevel(RogueSkillId.NormalPower) + GetLevel(RogueSkillId.FaithAngel) * 10f;
            }
            if (_spit != null) _spit.RefreshSkillModifiers();
        }

        private void RestoreFromActiveSave()
        {
            if (GameManager.Instance.IsTestMode) return;
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
            if (GameManager.Instance.IsTestMode) return;
            List<RogueSkillSaveEntry> entries = new();
            foreach (var pair in _levels)
                entries.Add(new RogueSkillSaveEntry { id = pair.Key.ToString(), level = pair.Value });
            SaveGameService.SaveRogueSkills(entries);
        }

        public void ResetForTesting()
        {
            _pendingChoices.Clear();
            _levels.Clear();
            _faith = null;
            _witchProgress = 0;
            if (_choiceOpen)
            {
                _choiceOpen = false;
                _choiceFromStatue = false;
                if (_selectionUI != null) Destroy(_selectionUI.gameObject);
                _selectionUI = null;
                if (GameManager.Instance.CurrentState == GameState.RogueChoosing)
                    GameManager.Instance.ExitRogueSelection();
            }
            ApplyAllPlayerModifiers();
            OnWitchProgressChanged?.Invoke(0f, false);
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
            // This Faith benefit is binary: repeated Angel/Demon levels never stack the multiplier.
            float progress = (enemy != null ? enemy.MassValue : 5f) * FaithKillMassMultiplier;
            _container.AddProgress(progress);
        }
    }
}
