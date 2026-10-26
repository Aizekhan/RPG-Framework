using System.Collections.Generic;
using System.Linq;

namespace RPGFramework.Companions.Runtime
{
    /// <summary>
    /// Default implementation of ICompanionInventory.
    /// Manages companion's carried items and equipped gear.
    /// </summary>
    public class CompanionInventory : ICompanionInventory
    {
        private readonly Dictionary<string, int> _items;
        private readonly Dictionary<string, string> _equippedItems;
        private readonly int _capacity;

        public int Capacity => _capacity;
        public int ItemCount => _items.Count;

        public CompanionInventory(int capacity = 10)
        {
            _capacity = capacity;
            _items = new Dictionary<string, int>();
            _equippedItems = new Dictionary<string, string>();
        }

        public IReadOnlyList<string> GetAllItems()
        {
            return _items.Keys.ToList();
        }

        public bool AddItem(string itemId, int quantity = 1)
        {
            if (string.IsNullOrEmpty(itemId) || quantity <= 0)
                return false;

            if (_items.ContainsKey(itemId))
            {
                _items[itemId] += quantity;
                return true;
            }

            if (_items.Count >= _capacity)
                return false;

            _items[itemId] = quantity;
            return true;
        }

        public bool RemoveItem(string itemId, int quantity = 1)
        {
            if (!_items.ContainsKey(itemId))
                return false;

            _items[itemId] -= quantity;

            if (_items[itemId] <= 0)
            {
                _items.Remove(itemId);
            }

            return true;
        }

        public bool HasItem(string itemId)
        {
            return _items.ContainsKey(itemId);
        }

        public int GetItemQuantity(string itemId)
        {
            return _items.TryGetValue(itemId, out int quantity) ? quantity : 0;
        }

        public bool EquipItem(string itemId, string slotId)
        {
            if (string.IsNullOrEmpty(itemId) || string.IsNullOrEmpty(slotId))
                return false;

            if (!HasItem(itemId))
                return false;

            // Unequip existing item in slot if present
            if (_equippedItems.ContainsKey(slotId))
            {
                UnequipItem(slotId);
            }

            _equippedItems[slotId] = itemId;
            return true;
        }

        public string UnequipItem(string slotId)
        {
            if (!_equippedItems.TryGetValue(slotId, out string itemId))
                return null;

            _equippedItems.Remove(slotId);
            return itemId;
        }

        public string GetEquippedItem(string slotId)
        {
            return _equippedItems.TryGetValue(slotId, out string itemId) ? itemId : null;
        }

        public void Clear()
        {
            _items.Clear();
            _equippedItems.Clear();
        }

        public IReadOnlyDictionary<string, string> GetAllEquippedItems()
        {
            return _equippedItems;
        }
    }
}
