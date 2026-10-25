// Assets/_Framework/Inventory/Runtime/Implementations/EquipmentSlot.cs

using System;
using System.Collections.Generic;

namespace RPGFramework.Inventory.Runtime
{
    /// <summary>
    /// Default implementation of IEquipmentSlot.
    /// Supports optional validation via delegate.
    /// </summary>
    /// <typeparam name="TSlotType">Enum type for slot categories</typeparam>
    /// <typeparam name="TItem">Type of items that can be equipped</typeparam>
    public class EquipmentSlot<TSlotType, TItem> : IEquipmentSlot<TSlotType, TItem>
        where TSlotType : Enum
    {
        private TItem _equippedItem;
        private readonly TSlotType _slotType;
        private readonly Func<TItem, bool> _canEquipValidator;

        /// <summary>
        /// Creates a new equipment slot.
        /// </summary>
        /// <param name="slotType">Type of this slot</param>
        /// <param name="canEquipValidator">Optional validation function (null = always allow)</param>
        public EquipmentSlot(TSlotType slotType, Func<TItem, bool> canEquipValidator = null)
        {
            _slotType = slotType;
            _equippedItem = default(TItem);
            _canEquipValidator = canEquipValidator;
        }

        public TSlotType SlotType => _slotType;
        public TItem EquippedItem => _equippedItem;
        public bool IsOccupied => !EqualityComparer<TItem>.Default.Equals(_equippedItem, default(TItem));

        public TItem Equip(TItem item)
        {
            TItem previous = _equippedItem;
            _equippedItem = item;
            return previous;
        }

        public TItem Unequip()
        {
            TItem previous = _equippedItem;
            _equippedItem = default(TItem);
            return previous;
        }

        public bool CanEquip(TItem item)
        {
            return _canEquipValidator?.Invoke(item) ?? true;
        }
    }
}
