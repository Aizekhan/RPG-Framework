using System;

namespace RPGFramework.Progression
{
    /// <summary>
    /// Defines how a stat grows on level up.
    /// Supports multiple growth formulas and curve types.
    /// </summary>
    public struct StatGrowthData
    {
        /// <summary>Stat identifier (e.g., "Strength", "HP", "AttackPower")</summary>
        public string StatId;

        /// <summary>Base value at level 1</summary>
        public float BaseValue;

        /// <summary>Growth per level (flat amount)</summary>
        public float GrowthPerLevel;

        /// <summary>Growth percentage per level (multiplicative, 0.1 = 10%)</summary>
        public float GrowthPercentage;

        /// <summary>Growth curve type</summary>
        public GrowthCurveType CurveType;

        public StatGrowthData(string statId, float baseValue, float growthPerLevel,
            float growthPercentage = 0f, GrowthCurveType curveType = GrowthCurveType.Linear)
        {
            StatId = statId;
            BaseValue = baseValue;
            GrowthPerLevel = growthPerLevel;
            GrowthPercentage = growthPercentage;
            CurveType = curveType;
        }

        /// <summary>Calculate stat value at specific level</summary>
        public float CalculateAtLevel(int level)
        {
            if (level <= 1) return BaseValue;

            int levelsGained = level - 1;

            switch (CurveType)
            {
                case GrowthCurveType.Linear:
                    // value = base + (growth * levels)
                    return BaseValue + (GrowthPerLevel * levelsGained);

                case GrowthCurveType.Exponential:
                    // Each level adds growth and multiplies by percentage
                    float value = BaseValue;
                    for (int i = 0; i < levelsGained; i++)
                    {
                        value += GrowthPerLevel;
                        value *= (1f + GrowthPercentage);
                    }
                    return value;

                case GrowthCurveType.Polynomial:
                    // value = base + (growth * level^2)
                    return BaseValue + (GrowthPerLevel * levelsGained * levelsGained);

                default:
                    return BaseValue;
            }
        }
    }
}
