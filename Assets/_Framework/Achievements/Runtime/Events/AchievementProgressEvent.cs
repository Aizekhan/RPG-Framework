namespace RPGFramework.Achievements
{
    /// <summary>
    /// Event emitted when achievement progress is updated.
    /// </summary>
    public struct AchievementProgressEvent
    {
        /// <summary>
        /// The entity whose progress was updated.
        /// </summary>
        public int EntityId;

        /// <summary>
        /// The ID of the achievement that progressed.
        /// </summary>
        public string AchievementId;

        /// <summary>
        /// The ID of the criterion that was updated.
        /// </summary>
        public string CriterionId;

        /// <summary>
        /// Previous progress value.
        /// </summary>
        public int PreviousValue;

        /// <summary>
        /// New progress value.
        /// </summary>
        public int NewValue;

        /// <summary>
        /// Required value to complete this criterion.
        /// </summary>
        public int RequiredValue;

        /// <summary>
        /// Whether this update completed the criterion.
        /// </summary>
        public bool CriterionCompleted;

        /// <summary>
        /// Overall completion percentage of the achievement (0.0 to 1.0).
        /// </summary>
        public float CompletionPercentage;

        /// <summary>
        /// Constructor for creating an achievement progress event.
        /// </summary>
        public AchievementProgressEvent(
            int entityId,
            string achievementId,
            string criterionId,
            int previousValue,
            int newValue,
            int requiredValue,
            bool criterionCompleted,
            float completionPercentage)
        {
            EntityId = entityId;
            AchievementId = achievementId;
            CriterionId = criterionId;
            PreviousValue = previousValue;
            NewValue = newValue;
            RequiredValue = requiredValue;
            CriterionCompleted = criterionCompleted;
            CompletionPercentage = completionPercentage;
        }
    }
}
