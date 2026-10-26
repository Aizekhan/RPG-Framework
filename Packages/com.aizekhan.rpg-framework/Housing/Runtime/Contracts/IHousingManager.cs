using System;
using System.Collections.Generic;
using RPGFramework.Housing.Runtime.Data;

namespace RPGFramework.Housing.Runtime.Contracts
{
    /// <summary>
    /// Central manager for all houses in the game world.
    /// </summary>
    public interface IHousingManager
    {
        /// <summary>
        /// All houses managed by this system.
        /// </summary>
        IReadOnlyList<IHouse> AllHouses { get; }

        /// <summary>
        /// Register a new house.
        /// </summary>
        void RegisterHouse(IHouse house, Guid ownerId);

        /// <summary>
        /// Unregister a house.
        /// </summary>
        void UnregisterHouse(Guid houseId);

        /// <summary>
        /// Get a house by ID.
        /// </summary>
        IHouse GetHouse(Guid houseId);

        /// <summary>
        /// Get all houses owned by an entity.
        /// </summary>
        IReadOnlyList<IHouse> GetHousesOwnedBy(Guid entityId);

        /// <summary>
        /// Get all houses where an entity has any permission.
        /// </summary>
        IReadOnlyList<IHouse> GetAccessibleHouses(Guid entityId);

        /// <summary>
        /// Transfer house ownership.
        /// </summary>
        bool TryTransferOwnership(Guid houseId, Guid newOwnerId);

        /// <summary>
        /// Purchase a house on a building plot.
        /// </summary>
        IHouse PurchaseHouse(IBuildingPlot plot, Guid buyerId, HouseType houseType);

        /// <summary>
        /// Get permissions for a house.
        /// </summary>
        IHousePermissions GetPermissions(Guid houseId);

        /// <summary>
        /// Get all storage containers in a house.
        /// </summary>
        IReadOnlyList<IHouseStorage> GetHouseStorage(Guid houseId);
    }
}
