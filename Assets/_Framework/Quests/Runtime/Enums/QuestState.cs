namespace RPGFramework.Quests
{
    /// <summary>
    /// State of a quest.
    /// </summary>
    public enum QuestState
    {
        /// <summary>
        /// Quest has not been started yet.
        /// </summary>
        NotStarted,

        /// <summary>
        /// Quest is currently active and in progress.
        /// </summary>
        Active,

        /// <summary>
        /// Quest has been successfully completed.
        /// </summary>
        Completed,

        /// <summary>
        /// Quest has failed.
        /// </summary>
        Failed,

        /// <summary>
        /// Quest was started but abandoned by the player.
        /// </summary>
        Abandoned
    }
}
