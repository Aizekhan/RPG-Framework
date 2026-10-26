// Packages/com.aizekhan.rpg-framework/Inventory/Runtime/Implementations/EquipmentSlot.cs

using System;
using System.Collections.Generic;
using RPGFramework.Events;

namespace RPGFramework.Inventory.Runtime
{
    /// <summary>
    /// Default implementation of IEquipmentSlot.
    /// Supports optional validation via delegate.
    /// With an event publisher, raises ItemEquipped / ItemUnequipped for the owning entity.
    /// </summary>
    /// <typeparam name="TSlotType">Enum type for slot categories</typeparam>
    /// <typeparam name="TItem">Type of items that can be equipped</typeparam>
    public class EquipmentSlot<TSlotType, TItem> : IEquipmentSlot<TSlotType, TItem>
        where TSlotType : Enum
    {
        private TItem _equippedItem;
        private readonly TSlotType _slotType;
        private readonly Func<TItem, bool> _canEquipValidator;
        private readonly IEventPublisher _events;
        private readonly int _ownerEntityId;
        private readonly Func<float> _clock;

        /// <summary>
        /// Creates a new equipment slot.
        /// </summary>
        /// <param name="slotType">Type of this slot</param>
        /// <param name="canEquipValidator">Optional validation function (null = always allow)</param>
        /// <param name="events">Optional publisher for equipment events</param>
        /// <param name="ownerEntityId">Entity reported in events</param>
        /// <param name="clock">Event timestamp source (game time, default 0)</param>
        public EquipmentSlot(TSlotType slotType, Func<TItem, bool> canEquipValidator = null,
            IEventPublisher events = null, int ownerEntityId = -1, Func<float> clock = null)
        {
            _slotType = slotType;
            _equippedItem = default(TItem);
            _canEquipValidator = canEquipValidator;
            _events = events;
            _ownerEntityId = ownerEntityId;
            _clock = clock;
        }

        public TSlotType SlotType => _slotType;
        public TItem EquippedItem => _equippedItem;
        public bool IsOccupied => !EqualityComparer<TItem>.Default.Equals(_equippedItem, default(TItem));

        public TItem Equip(TItem item)
        {
            TItem previous = _equippedItem;
            _equippedItem = item;
            _events?.Publish(new ItemEquippedEvent<TSlotType, TItem>
            {
                EntityId = _ownerEntityId,
                SlotType = _slotType,
                EquippedItem = item,
                PreviousItem = previous,
                Timestamp = _clock?.Invoke() ?? 0f
            });
            return previous;
        }

        public TItem Unequip()
        {
            bool wasOccupied = IsOccupied;
            TItem previous = _equippedItem;
            _equippedItem = default(TItem);
            if (wasOccupied)
            {
                _events?.Publish(new ItemUnequippedEvent<TSlotType, TItem>
                {
                    EntityId = _ownerEntityId,
                    SlotType = _slotType,
                    UnequippedItem = previous,
                    Timestamp = _clock?.Invoke() ?? 0f
                });
            }
            return previous;
        }

        public bool CanEquip(TItem item)
        {
            return _canEquipValidator?.Invoke(item) ?? true;
        }
    }
}
