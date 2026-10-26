// Assets/_Framework/Inventory/Tests/InventorySystemTests.cs

using NUnit.Framework;
using RPGFramework.Inventory.Runtime;

namespace RPGFramework.Inventory.Tests
{
    /// <summary>
    /// Comprehensive tests for IInventoryContainer implementations.
    /// </summary>
    [TestFixture]
    public class InventoryContainerTests
    {
        [Test]
        public void AddItem_ToEmptyContainer_ShouldSucceed()
        {
            // Arrange
            var container = new InventoryContainer<string>(capacity: 10);

            // Act
            bool result = container.Add("item1");

            // Assert
            Assert.IsTrue(result, "Adding item to empty container should succeed");
            Assert.AreEqual(1, container.Count, "Container count should be 1");
            Assert.IsTrue(container.Contains("item1"), "Container should contain added item");
        }

        [Test]
        public void AddItem_ToFullContainer_ShouldFail()
        {
            // Arrange
            var container = new InventoryContainer<string>(capacity: 2);
            container.Add("item1");
            container.Add("item2");

            // Act
            bool result = container.Add("item3");

            // Assert
            Assert.IsFalse(result, "Adding item to full container should fail");
            Assert.AreEqual(2, container.Count, "Container count should remain 2");
            Assert.IsFalse(container.Contains("item3"), "Container should not contain rejected item");
        }

        [Test]
        public void RemoveItem_ExistingItem_ShouldSucceed()
        {
            // Arrange
            var container = new InventoryContainer<string>(capacity: 10);
            container.Add("item1");
            container.Add("item2");

            // Act
            bool result = container.Remove("item1");

            // Assert
            Assert.IsTrue(result, "Removing existing item should succeed");
            Assert.AreEqual(1, container.Count, "Container count should be 1");
            Assert.IsFalse(container.Contains("item1"), "Container should not contain removed item");
        }

        [Test]
        public void RemoveItem_NonExistentItem_ShouldFail()
        {
            // Arrange
            var container = new InventoryContainer<string>(capacity: 10);
            container.Add("item1");

            // Act
            bool result = container.Remove("item2");

            // Assert
            Assert.IsFalse(result, "Removing non-existent item should fail");
            Assert.AreEqual(1, container.Count, "Container count should remain unchanged");
        }

        [Test]
        public void RemoveAt_ValidIndex_ShouldRemoveItem()
        {
            // Arrange
            var container = new InventoryContainer<string>(capacity: 10);
            container.Add("item1");
            container.Add("item2");
            container.Add("item3");

            // Act
            bool result = container.RemoveAt(1); // Remove "item2"

            // Assert
            Assert.IsTrue(result, "Removing at valid index should succeed");
            Assert.AreEqual(2, container.Count, "Container count should be 2");
            Assert.IsFalse(container.Contains("item2"), "Container should not contain removed item");
        }

        [Test]
        public void RemoveAt_InvalidIndex_ShouldFail()
        {
            // Arrange
            var container = new InventoryContainer<string>(capacity: 10);
            container.Add("item1");

            // Act
            bool result = container.RemoveAt(5);

            // Assert
            Assert.IsFalse(result, "Removing at invalid index should fail");
            Assert.AreEqual(1, container.Count, "Container count should remain unchanged");
        }

        [Test]
        public void Clear_PopulatedContainer_ShouldEmpty()
        {
            // Arrange
            var container = new InventoryContainer<string>(capacity: 10);
            container.Add("item1");
            container.Add("item2");
            container.Add("item3");

            // Act
            container.Clear();

            // Assert
            Assert.IsTrue(container.IsEmpty, "Container should be empty after clear");
            Assert.AreEqual(0, container.Count, "Container count should be 0");
        }

        [Test]
        public void Contains_ExistingItem_ShouldReturnTrue()
        {
            // Arrange
            var container = new InventoryContainer<string>(capacity: 10);
            container.Add("item1");
            container.Add("item2");

            // Act
            bool result = container.Contains("item1");

            // Assert
            Assert.IsTrue(result, "Container should contain existing item");
        }
    }

    /// <summary>
    /// Comprehensive tests for IEquipmentSlot implementations.
    /// </summary>
    [TestFixture]
    public class EquipmentSlotTests
    {
        private enum TestSlotType
        {
            Weapon,
            Armor,
            Accessory
        }

        [Test]
        public void Equip_ToEmptySlot_ShouldSucceed()
        {
            // Arrange
            var slot = new EquipmentSlot<TestSlotType, string>(TestSlotType.Weapon);

            // Act
            string previous = slot.Equip("sword");

            // Assert
            Assert.AreEqual(default(string), previous, "Previous item should be default for empty slot");
            Assert.IsTrue(slot.IsOccupied, "Slot should be occupied after equip");
            Assert.AreEqual("sword", slot.EquippedItem, "Slot should contain equipped item");
        }

        [Test]
        public void Equip_ToOccupiedSlot_ShouldReplaceAndReturnPrevious()
        {
            // Arrange
            var slot = new EquipmentSlot<TestSlotType, string>(TestSlotType.Weapon);
            slot.Equip("sword");

            // Act
            string previous = slot.Equip("axe");

            // Assert
            Assert.AreEqual("sword", previous, "Previous item should be returned");
            Assert.IsTrue(slot.IsOccupied, "Slot should still be occupied");
            Assert.AreEqual("axe", slot.EquippedItem, "Slot should contain new item");
        }

        [Test]
        public void Unequip_OccupiedSlot_ShouldReturnItem()
        {
            // Arrange
            var slot = new EquipmentSlot<TestSlotType, string>(TestSlotType.Weapon);
            slot.Equip("sword");

            // Act
            string unequipped = slot.Unequip();

            // Assert
            Assert.AreEqual("sword", unequipped, "Unequipped item should be returned");
            Assert.IsFalse(slot.IsOccupied, "Slot should be empty after unequip");
            Assert.AreEqual(default(string), slot.EquippedItem, "Equipped item should be default");
        }

        [Test]
        public void Unequip_EmptySlot_ShouldReturnDefault()
        {
            // Arrange
            var slot = new EquipmentSlot<TestSlotType, string>(TestSlotType.Weapon);

            // Act
            string unequipped = slot.Unequip();

            // Assert
            Assert.AreEqual(default(string), unequipped, "Unequipping empty slot should return default");
            Assert.IsFalse(slot.IsOccupied, "Slot should remain empty");
        }

        [Test]
        public void IsOccupied_EmptySlot_ShouldBeFalse()
        {
            // Arrange
            var slot = new EquipmentSlot<TestSlotType, string>(TestSlotType.Weapon);

            // Assert
            Assert.IsFalse(slot.IsOccupied, "Empty slot should not be occupied");
        }

        [Test]
        public void IsOccupied_FilledSlot_ShouldBeTrue()
        {
            // Arrange
            var slot = new EquipmentSlot<TestSlotType, string>(TestSlotType.Weapon);
            slot.Equip("sword");

            // Assert
            Assert.IsTrue(slot.IsOccupied, "Filled slot should be occupied");
        }
    }

    /// <summary>
    /// Comprehensive tests for IItemStack implementations.
    /// </summary>
    [TestFixture]
    public class ItemStackTests
    {
        [Test]
        public void AddToStack_UnderMax_ShouldIncreaseQuantity()
        {
            // Arrange
            var stack = new ItemStack("potion", quantity: 5, maxStackSize: 10);

            // Act
            int added = stack.AddToStack(3);

            // Assert
            Assert.AreEqual(3, added, "Should add full amount");
            Assert.AreEqual(8, stack.Quantity, "Quantity should be 8");
        }

        [Test]
        public void AddToStack_OverMax_ShouldClamp()
        {
            // Arrange
            var stack = new ItemStack("potion", quantity: 8, maxStackSize: 10);

            // Act
            int added = stack.AddToStack(5);

            // Assert
            Assert.AreEqual(2, added, "Should only add 2 to reach max");
            Assert.AreEqual(10, stack.Quantity, "Quantity should be clamped to max");
            Assert.IsTrue(stack.IsFull, "Stack should be full");
        }

        [Test]
        public void RemoveFromStack_ValidAmount_ShouldDecreaseQuantity()
        {
            // Arrange
            var stack = new ItemStack("potion", quantity: 8, maxStackSize: 10);

            // Act
            int removed = stack.RemoveFromStack(3);

            // Assert
            Assert.AreEqual(3, removed, "Should remove full amount");
            Assert.AreEqual(5, stack.Quantity, "Quantity should be 5");
        }

        [Test]
        public void RemoveFromStack_ExcessAmount_ShouldClamp()
        {
            // Arrange
            var stack = new ItemStack("potion", quantity: 3, maxStackSize: 10);

            // Act
            int removed = stack.RemoveFromStack(5);

            // Assert
            Assert.AreEqual(3, removed, "Should only remove available amount");
            Assert.AreEqual(0, stack.Quantity, "Quantity should be 0");
        }

        [Test]
        public void Split_ValidAmount_ShouldCreateNewStack()
        {
            // Arrange
            var stack = new ItemStack("potion", quantity: 10, maxStackSize: 99);

            // Act
            var newStack = stack.Split(4);

            // Assert
            Assert.IsNotNull(newStack, "New stack should be created");
            Assert.AreEqual(6, stack.Quantity, "Original stack should have 6");
            Assert.AreEqual(4, newStack.Quantity, "New stack should have 4");
            Assert.AreEqual(stack.Item, newStack.Item, "Both stacks should have same item");
        }

        [Test]
        public void Merge_SameItem_ShouldCombineStacks()
        {
            // Arrange
            var stack1 = new ItemStack("potion", quantity: 5, maxStackSize: 10);
            var stack2 = new ItemStack("potion", quantity: 3, maxStackSize: 10);

            // Act
            int remaining = stack1.Merge(stack2);

            // Assert
            Assert.AreEqual(0, remaining, "Should merge all items");
            Assert.AreEqual(8, stack1.Quantity, "Stack1 should have 8");
        }
    }

    /// <summary>
    /// Comprehensive tests for InventoryComponent.
    /// </summary>
    [TestFixture]
    public class InventoryComponentTests
    {
        private enum TestSlotType
        {
            Weapon,
            Armor,
            Accessory
        }

        [Test]
        public void CreateComponent_WithCapacityAndSlots_ShouldInitialize()
        {
            // Act
            var component = InventoryComponent<string, TestSlotType>.Create(
                primaryCapacity: 20,
                TestSlotType.Weapon,
                TestSlotType.Armor
            );

            // Assert
            Assert.IsNotNull(component.PrimaryContainer, "Primary container should be initialized");
            Assert.AreEqual(20, component.PrimaryContainer.Capacity, "Primary capacity should be 20");
            Assert.AreEqual(2, component.EquipmentSlots.Count, "Should have 2 equipment slots");
            Assert.IsTrue(component.EquipmentSlots.ContainsKey(TestSlotType.Weapon), "Should have weapon slot");
            Assert.IsTrue(component.EquipmentSlots.ContainsKey(TestSlotType.Armor), "Should have armor slot");
        }

        [Test]
        public void PrimaryContainer_AddRemove_ShouldWork()
        {
            // Arrange
            var component = InventoryComponent<string, TestSlotType>.Create(10, TestSlotType.Weapon);

            // Act
            bool added = component.PrimaryContainer.Add("item1");
            bool removed = component.PrimaryContainer.Remove("item1");

            // Assert
            Assert.IsTrue(added, "Adding to primary container should succeed");
            Assert.IsTrue(removed, "Removing from primary container should succeed");
            Assert.IsTrue(component.PrimaryContainer.IsEmpty, "Primary container should be empty");
        }

        [Test]
        public void EquipmentSlots_EquipUnequip_ShouldWork()
        {
            // Arrange
            var component = InventoryComponent<string, TestSlotType>.Create(10, TestSlotType.Weapon);

            // Act
            var slot = component.EquipmentSlots[TestSlotType.Weapon];
            slot.Equip("sword");
            string unequipped = slot.Unequip();

            // Assert
            Assert.AreEqual("sword", unequipped, "Unequipped item should be sword");
            Assert.IsFalse(slot.IsOccupied, "Slot should be empty after unequip");
        }

        [Test]
        public void AdditionalContainers_AddContainer_ShouldWork()
        {
            // Arrange
            var component = InventoryComponent<string, TestSlotType>.Create(10, TestSlotType.Weapon);
            var bankContainer = new InventoryContainer<string>(50);

            // Act
            component.AdditionalContainers["bank"] = bankContainer;

            // Assert
            Assert.IsTrue(component.AdditionalContainers.ContainsKey("bank"), "Should contain bank container");
            Assert.AreEqual(50, component.AdditionalContainers["bank"].Capacity, "Bank capacity should be 50");
        }
    }
}
