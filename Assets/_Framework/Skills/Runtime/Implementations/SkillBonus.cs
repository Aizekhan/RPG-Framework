// Assets/_Framework/Skills/Runtime/Implementations/SkillBonus.cs

namespace RPGFramework.Skills
{
    /// <summary>
    /// Implementation of a skill bonus.
    /// </summary>
    public class SkillBonus : ISkillBonus
    {
        public string StatId { get; private set; }
        public float BonusPerLevel { get; private set; }
        public BonusType Type { get; private set; }

        public SkillBonus(SkillBonusData data)
        {
            StatId = data.StatId;
            BonusPerLevel = data.BonusPerLevel;
            Type = data.Type;
        }

        public SkillBonus(string statId, float bonusPerLevel, BonusType type)
        {
            StatId = statId;
            BonusPerLevel = bonusPerLevel;
            Type = type;
        }

        public float CalculateBonus(int skillLevel)
        {
            return BonusPerLevel * skillLevel;
        }
    }
}
