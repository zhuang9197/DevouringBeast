using System.Collections.Generic;
using UnityEngine;

namespace DevouringBeast
{
    /// <summary>
    /// RogueSkillManager — 管理玩家技能获取与升级
    /// </summary>
    public class RogueSkillManager : MonoBehaviour
    {
        [Header("技能列表")]
        [SerializeField] private List<RogueSkillData> allSkills = new();

        [Header("事件")]
        [SerializeField] private VoidEventChannel onLevelUp;

        private SwallowContainer _container;
        private PlayerController _playerController;
        private PlayerSpit _playerSpit;
        private PlayerInhale _playerInhale;

        /// <summary>当前已拥有的技能</summary>
        private readonly List<RogueSkillData> _ownedSkills = new();

        /// <summary>待选技能（三选一）</summary>
        private List<RogueSkillData> _pendingChoices;

        public System.Action<List<RogueSkillData>> OnSkillChoiceRequired;

        /// <summary>当前玩家拥有的技能。调用方不可修改内部列表。</summary>
        public IReadOnlyList<RogueSkillData> OwnedSkills => _ownedSkills;

        private void Awake()
        {
            _container = GetComponent<SwallowContainer>();
            _playerController = GetComponent<PlayerController>();
            _playerSpit = GetComponent<PlayerSpit>();
            _playerInhale = GetComponent<PlayerInhale>();
        }

        private void Start()
        {
            // 监听容器变化
            _container.OnLevelUpCheck += CheckLevelUp;
        }

        private void OnDestroy()
        {
            if (_container != null)
                _container.OnLevelUpCheck -= CheckLevelUp;
        }

        /// <summary>
        /// 检查是否可以升级
        /// </summary>
        private void CheckLevelUp()
        {
            if (!_container.CanLevelUp) return;

            // 获取占比最高的标签
            ItemTag dominantTag = _container.GetDominantTag();

            // 获取可用技能（筛选该标签下未满级的技能）
            var availableSkills = GetAvailableChoices(dominantTag, 3);
            if (availableSkills.Count == 0) return;

            _pendingChoices = availableSkills;
            onLevelUp?.RaiseEvent();
            OnSkillChoiceRequired?.Invoke(availableSkills);
        }

        /// <summary>
        /// 选择技能
        /// </summary>
        public void SelectSkill(RogueSkillData skill)
        {
            if (_pendingChoices == null || !_pendingChoices.Contains(skill)) return;

            AddSkill(skill);
            _container.ResetForLevelUp();
            _pendingChoices = null;
        }

        /// <summary>
        /// 添加/升级技能
        /// </summary>
        private void AddSkill(RogueSkillData skill)
        {
            if (!_ownedSkills.Contains(skill))
            {
                _ownedSkills.Add(skill);
                skill.currentLevel = 1;
            }
            else
            {
                skill.currentLevel++;
            }

            ApplySkillEffect(skill);
            Debug.Log($"[SkillManager] {skill.skillName} Lv.{skill.currentLevel}");
        }

        /// <summary>
        /// 应用技能效果
        /// </summary>
        private void ApplySkillEffect(RogueSkillData skill)
        {
            float value = skill.CurrentValue;

            switch (skill.skillName)
            {
                // 无标签系
                case "移速提升":
                    _playerController.MoveSpeed *= (1f + value);
                    break;
                case "吸力提升":
                    _playerInhale.MaxSuctionForce *= (1f + value);
                    break;
                case "弹速提升":
                    _playerSpit.SpitSpeed *= (1f + value);
                    break;

                // 自然系
                case "翅膀":
                    _playerController.HasInhaleWalkSkill = true;
                    break;
                case "大嘴":
                    _playerInhale.MaxInhaleDuration *= (1f + value);
                    break;
                // 更多技能效果在此扩展...
            }
        }

        /// <summary>
        /// 获取可选技能（满足前置、未满级）
        /// </summary>
        public List<RogueSkillData> GetAvailableChoices(ItemTag tag, int count)
        {
            var result = new List<RogueSkillData>();

            foreach (var skill in allSkills)
            {
                if (skill.tag != tag) continue;
                if (skill.IsMaxLevel) continue;
                if (skill.prerequisite != null && !_ownedSkills.Contains(skill.prerequisite))
                    continue;

                result.Add(skill);
            }

            // 随机选 count 个
            while (result.Count > count)
            {
                result.RemoveAt(Random.Range(0, result.Count));
            }

            return result;
        }

        /// <summary>
        /// 为一次发射复制当前伤害、速度、最大距离以及全部已拥有技能。
        /// </summary>
        public EnergyBallShotSnapshot CreateEnergyBallSnapshot(
            float damage,
            float speed,
            float maxDistance)
        {
            return new EnergyBallShotSnapshot(damage, speed, maxDistance, _ownedSkills);
        }

        public int GetOwnedSkillLevel(string nameFragment)
        {
            if (string.IsNullOrWhiteSpace(nameFragment))
                return 0;

            for (int i = 0; i < _ownedSkills.Count; i++)
            {
                RogueSkillData skill = _ownedSkills[i];
                if (skill != null && !string.IsNullOrEmpty(skill.skillName) &&
                    skill.skillName.IndexOf(nameFragment, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return skill.currentLevel;
                }
            }

            return 0;
        }
    }
}
