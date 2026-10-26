namespace RPGFramework.Achievements
{
    /// <summary>
    /// Represents the current state of an achievement for a specific entity.
    /// </summary>
    public enum AchievementState
    {
        /// <summary>
        /// Achievement is locked and no progress has been made.
        /// </summary>
        Locked = 0,

        /// <summary>
        /// Achievement is in progress with partial completion.
        /// </summary>
        InProgress = 1,

        /// <summary>
        /// Achievement has been unlocked/completed.
        /// </summary>
        Unlocked = 2
    }
}
