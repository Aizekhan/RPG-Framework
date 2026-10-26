// Packages/com.aizekhan.rpg-framework/Skills/Runtime/Implementations/SkillTree.cs

using System.Collections.Generic;
using System.Linq;

namespace RPGFramework.Skills
{
    /// <summary>
    /// Implementation of a skill tree.
    /// </summary>
    public class SkillTree : ISkillTree
    {
        public string Id { get; private set; }
        public string Name { get; private set; }
        public string Description { get; private set; }

        private readonly List<ISkill> _skills;
        private readonly Dictionary<string, ISkill> _skillLookup;

        public IReadOnlyList<ISkill> Skills => _skills;

        public SkillTree(string id, string name, string description)
        {
            Id = id;
            Name = name;
            Description = description;
            _skills = new List<ISkill>();
            _skillLookup = new Dictionary<string, ISkill>();
        }

        public void AddSkill(ISkill skill)
        {
            if (!_skillLookup.ContainsKey(skill.Id))
            {
                _skills.Add(skill);
                _skillLookup[skill.Id] = skill;
            }
        }

        public bool ContainsSkill(string skillId)
        {
            return _skillLookup.ContainsKey(skillId);
        }

        public ISkill GetSkill(string skillId)
        {
            return _skillLookup.TryGetValue(skillId, out ISkill skill) ? skill : null;
        }

        public bool CanLearnSkill(ISkillProgress progress, int characterLevel, string skillId)
        {
            ISkill skill = GetSkill(skillId);
            if (skill == null)
                return false;

            // Check if already at max level
            int currentLevel = progress.GetSkillLevel(skillId);
            if (currentLevel >= skill.MaxLevel)
                return false;

            // Check prerequisites
            foreach (var prerequisite in skill.Prerequisites)
            {
                if (!prerequisite.IsSatisfied(progress, characterLevel))
                    return false;
            }

            return true;
        }

        public IReadOnlyList<ISkill> GetAvailableSkills(ISkillProgress progress, int characterLevel)
        {
            return _skills
                .Where(skill => CanLearnSkill(progress, characterLevel, skill.Id))
                .ToList();
        }
    }
}
