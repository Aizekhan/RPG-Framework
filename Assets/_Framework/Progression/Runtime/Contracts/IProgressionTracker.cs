namespace RPGFramework.Progression
{
    /// <summary>
    /// Tracks experience and level progression for entities.
    /// Manages XP gain and level up mechanics.
    /// </summary>
    public interface IProgressionTracker
    {
        /// <summary>Add experience points to entity</summary>
        /// <param name="entityId">Entity receiving XP</param>
        /// <param name="xpAmount">Amount of XP to add</param>
        /// <returns>Progression result with level changes</returns>
        ProgressionResult AddExperience(int entityId, long xpAmount);

        /// <summary>Set level directly (for testing/admin commands)</summary>
        /// <param name="entityId">Entity to set level for</param>
        /// <param name="level">Level to set</param>
        void SetLevel(int entityId, int level);

        /// <summary>Get current experience data for entity</summary>
        /// <param name="entityId">Entity to query</param>
        /// <returns>Current experience data</returns>
        ExperienceData GetExperienceData(int entityId);

        /// <summary>Get current level for entity</summary>
        /// <param name="entityId">Entity to query</param>
        /// <returns>Current level</returns>
        int GetLevel(int entityId);

        /// <summary>Get total XP accumulated for entity</summary>
        /// <param name="entityId">Entity to query</param>
        /// <returns>Total XP</returns>
        long GetTotalXP(int entityId);

        /// <summary>Calculate XP required to reach specific level</summary>
        /// <param name="level">Target level</param>
        /// <returns>Total XP required</returns>
        long GetXPRequiredForLevel(int level);
    }
}
