using System;

namespace RPGFramework.Housing.Runtime.Data
{
    /// <summary>
    /// Tracks upgrade history and state for a house.
    /// </summary>
    public struct HouseUpgradeData
    {
        /// <summary>
        /// House being upgraded.
        /// </summary>
        public Guid HouseId;

        /// <summary>
        /// Current upgrade level.
        /// </summary>
        public int CurrentLevel;

        /// <summary>
        /// Maximum upgrade level.
        /// </summary>
        public int MaxLevel;

        /// <summary>
        /// Base furniture capacity.
        /// </summary>
        public int BaseFurnitureCapacity;

        /// <summary>
        /// Capacity bonus per upgrade level.
        /// </summary>
        public int CapacityBonusPerLevel;

        /// <summary>
        /// Total number of upgrades performed.
        /// </summary>
        public int TotalUpgradesPerformed;

        /// <summary>
        /// Timestamp of last upgrade.
        /// </summary>
        public DateTime LastUpgradeTime;

        /// <summary>
        /// Create a new upgrade data instance.
        /// </summary>
        public HouseUpgradeData(
            Guid houseId,
            int maxLevel,
            int baseCapacity,
            int capacityBonusPerLevel)
        {
            HouseId = houseId;
            CurrentLevel = 0;
            MaxLevel = maxLevel;
            BaseFurnitureCapacity = baseCapacity;
            CapacityBonusPerLevel = capacityBonusPerLevel;
            TotalUpgradesPerformed = 0;
            LastUpgradeTime = DateTime.MinValue;
        }

        /// <summary>
        /// Calculate current furniture capacity based on upgrade level.
        /// </summary>
        public readonly int CalculateCurrentCapacity()
        {
            return BaseFurnitureCapacity + (CurrentLevel * CapacityBonusPerLevel);
        }

        /// <summary>
        /// Check if an upgrade is possible.
        /// </summary>
        public readonly bool CanUpgrade()
        {
            return CurrentLevel < MaxLevel;
        }
    }
}
