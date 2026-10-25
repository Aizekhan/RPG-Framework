using System;
using RPGFramework.Housing.Runtime.Data;

namespace RPGFramework.Housing.Runtime.Contracts
{
    /// <summary>
    /// Represents a placeable furniture item with position, rotation, and functional properties.
    /// </summary>
    public interface IFurniture
    {
        /// <summary>
        /// Unique identifier for this furniture instance.
        /// </summary>
        Guid FurnitureId { get; }

        /// <summary>
        /// Template ID for the furniture type.
        /// </summary>
        string TemplateId { get; }

        /// <summary>
        /// Display name of the furniture.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Category of furniture (Decoration, Storage, Crafting, etc.).
        /// </summary>
        FurnitureCategory Category { get; }

        /// <summary>
        /// How this furniture can be placed (Floor, Wall, Ceiling, Freestanding).
        /// </summary>
        PlacementMode PlacementMode { get; }

        /// <summary>
        /// Position in house local space.
        /// </summary>
        (float X, float Y, float Z) Position { get; }

        /// <summary>
        /// Rotation in degrees (Yaw around Y-axis).
        /// </summary>
        float Rotation { get; }

        /// <summary>
        /// Whether this furniture provides storage functionality.
        /// </summary>
        bool HasStorage { get; }

        /// <summary>
        /// Storage capacity if this is a storage item.
        /// </summary>
        int StorageCapacity { get; }

        /// <summary>
        /// Whether this furniture provides crafting station functionality.
        /// </summary>
        bool IsCraftingStation { get; }

        /// <summary>
        /// Type of crafting station (if applicable).
        /// </summary>
        string CraftingStationType { get; }

        /// <summary>
        /// Update the position of this furniture.
        /// </summary>
        void SetPosition(float x, float y, float z);

        /// <summary>
        /// Update the rotation of this furniture.
        /// </summary>
        void SetRotation(float rotation);
    }
}
