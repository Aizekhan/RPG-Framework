// Packages/com.aizekhan.rpg-framework/Inventory/Runtime/Components/InventoryComponent.cs

using System.Collections.Generic;
using RPGFramework.ECS;

namespace RPGFramework.Inventory.Runtime
{
    /// <summary>
    /// Generic inventory component for entities.
    /// Stores containers and equipment slots.
    /// Supports multiple inventory types via generics.
    /// </summary>
    /// <typeparam name="TItem">Type of items stored (string IDs, item instances, etc.)</typeparam>
    /// <typeparam name="TSlotType">Enum type for equipment slot categories</typeparam>
    public struct InventoryComponent<TItem, TSlotType> : IComponent
        where TSlotType : System.Enum
    {
        /// <summary>
        /// Primary container (backpack, bag, etc.)
        /// Main storage for unequipped items.
        /// </summary>
        public IInventoryContainer<TItem> PrimaryContainer;

        /// <summary>
        /// Equipment slots keyed by slot type.
        /// Allows flexible equipment configurations per entity.
        /// </summary>
        public Dictionary<TSlotType, IEquipmentSlot<TSlotType, TItem>> EquipmentSlots;

        /// <summary>
        /// Additional named containers (bank, quest items, key items, etc.)
        /// Optional for most entities.
        /// </summary>
        public Dictionary<string, IInventoryContainer<TItem>> AdditionalContainers;

        /// <summary>
        /// Factory method to create an entity with basic inventory.
        /// </summary>
        /// <param name="primaryCapacity">Capacity of primary container (-1 for unlimited)</param>
        /// <param name="equipmentSlotTypes">Equipment slot types to create</param>
        /// <returns>Initialized inventory component</returns>
        public static InventoryComponent<TItem, TSlotType> Create(int primaryCapacity, params TSlotType[] equipmentSlotTypes)
        {
            var component = new InventoryComponent<TItem, TSlotType>
            {
                PrimaryContainer = new InventoryContainer<TItem>(primaryCapacity),
                EquipmentSlots = new Dictionary<TSlotType, IEquipmentSlot<TSlotType, TItem>>(),
                AdditionalContainers = new Dictionary<string, IInventoryContainer<TItem>>()
            };

            foreach (var slotType in equipmentSlotTypes)
            {
                component.EquipmentSlots[slotType] = new EquipmentSlot<TSlotType, TItem>(slotType);
            }

            return component;
        }

        /// <summary>
        /// Factory method to create an entity with unlimited inventory.
        /// </summary>
        /// <param name="equipmentSlotTypes">Equipment slot types to create</param>
        /// <returns>Initialized inventory component with unlimited capacity</returns>
        public static InventoryComponent<TItem, TSlotType> CreateUnlimited(params TSlotType[] equipmentSlotTypes)
        {
            return Create(-1, equipmentSlotTypes);
        }

        /// <summary>
        /// Factory method to create a minimal inventory (no equipment slots).
        /// </summary>
        /// <param name="capacity">Primary container capacity</param>
        /// <returns>Initialized inventory component without equipment slots</returns>
        public static InventoryComponent<TItem, TSlotType> CreateSimple(int capacity)
        {
            return new InventoryComponent<TItem, TSlotType>
            {
                PrimaryContainer = new InventoryContainer<TItem>(capacity),
                EquipmentSlots = new Dictionary<TSlotType, IEquipmentSlot<TSlotType, TItem>>(),
                AdditionalContainers = new Dictionary<string, IInventoryContainer<TItem>>()
            };
        }
    }
}
