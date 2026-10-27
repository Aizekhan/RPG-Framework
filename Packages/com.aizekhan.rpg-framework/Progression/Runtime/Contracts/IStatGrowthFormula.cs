using System.Collections.Generic;

namespace RPGFramework.Progression
{
    /// <summary>
    /// Calculates stat values based on level using growth formulas.
    /// Supports multiple stats with different growth curves.
    /// <para>Growth can differ per entity: register stats under a profile (a hero, a class, a monster kind) and
    /// assign the profile to entities. An entity uses its profile's growth for the stats the profile has, and the
    /// shared growth (registered without a profile) for the rest.</para>
    /// </summary>
    public interface IStatGrowthFormula
    {
        /// <summary>Calculate stat value at specific level, from the shared growth</summary>
        /// <param name="statId">Stat identifier</param>
        /// <param name="level">Target level</param>
        /// <returns>Calculated stat value</returns>
        float CalculateStatAtLevel(string statId, int level);

        /// <summary>Calculate the entity's stat at a level, from its profile or else the shared growth (0 when neither has the stat)</summary>
        float CalculateStatAtLevel(int entityId, string statId, int level);

        /// <summary>Get all stat changes when leveling up, from the shared growth</summary>
        /// <param name="fromLevel">Starting level</param>
        /// <param name="toLevel">Target level</param>
        /// <returns>Dictionary of stat changes (statId -> new value)</returns>
        Dictionary<string, float> GetStatChangesForLevelUp(int fromLevel, int toLevel);

        /// <summary>The entity's stats at the new level (statId -> new value): its profile's stats and the shared ones it does not override</summary>
        Dictionary<string, float> GetStatChangesForLevelUp(int entityId, int fromLevel, int toLevel);

        /// <summary>Register stat growth data shared by every entity</summary>
        /// <param name="growthData">Growth configuration</param>
        void RegisterStatGrowth(StatGrowthData growthData);

        /// <summary>Register stat growth for one profile (replaces the profile's growth of the same stat)</summary>
        void RegisterStatGrowth(string profileId, StatGrowthData growthData);

        /// <summary>Check if stat has registered shared growth</summary>
        /// <param name="statId">Stat identifier</param>
        /// <returns>True if registered</returns>
        bool HasStatGrowth(string statId);

        /// <summary>Whether the entity's stat grows, by its profile or the shared growth</summary>
        bool HasStatGrowth(int entityId, string statId);

        /// <summary>Give the entity a growth profile; null or empty takes it away (the entity uses the shared growth)</summary>
        void AssignProfile(int entityId, string profileId);

        /// <summary>The entity's growth profile, or null when it uses the shared growth</summary>
        string GetProfile(int entityId);
    }
}
