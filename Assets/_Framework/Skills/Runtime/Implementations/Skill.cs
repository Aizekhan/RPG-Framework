// Assets/_Framework/Skills/Runtime/Implementations/Skill.cs

using System.Collections.Generic;
using System.Linq;

namespace RPGFramework.Skills
{
    /// <summary>
    /// Implementation of a skill definition.
    /// </summary>
    public class Skill : ISkill
    {
        public string Id { get; private set; }
        public string Name { get; private set; }
        public string Description { get; private set; }
        public int MaxLevel { get; private set; }
        public SkillType Type { get; private set; }

        private readonly List<ISkillPrerequisite> _prerequisites;
        private readonly List<ISkillBonus> _bonuses;

        public IReadOnlyList<ISkillPrerequisite> Prerequisites => _prerequisites;
        public IReadOnlyList<ISkillBonus> Bonuses => _bonuses;

        public Skill(SkillData data)
        {
            Id = data.Id;
            Name = data.Name;
            Description = data.Description;
            MaxLevel = data.MaxLevel;
            Type = data.Type;

            _prerequisites = data.Prerequisites?
                .Select(p => new SkillPrerequisite(p) as ISkillPrerequisite)
                .ToList() ?? new List<ISkillPrerequisite>();

            _bonuses = data.Bonuses?
                .Select(b => new SkillBonus(b) as ISkillBonus)
                .ToList() ?? new List<ISkillBonus>();
        }

        public Skill(string id, string name, string description, int maxLevel, SkillType type)
        {
            Id = id;
            Name = name;
            Description = description;
            MaxLevel = maxLevel;
            Type = type;
            _prerequisites = new List<ISkillPrerequisite>();
            _bonuses = new List<ISkillBonus>();
        }

        public void AddPrerequisite(ISkillPrerequisite prerequisite)
        {
            if (!_prerequisites.Contains(prerequisite))
                _prerequisites.Add(prerequisite);
        }

        public void AddBonus(ISkillBonus bonus)
        {
            if (!_bonuses.Contains(bonus))
                _bonuses.Add(bonus);
        }
    }
}
