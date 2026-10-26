using System;
using System.Collections.Generic;
using RPGFramework.Housing.Runtime.Data;

namespace RPGFramework.Housing.Runtime.Contracts
{
    /// <summary>
    /// Represents a player-owned house or building with furniture placement and upgrade capabilities.
    /// </summary>
    public interface IHouse
    {
        /// <summary>
        /// Unique identifier for this house instance.
        /// </summary>
        Guid HouseId { get; }

        /// <summary>
        /// The type of house (Apartment, Cottage, Manor, etc.).
        /// </summary>
        HouseType HouseType { get; }

        /// <summary>
        /// Display name of the house.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Current upgrade level of the house.
        /// </summary>
        int UpgradeLevel { get; }

        /// <summary>
        /// Maximum furniture capacity based on house type and upgrades.
        /// </summary>
        int MaxFurnitureCapacity { get; }

        /// <summary>
        /// All furniture placed in this house.
        /// </summary>
        IReadOnlyList<IFurniture> PlacedFurniture { get; }

        /// <summary>
        /// Whether the house can be entered/accessed.
        /// </summary>
        bool IsAccessible { get; }

        /// <summary>
        /// World location of the house (if applicable).
        /// </summary>
        (float X, float Y, float Z) WorldLocation { get; }

        /// <summary>
        /// Place a furniture item in the house.
        /// </summary>
        bool TryPlaceFurniture(IFurniture furniture);

        /// <summary>
        /// Remove furniture from the house.
        /// </summary>
        bool TryRemoveFurniture(Guid furnitureId);

        /// <summary>
        /// Get furniture by ID.
        /// </summary>
        IFurniture GetFurniture(Guid furnitureId);

        /// <summary>
        /// Upgrade the house to the next level.
        /// </summary>
        bool TryUpgrade();

        /// <summary>
        /// Check if an upgrade is available.
        /// </summary>
        bool CanUpgrade();
    }
}
