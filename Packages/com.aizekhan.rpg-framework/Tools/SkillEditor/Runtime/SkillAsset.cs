using System.Collections.Generic;
using UnityEngine;
using RPGFramework.Skills;

namespace RPGFramework.Tools.SkillEditor
{
    /// <summary>
    /// One skill made in the inspector: its id, name, how high it can be raised, what it needs before it can be
    /// learned and what it gives per level. <see cref="ToSkill"/> turns it into the skill the game runs on.
    /// </summary>
    [CreateAssetMenu(fileName = "NewSkill", menuName = "RPG Framework/Skill", order = 105)]
    public class SkillAsset : ScriptableObject
    {
        [Header("Skill Identity")]
        [Tooltip("Unique id the game uses for this skill (e.g. 'warrior_strike')")]
        public string skillId = "skill_new";

        [Tooltip("Name shown to players")]
        public string skillName = "New Skill";

        [Tooltip("What the skill does, shown to players")]
        [TextArea(3, 6)]
        public string description = "Skill description...";

        [Tooltip("What kind of skill this is (passive, active, combat, crafting...)")]
        public SkillType skillType = SkillType.Passive;

        [Tooltip("Highest level the skill can be raised to (1 = learned once, never raised)")]
        [Min(1)]
        public int maxLevel = 1;

        [Header("Requirements")]
        [Tooltip("Character level needed to learn this skill (0 = any level)")]
        [Min(0)]
        public int requiredCharacterLevel = 0;

        [Tooltip("Skills that must be learned first, each up to a level")]
        public SkillPrerequisiteEntry[] prerequisites = new SkillPrerequisiteEntry[0];

        [Header("Bonuses")]
        [Tooltip("Stat bonuses this skill gives for each of its levels")]
        public SkillBonusEntry[] bonuses = new SkillBonusEntry[0];

        /// <summary>
        /// The skill's data as the runtime keeps it. Empty prerequisite and bonus slots are left out (an empty
        /// prerequisite would otherwise mean nothing); a character level of 0 adds no level requirement.
        /// </summary>
        public SkillData ToSkillData()
        {
            var data = SkillData.Create(skillId, skillName, description, maxLevel, skillType);

            if (requiredCharacterLevel > 0)
                data.Prerequisites.Add(SkillPrerequisiteData.CreateCharacterLevelRequirement(requiredCharacterLevel));

            if (prerequisites != null)
            {
                foreach (var entry in prerequisites)
                {
                    if (entry == null || entry.skill == null)
                        continue;
                    data.Prerequisites.Add(
                        SkillPrerequisiteData.CreateSkillRequirement(entry.skill.skillId, entry.requiredLevel));
                }
            }

            if (bonuses != null)
            {
                foreach (var bonus in bonuses)
                {
                    if (bonus == null)
                        continue;
                    data.Bonuses.Add(SkillBonusData.Create(bonus.statId, bonus.bonusPerLevel, bonus.bonusType));
                }
            }

            return data;
        }

        /// <summary>
        /// The skill the game runs on, ready to add to a <see cref="SkillTree"/>.
        /// </summary>
        public Skill ToSkill()
        {
            return new Skill(ToSkillData());
        }

        /// <summary>
        /// Problems with this skill on its own: missing id or name, a maximum level below 1, a negative character
        /// level, empty or self-pointing prerequisites, prerequisite levels the other skill can never reach, and
        /// bonuses without a stat. An empty list means the skill is ready.
        /// </summary>
        public List<string> Validate()
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(skillId))
                errors.Add("Skill ID cannot be empty");

            if (string.IsNullOrWhiteSpace(skillName))
                errors.Add("Skill Name cannot be empty");

            if (maxLevel < 1)
                errors.Add("Max Level must be at least 1");

            if (requiredCharacterLevel < 0)
                errors.Add("Required Character Level cannot be negative");

            if (prerequisites != null)
            {
                for (int i = 0; i < prerequisites.Length; i++)
                {
                    var entry = prerequisites[i];
                    if (entry == null || entry.skill == null)
                    {
                        errors.Add($"Prerequisite {i + 1} is empty");
                        continue;
                    }

                    if (entry.skill == this || (!string.IsNullOrEmpty(skillId) && entry.skill.skillId == skillId))
                    {
                        errors.Add($"Skill '{skillId}' requires itself and can never be learned");
                        continue;
                    }

                    if (entry.requiredLevel < 1)
                        errors.Add($"Prerequisite '{entry.skill.skillId}' needs a required level of at least 1");
                    else if (entry.requiredLevel > entry.skill.maxLevel)
                        errors.Add($"Prerequisite '{entry.skill.skillId}' needs level {entry.requiredLevel}, " +
                                   $"but that skill only goes up to {entry.skill.maxLevel}");
                }
            }

            if (bonuses != null)
            {
                for (int i = 0; i < bonuses.Length; i++)
                {
                    if (bonuses[i] == null || string.IsNullOrWhiteSpace(bonuses[i].statId))
                        errors.Add($"Bonus {i + 1} has no stat");
                }
            }

            return errors;
        }

        private void OnValidate()
        {
            if (!string.IsNullOrWhiteSpace(skillId))
                skillId = skillId.Trim().ToLower().Replace(" ", "_");

            if (maxLevel < 1) maxLevel = 1;
            if (requiredCharacterLevel < 0) requiredCharacterLevel = 0;
        }
    }

    /// <summary>
    /// A skill that must be learned first, and the level it must reach.
    /// </summary>
    [System.Serializable]
    public class SkillPrerequisiteEntry
    {
        [Tooltip("The skill that must be learned first")]
        public SkillAsset skill;

        [Tooltip("Level that skill must reach before this one can be learned")]
        [Min(1)]
        public int requiredLevel = 1;
    }

    /// <summary>
    /// A stat bonus a skill gives for each of its levels.
    /// </summary>
    [System.Serializable]
    public class SkillBonusEntry
    {
        [Tooltip("Id of the stat to raise (e.g. 'HP', 'AttackPower')")]
        public string statId = "HP";

        [Tooltip("How much the stat changes for each skill level")]
        public float bonusPerLevel = 1f;

        [Tooltip("How the bonus is applied: added, multiplied, or replacing the stat")]
        public BonusType bonusType = BonusType.Additive;
    }
}
