namespace RPGFramework.Progression
{
    /// <summary>
    /// Represents current experience and level state.
    /// Tracks XP progress and level information.
    /// </summary>
    public struct ExperienceData
    {
        /// <summary>Current level</summary>
        public int Level;

        /// <summary>Current XP in this level</summary>
        public long CurrentXP;

        /// <summary>Total XP accumulated across all levels</summary>
        public long TotalXP;

        /// <summary>XP required for next level</summary>
        public long XPToNextLevel;

        /// <summary>Progress to next level (0.0 - 1.0)</summary>
        public float ProgressToNextLevel => XPToNextLevel > 0
            ? (float)CurrentXP / XPToNextLevel
            : 1.0f;

        public ExperienceData(int level, long currentXP, long totalXP, long xpToNextLevel)
        {
            Level = level;
            CurrentXP = currentXP;
            TotalXP = totalXP;
            XPToNextLevel = xpToNextLevel;
        }
    }
}
