namespace RPGFramework.Achievements
{
    /// <summary>
    /// Types of criteria that can be tracked for achievement completion.
    /// </summary>
    public enum CriterionType
    {
        /// <summary>
        /// Track number of entities killed (monsters, enemies, etc.).
        /// </summary>
        KillCount = 0,

        /// <summary>
        /// Track number of items collected or obtained.
        /// </summary>
        ItemCollected = 1,

        /// <summary>
        /// Track completion of specific quests.
        /// </summary>
        QuestCompleted = 2,

        /// <summary>
        /// Track character level milestones.
        /// </summary>
        LevelReached = 3,

        /// <summary>
        /// Track character stat thresholds (strength, intelligence, etc.).
        /// </summary>
        StatThreshold = 4,

        /// <summary>
        /// Track total damage dealt.
        /// </summary>
        DamageDealt = 5,

        /// <summary>
        /// Track total distance traveled.
        /// </summary>
        DistanceTraveled = 6,

        /// <summary>
        /// Track time spent in-game or in specific activities.
        /// </summary>
        TimeSpent = 7,

        /// <summary>
        /// Custom criterion type for game-specific achievements.
        /// </summary>
        Custom = 99
    }
}
