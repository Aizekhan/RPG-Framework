namespace RPGFramework.Progression
{
    /// <summary>
    /// Defines level requirements and XP curves.
    /// Provides calculations for level progression.
    /// </summary>
    public interface ILevelTable
    {
        /// <summary>Maximum level in this table</summary>
        int MaxLevel { get; }

        /// <summary>Get level info for specific level</summary>
        /// <param name="level">Level to query</param>
        /// <returns>Level information</returns>
        LevelInfo GetLevelInfo(int level);

        /// <summary>Calculate level from total XP</summary>
        /// <param name="totalXP">Total XP accumulated</param>
        /// <returns>Calculated level</returns>
        int CalculateLevelFromXP(long totalXP);

        /// <summary>Get XP required for next level from current level</summary>
        /// <param name="currentLevel">Current level</param>
        /// <returns>XP needed to reach next level</returns>
        long GetXPForNextLevel(int currentLevel);

        /// <summary>Get total XP required to reach level from level 1</summary>
        /// <param name="level">Target level</param>
        /// <returns>Total XP required</returns>
        long GetTotalXPForLevel(int level);
    }
}
