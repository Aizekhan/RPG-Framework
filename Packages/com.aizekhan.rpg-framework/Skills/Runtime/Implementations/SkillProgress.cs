// Packages/com.aizekhan.rpg-framework/Skills/Runtime/Implementations/SkillProgress.cs

using System.Collections.Generic;
using System.Linq;

namespace RPGFramework.Skills
{
    /// <summary>
    /// Implementation of skill progress tracking.
    /// </summary>
    public class SkillProgress : ISkillProgress
    {
        private readonly SkillProgressData _data;

        public IReadOnlyList<string> LearnedSkills =>
            _data.SkillLevels?.Keys.ToList() ?? new List<string>();

        public int TotalSkillsLearned =>
            _data.SkillLevels?.Count ?? 0;

        public SkillProgress(SkillProgressData data)
        {
            _data = data;
        }

        public int GetSkillLevel(string skillId)
        {
            return _data.GetSkillLevel(skillId);
        }

        public bool HasSkill(string skillId)
        {
            return _data.HasSkill(skillId);
        }
    }
}
