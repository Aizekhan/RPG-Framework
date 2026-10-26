namespace RPGFramework.Achievements
{
    /// <summary>
    /// Event emitted when a criterion is completed.
    /// </summary>
    public struct CriterionCompletedEvent
    {
        /// <summary>
        /// The entity that completed the criterion.
        /// </summary>
        public int EntityId;

        /// <summary>
        /// The ID of the achievement this criterion belongs to.
        /// </summary>
        public string AchievementId;

        /// <summary>
        /// The ID of the criterion that was completed.
        /// </summary>
        public string CriterionId;

        /// <summary>
        /// The type of criterion that was completed.
        /// </summary>
        public CriterionType CriterionType;

        /// <summary>
        /// Final value achieved.
        /// </summary>
        public int FinalValue;

        /// <summary>
        /// Required value that was met.
        /// </summary>
        public int RequiredValue;

        /// <summary>
        /// Constructor for creating a criterion completed event.
        /// </summary>
        public CriterionCompletedEvent(
            int entityId,
            string achievementId,
            string criterionId,
            CriterionType criterionType,
            int finalValue,
            int requiredValue)
        {
            EntityId = entityId;
            AchievementId = achievementId;
            CriterionId = criterionId;
            CriterionType = criterionType;
            FinalValue = finalValue;
            RequiredValue = requiredValue;
        }
    }
}
