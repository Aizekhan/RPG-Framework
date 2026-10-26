using System;

namespace RPGFramework.Achievements
{
    /// <summary>
    /// Represents progress data for a single achievement criterion.
    /// Tracks current progress towards the required value.
    /// </summary>
    [Serializable]
    public struct CriterionProgressData
    {
        /// <summary>
        /// The criterion ID this progress is tracking.
        /// </summary>
        public string CriterionId;

        /// <summary>
        /// Current value achieved.
        /// </summary>
        public int CurrentValue;

        /// <summary>
        /// Required value to complete this criterion.
        /// </summary>
        public int RequiredValue;

        /// <summary>
        /// Whether this criterion is completed.
        /// </summary>
        public bool IsCompleted;

        /// <summary>
        /// Constructor for creating criterion progress data.
        /// </summary>
        /// <param name="criterionId">Criterion ID.</param>
        /// <param name="currentValue">Current progress value.</param>
        /// <param name="requiredValue">Required value for completion.</param>
        public CriterionProgressData(string criterionId, int currentValue, int requiredValue)
        {
            CriterionId = criterionId;
            CurrentValue = currentValue;
            RequiredValue = requiredValue;
            IsCompleted = currentValue >= requiredValue;
        }

        /// <summary>
        /// Get completion percentage (0.0 to 1.0).
        /// </summary>
        public readonly float GetCompletionPercentage()
        {
            if (RequiredValue <= 0)
                return 0f;

            return Math.Min(1f, (float)CurrentValue / RequiredValue);
        }
    }
}
