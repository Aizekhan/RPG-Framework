// Assets/_Framework/Inventory/Runtime/Implementations/InventoryContainer.cs

using System;
using System.Collections.Generic;
using RPGFramework.Events;

namespace RPGFramework.Inventory.Runtime
{
    /// <summary>
    /// Default implementation of IInventoryContainer.
    /// Uses a List for storage with optional capacity constraints.
    /// With an event publisher, raises ItemAdded / ItemRemoved (quantity 1 per item) for the owning entity.
    /// </summary>
    /// <typeparam name="TItem">Type of items stored</typeparam>
    public class InventoryContainer<TItem> : IInventoryContainer<TItem>
    {
        private readonly List<TItem> _items;
        private readonly int _capacity;
        private readonly IEventPublisher _events;
        private readonly int _ownerEntityId;
        private readonly string _name;
        private readonly Func<float> _clock;

        /// <summary>
        /// Creates a new inventory container.
        /// </summary>
        /// <param name="capacity">Maximum capacity (-1 for unlimited)</param>
        /// <param name="events">Optional publisher for item events</param>
        /// <param name="ownerEntityId">Entity reported in events</param>
        /// <param name="name">Container name reported in events (e.g. "Backpack", "Bank")</param>
        /// <param name="clock">Event timestamp source (game time, default 0)</param>
        public InventoryContainer(int capacity = -1, IEventPublisher events = null, int ownerEntityId = -1,
            string name = null, Func<float> clock = null)
        {
            _capacity = capacity;
            _items = capacity > 0 ? new List<TItem>(capacity) : new List<TItem>();
            _events = events;
            _ownerEntityId = ownerEntityId;
            _name = name;
            _clock = clock;
        }

        public int Capacity => _capacity;
        public int Count => _items.Count;
        public bool IsFull => _capacity > 0 && _items.Count >= _capacity;
        public bool IsEmpty => _items.Count == 0;

        public bool Add(TItem item)
        {
            if (IsFull) return false;
            _items.Add(item);
            _events?.Publish(new ItemAddedEvent<TItem>
            {
                EntityId = _ownerEntityId,
                ContainerName = _name,
                Item = item,
                Quantity = 1,
                Timestamp = _clock?.Invoke() ?? 0f
            });
            return true;
        }

        public bool Remove(TItem item)
        {
            if (!_items.Remove(item)) return false;
            PublishRemoved(item);
            return true;
        }

        public bool RemoveAt(int index)
        {
            if (index < 0 || index >= _items.Count) return false;
            var item = _items[index];
            _items.RemoveAt(index);
            PublishRemoved(item);
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
            if (_events == null || _items.Count == 0)
            {
                _items.Clear();
                return;
            }
            var removed = _items.ToArray();
            _items.Clear();
            foreach (var item in removed)
                PublishRemoved(item);
        }

        public IReadOnlyList<TItem> GetAllItems()
        {
            return _items.AsReadOnly();
        }

        private void PublishRemoved(TItem item)
        {
            _events?.Publish(new ItemRemovedEvent<TItem>
            {
                EntityId = _ownerEntityId,
                ContainerName = _name,
                Item = item,
                Quantity = 1,
                Timestamp = _clock?.Invoke() ?? 0f
            });
        }
    }
}
