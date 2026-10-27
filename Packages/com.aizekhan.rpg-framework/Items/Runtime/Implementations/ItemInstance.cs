using System;
using System.Collections.Generic;
using RPGFramework.Events;

namespace RPGFramework.Items
{
    /// <summary>
    /// Default implementation of IItemInstance.
    /// Represents a unique instance of an item with runtime state.
    /// With an event publisher, raises <see cref="ItemCreatedEvent"/> when constructed.
    /// </summary>
    public class ItemInstance : IItemInstance
    {
        public string InstanceId { get; }
        public string ItemDefinitionId { get; }
        public int Quantity { get; set; }
        public int CurrentDurability { get; set; }
        public float CreatedAt { get; }
        public Dictionary<string, object> CustomProperties { get; }

        /// <summary>
        /// Create a new item instance.
        /// </summary>
        /// <param name="itemDefinitionId">Item definition ID from registry</param>
        /// <param name="quantity">Initial stack quantity (default: 1)</param>
        /// <param name="currentDurability">Initial durability (default: -1 = not applicable)</param>
        /// <param name="createdAt">Creation timestamp (default: 0)</param>
        /// <param name="events">Optional publisher for <see cref="ItemCreatedEvent"/>.</param>
        public ItemInstance(
            string itemDefinitionId,
            int quantity = 1,
            int currentDurability = -1,
            float createdAt = 0f,
            IEventPublisher events = null)
        {
            InstanceId = Guid.NewGuid().ToString();
            ItemDefinitionId = itemDefinitionId;
            Quantity = quantity;
            CurrentDurability = currentDurability;
            CreatedAt = createdAt;
            CustomProperties = new Dictionary<string, object>();

            events?.Publish(new ItemCreatedEvent
            {
                InstanceId = InstanceId,
                ItemDefinitionId = ItemDefinitionId,
                Quantity = Quantity,
                Timestamp = CreatedAt
            });
        }
    }
}
