using System;
using System.Collections.Generic;
using System.Linq;
using RPGFramework.Housing.Runtime.Contracts;

namespace RPGFramework.Housing.Runtime.Implementations
{
    /// <summary>
    /// Implementation of IHouseStorage managing storage containers.
    /// </summary>
    public class HouseStorage : IHouseStorage
    {
        private readonly List<Guid> _storedItemIds;

        public Guid HouseId { get; }
        public Guid FurnitureId { get; }
        public int Capacity { get; }
        public int CurrentItemCount => _storedItemIds.Count;
        public IReadOnlyList<Guid> StoredItemIds => _storedItemIds.AsReadOnly();
        public bool IsFull => _storedItemIds.Count >= Capacity;

        /// <summary>
        /// Create a new house storage instance.
        /// </summary>
        public HouseStorage(Guid houseId, Guid furnitureId, int capacity)
        {
            if (capacity < 0)
                throw new ArgumentException("Capacity cannot be negative", nameof(capacity));

            HouseId = houseId;
            FurnitureId = furnitureId;
            Capacity = capacity;
            _storedItemIds = new List<Guid>(capacity);
        }

        public bool TryAddItem(Guid itemId)
        {
            if (IsFull)
                return false;

            if (_storedItemIds.Contains(itemId))
                return false;

            _storedItemIds.Add(itemId);
            return true;
        }

        public bool TryRemoveItem(Guid itemId)
        {
            return _storedItemIds.Remove(itemId);
        }

        public bool ContainsItem(Guid itemId)
        {
            return _storedItemIds.Contains(itemId);
        }

        public void ClearStorage()
        {
            _storedItemIds.Clear();
        }
    }
}
