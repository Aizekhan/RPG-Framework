using System;

namespace RPGFramework.Housing.Runtime.Events
{
    /// <summary>
    /// Event raised when a house is upgraded to a higher level.
    /// </summary>
    public class HouseUpgradedEvent
    {
        /// <summary>
        /// ID of the upgraded house.
        /// </summary>
        public Guid HouseId { get; }

        /// <summary>
        /// Previous upgrade level.
        /// </summary>
        public int PreviousLevel { get; }

        /// <summary>
        /// New upgrade level.
        /// </summary>
        public int NewLevel { get; }

        /// <summary>
        /// Previous furniture capacity.
        /// </summary>
        public int PreviousCapacity { get; }

        /// <summary>
        /// New furniture capacity.
        /// </summary>
        public int NewCapacity { get; }

        /// <summary>
        /// Entity that performed the upgrade.
        /// </summary>
        public Guid UpgradedBy { get; }

        /// <summary>
        /// Timestamp of upgrade.
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// Create a new house upgraded event.
        /// </summary>
        public HouseUpgradedEvent(
            Guid houseId,
            int previousLevel,
            int newLevel,
            int previousCapacity,
            int newCapacity,
            Guid upgradedBy)
        {
            HouseId = houseId;
            PreviousLevel = previousLevel;
            NewLevel = newLevel;
            PreviousCapacity = previousCapacity;
            NewCapacity = newCapacity;
            UpgradedBy = upgradedBy;
            Timestamp = DateTime.UtcNow;
        }
    }
}
