using System;
using RPGFramework.Housing.Runtime.Data;

namespace RPGFramework.Housing.Runtime.Contracts
{
    /// <summary>
    /// Represents a land plot where houses can be constructed.
    /// </summary>
    public interface IBuildingPlot
    {
        /// <summary>
        /// Unique identifier for this plot.
        /// </summary>
        Guid PlotId { get; }

        /// <summary>
        /// Display name of the plot location.
        /// </summary>
        string LocationName { get; }

        /// <summary>
        /// World coordinates of this plot.
        /// </summary>
        (float X, float Y, float Z) WorldLocation { get; }

        /// <summary>
        /// Size of the plot (affects max house type).
        /// </summary>
        float PlotSize { get; }

        /// <summary>
        /// Whether this plot is currently owned.
        /// </summary>
        bool IsOwned { get; }

        /// <summary>
        /// Owner entity ID (if owned).
        /// </summary>
        Guid? OwnerId { get; }

        /// <summary>
        /// House built on this plot (if any).
        /// </summary>
        Guid? HouseId { get; }

        /// <summary>
        /// Base purchase price for this plot.
        /// </summary>
        int PurchasePrice { get; }

        /// <summary>
        /// Check if a house type can be built on this plot.
        /// </summary>
        bool CanBuildHouseType(HouseType houseType);

        /// <summary>
        /// Assign ownership of this plot.
        /// </summary>
        void SetOwner(Guid ownerId);

        /// <summary>
        /// Assign a house to this plot.
        /// </summary>
        void SetHouse(Guid houseId);

        /// <summary>
        /// Clear ownership and house.
        /// </summary>
        void Reset();
    }
}
