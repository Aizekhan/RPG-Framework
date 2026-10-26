// Packages/com.aizekhan.rpg-framework/Skills/Runtime/Data/SkillProgressData.cs

using System;
using System.Collections.Generic;

namespace RPGFramework.Skills
{
    /// <summary>
    /// Struct representing an entity's skill progression data.
    /// This is the data structure stored in ECS components.
    /// </summary>
    [Serializable]
    public struct SkillProgressData
    {
        /// <summary>
        /// Dictionary mapping skill IDs to their current levels.
        /// </summary>
        public Dictionary<string, int> SkillLevels;

        /// <summary>
        /// Creates empty skill progress data.
        /// </summary>
        public static SkillProgressData CreateEmpty()
        {
            return new SkillProgressData
            {
                SkillLevels = new Dictionary<string, int>()
            };
        }

        /// <summary>
        /// Creates skill progress data with a list of skills at level 1.
        /// </summary>
        public static SkillProgressData CreateWithSkills(List<string> skillIds)
        {
            var data = CreateEmpty();
            foreach (var skillId in skillIds)
            {
                data.SkillLevels[skillId] = 1;
            }
            return data;
        }

        /// <summary>
        /// Gets the level of a specific skill.
        /// Returns 0 if the skill has not been learned.
        /// </summary>
        public int GetSkillLevel(string skillId)
        {
            if (SkillLevels == null) return 0;
            return SkillLevels.TryGetValue(skillId, out int level) ? level : 0;
        }

        /// <summary>
        /// Checks if a skill has been learned.
        /// </summary>
        public bool HasSkill(string skillId)
        {
            return SkillLevels != null && SkillLevels.ContainsKey(skillId);
        }

        /// <summary>
        /// Sets the level of a skill.
        /// </summary>
        public void SetSkillLevel(string skillId, int level)
        {
            if (SkillLevels == null)
                SkillLevels = new Dictionary<string, int>();

            SkillLevels[skillId] = level;
        }

        /// <summary>
        /// Removes a skill.
        /// </summary>
        public void RemoveSkill(string skillId)
        {
            SkillLevels?.Remove(skillId);
        }
    }
}
