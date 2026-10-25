using System;

namespace RPGFramework.Companions.Runtime
{
    /// <summary>
    /// Tracks feeding and care history for companions.
    /// </summary>
    public struct FeedingData
    {
        /// <summary>
        /// Total number of times the companion has been fed.
        /// </summary>
        public int TotalFeedingCount;

        /// <summary>
        /// Last time the companion was fed.
        /// </summary>
        public DateTime LastFedTime;

        /// <summary>
        /// ID of the last food item given.
        /// </summary>
        public string LastFoodItemId;

        /// <summary>
        /// Whether the companion is currently starving (hunger below threshold).
        /// </summary>
        public bool IsStarving;

        /// <summary>
        /// Whether the companion is well-fed (hunger above threshold).
        /// </summary>
        public bool IsWellFed;

        /// <summary>
        /// Accumulated time since last fed (in seconds).
        /// </summary>
        public float TimeSinceLastFed;

        /// <summary>
        /// Preferred food type/category for this companion.
        /// </summary>
        public string PreferredFoodType;
    }
}
