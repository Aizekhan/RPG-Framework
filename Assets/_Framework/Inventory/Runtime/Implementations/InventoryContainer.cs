// Assets/_Framework/Inventory/Runtime/Implementations/InventoryContainer.cs

using System.Collections.Generic;

namespace RPGFramework.Inventory.Runtime
{
    /// <summary>
    /// Default implementation of IInventoryContainer.
    /// Uses a List for storage with optional capacity constraints.
    /// </summary>
    /// <typeparam name="TItem">Type of items stored</typeparam>
    public class InventoryContainer<TItem> : IInventoryContainer<TItem>
    {
        private readonly List<TItem> _items;
        private readonly int _capacity;

        /// <summary>
        /// Creates a new inventory container.
        /// </summary>
        /// <param name="capacity">Maximum capacity (-1 for unlimited)</param>
        public InventoryContainer(int capacity = -1)
        {
            _capacity = capacity;
            _items = capacity > 0 ? new List<TItem>(capacity) : new List<TItem>();
        }

        public int Capacity => _capacity;
        public int Count => _items.Count;
        public bool IsFull => _capacity > 0 && _items.Count >= _capacity;
        public bool IsEmpty => _items.Count == 0;

        public bool Add(TItem item)
        {
            if (IsFull) return false;
            _items.Add(item);
            return true;
        }

        public bool Remove(TItem item)
        {
            return _items.Remove(item);
        }

        public bool RemoveAt(int index)
        {
            if (index < 0 || index >= _items.Count) return false;
            _items.RemoveAt(index);
            return true;
        }

        public bool Contains(TItem item)
        {
            return _items.Contains(item);
        }

        public TItem GetAt(int index)
        {
            if (index < 0 || index >= _items.Count) return default(TItem);
            return _items[index];
        }

        public void Clear()
        {
            _items.Clear();
        }

        public IReadOnlyList<TItem> GetAllItems()
        {
            return _items.AsReadOnly();
        }
    }
}
