using System;
using System.Collections.Generic;
using System.Linq;
using RPGFramework.Events;
using RPGFramework.Housing.Runtime.Contracts;
using RPGFramework.Housing.Runtime.Data;
using RPGFramework.Housing.Runtime.Events;

namespace RPGFramework.Housing.Runtime.Implementations
{
    /// <summary>
    /// Implementation of IHousingManager managing all houses.
    /// With an event publisher, raises HousePurchased when a plot is bought, and hands the publisher to the houses it
    /// builds and the permissions it creates, so those raise their own furniture, upgrade and guest events.
    /// Houses built elsewhere and passed to <see cref="RegisterHouse"/> raise events only if given a publisher themselves.
    /// </summary>
    public class HousingManager : IHousingManager
    {
        private readonly Dictionary<Guid, IHouse> _houses;
        private readonly Dictionary<Guid, IHousePermissions> _permissions;
        private readonly Dictionary<Guid, List<IHouseStorage>> _storageContainers;
        private readonly Dictionary<Guid, Guid> _houseOwnership; // houseId -> ownerId
        private readonly IEventPublisher _events;

        public IReadOnlyList<IHouse> AllHouses => _houses.Values.ToList();

        /// <summary>
        /// Create a new housing manager instance.
        /// </summary>
        public HousingManager(IEventPublisher events = null)
        {
            _events = events;
            _houses = new Dictionary<Guid, IHouse>();
            _permissions = new Dictionary<Guid, IHousePermissions>();
            _storageContainers = new Dictionary<Guid, List<IHouseStorage>>();
            _houseOwnership = new Dictionary<Guid, Guid>();
        }

        public void RegisterHouse(IHouse house, Guid ownerId)
        {
            if (house == null)
                throw new ArgumentNullException(nameof(house));

            _houses[house.HouseId] = house;
            _houseOwnership[house.HouseId] = ownerId;

            var permissions = new HousePermissions(house.HouseId, ownerId, _events);
            _permissions[house.HouseId] = permissions;

            _storageContainers[house.HouseId] = new List<IHouseStorage>();
        }

        public void UnregisterHouse(Guid houseId)
        {
            _houses.Remove(houseId);
            _permissions.Remove(houseId);
            _storageContainers.Remove(houseId);
            _houseOwnership.Remove(houseId);
        }

        public IHouse GetHouse(Guid houseId)
        {
            return _houses.TryGetValue(houseId, out var house) ? house : null;
        }

        public IReadOnlyList<IHouse> GetHousesOwnedBy(Guid entityId)
        {
            return _houseOwnership
                .Where(kvp => kvp.Value == entityId)
                .Select(kvp => _houses[kvp.Key])
                .ToList();
        }

        public IReadOnlyList<IHouse> GetAccessibleHouses(Guid entityId)
        {
            var accessibleHouses = new List<IHouse>();

            foreach (var house in _houses.Values)
            {
                if (_permissions.TryGetValue(house.HouseId, out var permissions))
                {
                    if (permissions.GetPermissionLevel(entityId) > PermissionLevel.Banned)
                    {
                        accessibleHouses.Add(house);
                    }
                }
            }

            return accessibleHouses;
        }

        public bool TryTransferOwnership(Guid houseId, Guid newOwnerId)
        {
            if (!_houses.ContainsKey(houseId))
                return false;

            if (!_houseOwnership.ContainsKey(houseId))
                return false;

            var oldOwnerId = _houseOwnership[houseId];
            _houseOwnership[houseId] = newOwnerId;

            // Update permissions
            var permissions = new HousePermissions(houseId, newOwnerId, _events);
            _permissions[houseId] = permissions;

            return true;
        }

        public IHouse PurchaseHouse(IBuildingPlot plot, Guid buyerId, HouseType houseType)
        {
            if (plot == null)
                throw new ArgumentNullException(nameof(plot));

            if (plot.IsOwned)
                return null;

            if (!plot.CanBuildHouseType(houseType))
                return null;

            var houseId = Guid.NewGuid();
            var capacity = GetBaseCapacity(houseType);

            var houseData = new HouseData(
                houseId,
                houseType,
                $"{houseType} at {plot.LocationName}",
                capacity,
                plot.PlotId,
                plot.WorldLocation.X,
                plot.WorldLocation.Y,
                plot.WorldLocation.Z
            );

            var house = new House(houseData, _events);

            plot.SetOwner(buyerId);
            plot.SetHouse(houseId);

            RegisterHouse(house, buyerId);

            _events?.Publish(new HousePurchasedEvent(houseId, buyerId, plot.PlotId, houseType, plot.PurchasePrice));
            return house;
        }

        public IHousePermissions GetPermissions(Guid houseId)
        {
            return _permissions.TryGetValue(houseId, out var permissions) ? permissions : null;
        }

        public IReadOnlyList<IHouseStorage> GetHouseStorage(Guid houseId)
        {
            if (_storageContainers.TryGetValue(houseId, out var storage))
                return storage.AsReadOnly();

            return new List<IHouseStorage>().AsReadOnly();
        }

        /// <summary>
        /// Register a storage container for a house.
        /// </summary>
        public void RegisterStorage(IHouseStorage storage)
        {
            if (storage == null)
                throw new ArgumentNullException(nameof(storage));

            if (!_storageContainers.ContainsKey(storage.HouseId))
                _storageContainers[storage.HouseId] = new List<IHouseStorage>();

            _storageContainers[storage.HouseId].Add(storage);
        }

        /// <summary>
        /// Unregister a storage container.
        /// </summary>
        public void UnregisterStorage(Guid houseId, Guid furnitureId)
        {
            if (_storageContainers.TryGetValue(houseId, out var storageList))
            {
                storageList.RemoveAll(s => s.FurnitureId == furnitureId);
            }
        }

        private int GetBaseCapacity(HouseType houseType)
        {
            return houseType switch
            {
                HouseType.Apartment => 20,
                HouseType.Cottage => 40,
                HouseType.Manor => 80,
                HouseType.Castle => 150,
                HouseType.GuildHall => 200,
                _ => 20
            };
        }
    }
}
