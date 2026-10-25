namespace RPGFramework.Progression
{
    /// <summary>
    /// Result of adding experience.
    /// Contains information about level changes and new state.
    /// </summary>
    public struct ProgressionResult
    {
        /// <summary>Previous level before XP gain</summary>
        public int OldLevel;

        /// <summary>New level after XP gain</summary>
        public int NewLevel;

        /// <summary>Number of levels gained (can be multiple)</summary>
        public int LevelsGained => NewLevel - OldLevel;

        /// <summary>XP amount added</summary>
        public long XPAdded;

        /// <summary>New experience data after gain</summary>
        public ExperienceData ExperienceData;

        /// <summary>Whether at least one level up occurred</summary>
        public bool LeveledUp => LevelsGained > 0;

        public static ProgressionResult Create(int oldLevel, int newLevel, long xpAdded, ExperienceData expData)
        {
            return new ProgressionResult
            {
                OldLevel = oldLevel,
                NewLevel = newLevel,
                XPAdded = xpAdded,
                ExperienceData = expData
            };
        }
    }
}
