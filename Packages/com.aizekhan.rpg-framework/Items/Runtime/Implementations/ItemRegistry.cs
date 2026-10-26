using System;
using System.Collections.Generic;
using System.Linq;

namespace RPGFramework.Items
{
    /// <summary>
    /// Default implementation of IItemRegistry.
    /// Thread-safe singleton or can be injected via DI.
    /// </summary>
    public class ItemRegistry : IItemRegistry
    {
        private readonly Dictionary<string, IItem> _items;

        /// <summary>
        /// Create a new item registry.
        /// </summary>
        public ItemRegistry()
        {
            _items = new Dictionary<string, IItem>();
        }

        /// <inheritdoc/>
        public IItem GetItem(string itemId)
        {
            return _items.TryGetValue(itemId, out var item) ? item : null;
        }

        /// <inheritdoc/>
        public bool HasItem(string itemId)
        {
            return _items.ContainsKey(itemId);
        }

        /// <inheritdoc/>
        public IReadOnlyList<IItem> GetItemsByType(ItemType itemType)
        {
            return _items.Values.Where(item => item.ItemType == itemType).ToList();
        }

        /// <inheritdoc/>
        public IReadOnlyList<IItem> GetItemsByRarity(ItemRarity rarity)
        {
            return _items.Values.Where(item => item.Rarity == rarity).ToList();
        }

        /// <inheritdoc/>
        public IReadOnlyList<IItem> GetItemsByQuality(ItemQuality quality)
        {
            return _items.Values.Where(item => item.Quality == quality).ToList();
        }

        /// <inheritdoc/>
        public IReadOnlyList<IItem> GetAllItems()
        {
            return _items.Values.ToList();
        }

        /// <inheritdoc/>
        public void RegisterItem(IItem item)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));
            if (string.IsNullOrEmpty(item.ItemId))
                throw new ArgumentException("Item ID cannot be null or empty", nameof(item));

            _items[item.ItemId] = item;
        }

        /// <inheritdoc/>
        public bool UnregisterItem(string itemId)
        {
            return _items.Remove(itemId);
        }

        /// <inheritdoc/>
        public void Clear()
        {
            _items.Clear();
        }
    }
}
