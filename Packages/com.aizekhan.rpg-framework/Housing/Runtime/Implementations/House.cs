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
    /// Implementation of IHouse representing a player-owned house.
    /// With an event publisher, raises FurniturePlaced/FurnitureRemoved/HouseUpgraded when those succeed
    /// (the acting entity is Guid.Empty: the house is not told who did it).
    /// </summary>
    public class House : IHouse
    {
        private readonly List<IFurniture> _placedFurniture;
        private readonly IEventPublisher _events;
        private HouseData _data;

        public Guid HouseId => _data.HouseId;
        public HouseType HouseType => _data.HouseType;
        public string Name => _data.Name;
        public int UpgradeLevel => _data.UpgradeLevel;
        public int MaxFurnitureCapacity => _data.MaxFurnitureCapacity;
        public IReadOnlyList<IFurniture> PlacedFurniture => _placedFurniture.AsReadOnly();
        public bool IsAccessible => _data.IsAccessible;
        public (float X, float Y, float Z) WorldLocation => (_data.WorldX, _data.WorldY, _data.WorldZ);

        /// <summary>
        /// Create a new house instance.
        /// </summary>
        public House(HouseData data, IEventPublisher events = null)
        {
            _events = events;
            _data = data;
            _placedFurniture = new List<IFurniture>();
        }

        public bool TryPlaceFurniture(IFurniture furniture)
        {
            if (furniture == null)
                return false;

            if (_placedFurniture.Count >= _data.MaxFurnitureCapacity)
                return false;

            if (_placedFurniture.Any(f => f.FurnitureId == furniture.FurnitureId))
                return false;

            _placedFurniture.Add(furniture);
            _data.CurrentFurnitureCount = _placedFurniture.Count;

            var position = furniture.Position;
            _events?.Publish(new FurniturePlacedEvent(
                HouseId,
                furniture.FurnitureId,
                furniture.TemplateId,
                furniture.Category,
                Guid.Empty,
                position.X, position.Y, position.Z));
            return true;
        }

        public bool TryRemoveFurniture(Guid furnitureId)
        {
            var furniture = _placedFurniture.FirstOrDefault(f => f.FurnitureId == furnitureId);
            if (furniture == null)
                return false;

            _placedFurniture.Remove(furniture);
            _data.CurrentFurnitureCount = _placedFurniture.Count;

            _events?.Publish(new FurnitureRemovedEvent(HouseId, furnitureId, Guid.Empty));
            return true;
        }

        public IFurniture GetFurniture(Guid furnitureId)
        {
            return _placedFurniture.FirstOrDefault(f => f.FurnitureId == furnitureId);
        }

        public bool TryUpgrade()
        {
            if (!CanUpgrade())
                return false;

            int previousLevel = _data.UpgradeLevel;
            int previousCapacity = _data.MaxFurnitureCapacity;

            _data.UpgradeLevel++;
            // Increase capacity with upgrades
            _data.MaxFurnitureCapacity += GetCapacityBonusPerLevel();

            _events?.Publish(new HouseUpgradedEvent(
                HouseId,
                previousLevel,
                _data.UpgradeLevel,
                previousCapacity,
                _data.MaxFurnitureCapacity,
                Guid.Empty));
            return true;
        }

        public bool CanUpgrade()
        {
            return _data.UpgradeLevel < _data.MaxUpgradeLevel;
        }

        /// <summary>
        /// Get the underlying data structure.
        /// </summary>
        public HouseData GetData()
        {
            return _data;
        }

        /// <summary>
        /// Update the data structure.
        /// </summary>
        public void UpdateData(HouseData data)
        {
            _data = data;
        }

        private int GetCapacityBonusPerLevel()
        {
            return HouseType switch
            {
                HouseType.Apartment => 10,
                HouseType.Cottage => 15,
                HouseType.Manor => 25,
                HouseType.Castle => 40,
                HouseType.GuildHall => 50,
                _ => 10
            };
        }
    }
}
