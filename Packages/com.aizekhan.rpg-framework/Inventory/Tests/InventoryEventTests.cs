using System.Collections.Generic;
using NUnit.Framework;
using RPGFramework.Events;
using RPGFramework.Inventory.Runtime;

namespace RPGFramework.Inventory.Tests
{
    public sealed class InventoryEventTests
    {
        private enum Slot
        {
            Head,
            Weapon
        }

        [Test]
        public void Container_PublishesAddAndRemoveForItsOwner()
        {
            var bus = new EventBus();
            var log = new List<string>();
            bus.Subscribe<ItemAddedEvent<string>>(e => log.Add($"+{e.Item} {e.EntityId}/{e.ContainerName}"));
            bus.Subscribe<ItemRemovedEvent<string>>(e => log.Add($"-{e.Item}"));
            var bag = new InventoryContainer<string>(capacity: 2, events: bus, ownerEntityId: 4, name: "Bag");

            bag.Add("potion");
            bag.Add("sword");
            bag.Add("shield"); // full: no event
            bag.RemoveAt(0);
            bag.Remove("missing");
            bag.Clear();

            CollectionAssert.AreEqual(new[] { "+potion 4/Bag", "+sword 4/Bag", "-potion", "-sword" }, log);
        }

        [Test]
        public void Slot_PublishesEquipAndUnequip()
        {
            var bus = new EventBus();
            var log = new List<string>();
            bus.Subscribe<ItemEquippedEvent<Slot, string>>(e => log.Add($"equip {e.SlotType} {e.EquippedItem} (was {e.PreviousItem ?? "-"})"));
            bus.Subscribe<ItemUnequippedEvent<Slot, string>>(e => log.Add($"unequip {e.SlotType} {e.UnequippedItem}"));
            var slot = new EquipmentSlot<Slot, string>(Slot.Weapon, events: bus, ownerEntityId: 4);

            slot.Equip("axe");
            slot.Equip("bow");
            slot.Unequip();
            slot.Unequip(); // empty: no event

            CollectionAssert.AreEqual(new[]
            {
                "equip Weapon axe (was -)", "equip Weapon bow (was axe)", "unequip Weapon bow"
            }, log);
        }
    }
}
