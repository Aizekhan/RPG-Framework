using System;

namespace RPGFramework.Companions.Runtime
{
    /// <summary>
    /// ECS component holding core companion data.
    /// </summary>
    public struct CompanionData
    {
        /// <summary>
        /// Unique identifier for this companion instance.
        /// </summary>
        public string CompanionId;

        /// <summary>
        /// Display name of the companion.
        /// </summary>
        public string Name;

        /// <summary>
        /// Type/category of companion.
        /// </summary>
        public CompanionType CompanionType;

        /// <summary>
        /// Current state of the companion.
        /// </summary>
        public CompanionState CurrentState;

        /// <summary>
        /// Entity ID of the owner.
        /// </summary>
        public int OwnerId;

        /// <summary>
        /// Current loyalty value (-100 to 100).
        /// </summary>
        public int LoyaltyValue;

        /// <summary>
        /// Current level.
        /// </summary>
        public int Level;

        /// <summary>
        /// Whether the companion is currently active/summoned.
        /// </summary>
        public bool IsActive;

        /// <summary>
        /// Whether the companion is alive.
        /// </summary>
        public bool IsAlive;

        /// <summary>
        /// When the companion was first created/tamed.
        /// </summary>
        public DateTime CreatedAt;

        /// <summary>
        /// Last time the companion was fed.
        /// </summary>
        public DateTime LastFedAt;

        /// <summary>
        /// Gets the loyalty level based on loyalty value.
        /// </summary>
        public readonly LoyaltyLevel GetLoyaltyLevel()
        {
            if (LoyaltyValue <= -51) return LoyaltyLevel.Hostile;
            if (LoyaltyValue <= -1) return LoyaltyLevel.Wary;
            if (LoyaltyValue <= 24) return LoyaltyLevel.Neutral;
            if (LoyaltyValue <= 49) return LoyaltyLevel.Friendly;
            if (LoyaltyValue <= 74) return LoyaltyLevel.Loyal;
            return LoyaltyLevel.Devoted;
        }
    }
}
