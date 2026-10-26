// Packages/com.aizekhan.rpg-framework/Skills/Runtime/Data/SkillData.cs

using System;
using System.Collections.Generic;

namespace RPGFramework.Skills
{
    /// <summary>
    /// Struct representing skill definition data.
    /// </summary>
    [Serializable]
    public struct SkillData
    {
        public string Id;
        public string Name;
        public string Description;
        public int MaxLevel;
        public SkillType Type;
        public List<SkillPrerequisiteData> Prerequisites;
        public List<SkillBonusData> Bonuses;

        public static SkillData Create(
            string id,
            string name,
            string description,
            int maxLevel,
            SkillType type)
        {
            return new SkillData
            {
                Id = id,
                Name = name,
                Description = description,
                MaxLevel = maxLevel,
                Type = type,
                Prerequisites = new List<SkillPrerequisiteData>(),
                Bonuses = new List<SkillBonusData>()
            };
        }
    }

    /// <summary>
    /// Struct representing a skill prerequisite.
    /// </summary>
    [Serializable]
    public struct SkillPrerequisiteData
    {
        public string RequiredSkillId;
        public int RequiredSkillLevel;
        public int RequiredCharacterLevel;

        public static SkillPrerequisiteData CreateCharacterLevelRequirement(int level)
        {
            return new SkillPrerequisiteData
            {
                RequiredSkillId = null,
                RequiredSkillLevel = 0,
                RequiredCharacterLevel = level
            };
        }

        public static SkillPrerequisiteData CreateSkillRequirement(string skillId, int level)
        {
            return new SkillPrerequisiteData
            {
                RequiredSkillId = skillId,
                RequiredSkillLevel = level,
                RequiredCharacterLevel = 0
            };
        }
    }

    /// <summary>
    /// Struct representing a skill bonus.
    /// </summary>
    [Serializable]
    public struct SkillBonusData
    {
        public string StatId;
        public float BonusPerLevel;
        public BonusType Type;

        public static SkillBonusData Create(string statId, float bonusPerLevel, BonusType type)
        {
            return new SkillBonusData
            {
                StatId = statId,
                BonusPerLevel = bonusPerLevel,
                Type = type
            };
        }
    }
}
