namespace RPGFramework.Progression
{
    /// <summary>
    /// Information about a specific level.
    /// Contains XP requirements and level details.
    /// </summary>
    public struct LevelInfo
    {
        /// <summary>Level number</summary>
        public int Level;

        /// <summary>Total XP required to reach this level from level 1</summary>
        public long TotalXPRequired;

        /// <summary>XP required from previous level to this level</summary>
        public long XPFromPrevious;

        public LevelInfo(int level, long totalXPRequired, long xpFromPrevious)
        {
            Level = level;
            TotalXPRequired = totalXPRequired;
            XPFromPrevious = xpFromPrevious;
        }
    }
}
