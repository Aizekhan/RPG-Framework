using System;

namespace RPGFramework.Housing.Runtime.Data
{
    /// <summary>
    /// Data structure for placed furniture with position and rotation.
    /// </summary>
    public struct FurniturePlacementData
    {
        /// <summary>
        /// Unique furniture instance ID.
        /// </summary>
        public Guid FurnitureId;

        /// <summary>
        /// Template ID for furniture type.
        /// </summary>
        public string TemplateId;

        /// <summary>
        /// Display name.
        /// </summary>
        public string Name;

        /// <summary>
        /// Category of furniture.
        /// </summary>
        public FurnitureCategory Category;

        /// <summary>
        /// Placement mode.
        /// </summary>
        public PlacementMode PlacementMode;

        /// <summary>
        /// Position X in house local space.
        /// </summary>
        public float PositionX;

        /// <summary>
        /// Position Y in house local space.
        /// </summary>
        public float PositionY;

        /// <summary>
        /// Position Z in house local space.
        /// </summary>
        public float PositionZ;

        /// <summary>
        /// Rotation in degrees (Yaw).
        /// </summary>
        public float Rotation;

        /// <summary>
        /// Whether this furniture has storage.
        /// </summary>
        public bool HasStorage;

        /// <summary>
        /// Storage capacity if applicable.
        /// </summary>
        public int StorageCapacity;

        /// <summary>
        /// Whether this is a crafting station.
        /// </summary>
        public bool IsCraftingStation;

        /// <summary>
        /// Type of crafting station.
        /// </summary>
        public string CraftingStationType;

        /// <summary>
        /// Create a new furniture placement data instance.
        /// </summary>
        public FurniturePlacementData(
            Guid furnitureId,
            string templateId,
            string name,
            FurnitureCategory category,
            PlacementMode placementMode,
            float x, float y, float z,
            float rotation = 0f,
            bool hasStorage = false,
            int storageCapacity = 0,
            bool isCraftingStation = false,
            string craftingStationType = null)
        {
            FurnitureId = furnitureId;
            TemplateId = templateId ?? throw new ArgumentNullException(nameof(templateId));
            Name = name ?? "Unnamed Furniture";
            Category = category;
            PlacementMode = placementMode;
            PositionX = x;
            PositionY = y;
            PositionZ = z;
            Rotation = rotation;
            HasStorage = hasStorage;
            StorageCapacity = storageCapacity;
            IsCraftingStation = isCraftingStation;
            CraftingStationType = craftingStationType;
        }
    }
}
