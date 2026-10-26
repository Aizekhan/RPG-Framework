using System;
using RPGFramework.Housing.Runtime.Contracts;
using RPGFramework.Housing.Runtime.Data;

namespace RPGFramework.Housing.Runtime.Implementations
{
    /// <summary>
    /// Implementation of IBuildingPlot representing a land plot for construction.
    /// </summary>
    public class BuildingPlot : IBuildingPlot
    {
        public Guid PlotId { get; }
        public string LocationName { get; }
        public (float X, float Y, float Z) WorldLocation { get; }
        public float PlotSize { get; }
        public bool IsOwned { get; private set; }
        public Guid? OwnerId { get; private set; }
        public Guid? HouseId { get; private set; }
        public int PurchasePrice { get; }

        /// <summary>
        /// Create a new building plot instance.
        /// </summary>
        public BuildingPlot(
            Guid plotId,
            string locationName,
            float worldX,
            float worldY,
            float worldZ,
            float plotSize,
            int purchasePrice)
        {
            PlotId = plotId;
            LocationName = locationName ?? "Unknown Location";
            WorldLocation = (worldX, worldY, worldZ);
            PlotSize = plotSize;
            PurchasePrice = purchasePrice;
            IsOwned = false;
            OwnerId = null;
            HouseId = null;
        }

        public bool CanBuildHouseType(HouseType houseType)
        {
            // Plot size requirements for different house types
            return houseType switch
            {
                HouseType.Apartment => PlotSize >= 10f,
                HouseType.Cottage => PlotSize >= 20f,
                HouseType.Manor => PlotSize >= 40f,
                HouseType.Castle => PlotSize >= 80f,
                HouseType.GuildHall => PlotSize >= 100f,
                _ => false
            };
        }

        public void SetOwner(Guid ownerId)
        {
            OwnerId = ownerId;
            IsOwned = true;
        }

        public void SetHouse(Guid houseId)
        {
            HouseId = houseId;
        }

        public void Reset()
        {
            IsOwned = false;
            OwnerId = null;
            HouseId = null;
        }
    }
}
