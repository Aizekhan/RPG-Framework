using System;

namespace RPGFramework.Housing.Runtime.Data
{
    /// <summary>
    /// ECS component for house entity data.
    /// Pure data structure for house state.
    /// </summary>
    public struct HouseData
    {
        /// <summary>
        /// Unique identifier for this house.
        /// </summary>
        public Guid HouseId;

        /// <summary>
        /// Type of house.
        /// </summary>
        public HouseType HouseType;

        /// <summary>
        /// Display name of the house.
        /// </summary>
        public string Name;

        /// <summary>
        /// Current upgrade level (0-based).
        /// </summary>
        public int UpgradeLevel;

        /// <summary>
        /// Maximum upgrade level possible.
        /// </summary>
        public int MaxUpgradeLevel;

        /// <summary>
        /// Maximum furniture capacity.
        /// </summary>
        public int MaxFurnitureCapacity;

        /// <summary>
        /// Current furniture count.
        /// </summary>
        public int CurrentFurnitureCount;

        /// <summary>
        /// Whether the house is accessible.
        /// </summary>
        public bool IsAccessible;

        /// <summary>
        /// World X coordinate.
        /// </summary>
        public float WorldX;

        /// <summary>
        /// World Y coordinate.
        /// </summary>
        public float WorldY;

        /// <summary>
        /// World Z coordinate.
        /// </summary>
        public float WorldZ;

        /// <summary>
        /// Entity ID of the plot this house is on.
        /// </summary>
        public Guid PlotId;

        /// <summary>
        /// Create a new house data instance.
        /// </summary>
        public HouseData(Guid houseId, HouseType houseType, string name, int maxCapacity, Guid plotId, float worldX, float worldY, float worldZ)
        {
            HouseId = houseId;
            HouseType = houseType;
            Name = name ?? "Unnamed House";
            UpgradeLevel = 0;
            MaxUpgradeLevel = 3;
            MaxFurnitureCapacity = maxCapacity;
            CurrentFurnitureCount = 0;
            IsAccessible = true;
            PlotId = plotId;
            WorldX = worldX;
            WorldY = worldY;
            WorldZ = worldZ;
        }
    }
}
