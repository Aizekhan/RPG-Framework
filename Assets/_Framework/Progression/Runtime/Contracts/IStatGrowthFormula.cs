using System.Collections.Generic;

namespace RPGFramework.Progression
{
    /// <summary>
    /// Calculates stat values based on level using growth formulas.
    /// Supports multiple stats with different growth curves.
    /// </summary>
    public interface IStatGrowthFormula
    {
        /// <summary>Calculate stat value at specific level</summary>
        /// <param name="statId">Stat identifier</param>
        /// <param name="level">Target level</param>
        /// <returns>Calculated stat value</returns>
        float CalculateStatAtLevel(string statId, int level);

        /// <summary>Get all stat changes when leveling up</summary>
        /// <param name="fromLevel">Starting level</param>
        /// <param name="toLevel">Target level</param>
        /// <returns>Dictionary of stat changes (statId -> new value)</returns>
        Dictionary<string, float> GetStatChangesForLevelUp(int fromLevel, int toLevel);

        /// <summary>Register stat growth data</summary>
        /// <param name="growthData">Growth configuration</param>
        void RegisterStatGrowth(StatGrowthData growthData);

        /// <summary>Check if stat has registered growth</summary>
        /// <param name="statId">Stat identifier</param>
        /// <returns>True if registered</returns>
        bool HasStatGrowth(string statId);
    }
}
