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
            Assert.That(result, Is.True, "Adding item to empty container should succeed");
            Assert.That(container.Count, Is.EqualTo(1), "Container count should be 1");
            Assert.That(container.Contains("item1"), Is.True, "Container should contain added item");
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
            Assert.That(result, Is.False, "Adding item to full container should fail");
            Assert.That(container.Count, Is.EqualTo(2), "Container count should remain 2");
            Assert.That(container.Contains("item3"), Is.False, "Container should not contain rejected item");
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
            Assert.That(result, Is.True, "Removing existing item should succeed");
            Assert.That(container.Count, Is.EqualTo(1), "Container count should be 1");
            Assert.That(container.Contains("item1"), Is.False, "Container should not contain removed item");
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
            Assert.That(result, Is.False, "Removing non-existent item should fail");
            Assert.That(container.Count, Is.EqualTo(1), "Container count should remain unchanged");
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
            Assert.That(result, Is.True, "Removing at valid index should succeed");
            Assert.That(container.Count, Is.EqualTo(2), "Container count should be 2");
            Assert.That(container.Contains("item2"), Is.False, "Container should not contain removed item");
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
            Assert.That(result, Is.False, "Removing at invalid index should fail");
            Assert.That(container.Count, Is.EqualTo(1), "Container count should remain unchanged");
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
            Assert.That(container.IsEmpty, Is.True, "Container should be empty after clear");
            Assert.That(container.Count, Is.EqualTo(0), "Container count should be 0");
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
            Assert.That(result, Is.True, "Container should contain existing item");
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
            Assert.That(previous, Is.EqualTo(default(string)), "Previous item should be default for empty slot");
            Assert.That(slot.IsOccupied, Is.True, "Slot should be occupied after equip");
            Assert.That(slot.EquippedItem, Is.EqualTo("sword"), "Slot should contain equipped item");
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
            Assert.That(previous, Is.EqualTo("sword"), "Previous item should be returned");
            Assert.That(slot.IsOccupied, Is.True, "Slot should still be occupied");
            Assert.That(slot.EquippedItem, Is.EqualTo("axe"), "Slot should contain new item");
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
            Assert.That(unequipped, Is.EqualTo("sword"), "Unequipped item should be returned");
            Assert.That(slot.IsOccupied, Is.False, "Slot should be empty after unequip");
            Assert.That(slot.EquippedItem, Is.EqualTo(default(string)), "Equipped item should be default");
        }

        [Test]
        public void Unequip_EmptySlot_ShouldReturnDefault()
        {
            // Arrange
            var slot = new EquipmentSlot<TestSlotType, string>(TestSlotType.Weapon);

            // Act
            string unequipped = slot.Unequip();

            // Assert
            Assert.That(unequipped, Is.EqualTo(default(string)), "Unequipping empty slot should return default");
            Assert.That(slot.IsOccupied, Is.False, "Slot should remain empty");
        }

        [Test]
        public void IsOccupied_EmptySlot_ShouldBeFalse()
        {
            // Arrange
            var slot = new EquipmentSlot<TestSlotType, string>(TestSlotType.Weapon);

            // Assert
            Assert.That(slot.IsOccupied, Is.False, "Empty slot should not be occupied");
        }

        [Test]
        public void IsOccupied_FilledSlot_ShouldBeTrue()
        {
            // Arrange
            var slot = new EquipmentSlot<TestSlotType, string>(TestSlotType.Weapon);
            slot.Equip("sword");

            // Assert
            Assert.That(slot.IsOccupied, Is.True, "Filled slot should be occupied");
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
            Assert.That(added, Is.EqualTo(3), "Should add full amount");
            Assert.That(stack.Quantity, Is.EqualTo(8), "Quantity should be 8");
        }

        [Test]
        public void AddToStack_OverMax_ShouldClamp()
        {
            // Arrange
            var stack = new ItemStack("potion", quantity: 8, maxStackSize: 10);

            // Act
            int added = stack.AddToStack(5);

            // Assert
            Assert.That(added, Is.EqualTo(2), "Should only add 2 to reach max");
            Assert.That(stack.Quantity, Is.EqualTo(10), "Quantity should be clamped to max");
            Assert.That(stack.IsFull, Is.True, "Stack should be full");
        }

        [Test]
        public void RemoveFromStack_ValidAmount_ShouldDecreaseQuantity()
        {
            // Arrange
            var stack = new ItemStack("potion", quantity: 8, maxStackSize: 10);

            // Act
            int removed = stack.RemoveFromStack(3);

            // Assert
            Assert.That(removed, Is.EqualTo(3), "Should remove full amount");
            Assert.That(stack.Quantity, Is.EqualTo(5), "Quantity should be 5");
        }

        [Test]
        public void RemoveFromStack_ExcessAmount_ShouldClamp()
        {
            // Arrange
            var stack = new ItemStack("potion", quantity: 3, maxStackSize: 10);

            // Act
            int removed = stack.RemoveFromStack(5);

            // Assert
            Assert.That(removed, Is.EqualTo(3), "Should only remove available amount");
            Assert.That(stack.Quantity, Is.EqualTo(0), "Quantity should be 0");
        }

        [Test]
        public void Split_ValidAmount_ShouldCreateNewStack()
        {
            // Arrange
            var stack = new ItemStack("potion", quantity: 10, maxStackSize: 99);

            // Act
            var newStack = stack.Split(4);

            // Assert
            Assert.That(newStack, Is.Not.Null, "New stack should be created");
            Assert.That(stack.Quantity, Is.EqualTo(6), "Original stack should have 6");
            Assert.That(newStack.Quantity, Is.EqualTo(4), "New stack should have 4");
            Assert.That(newStack.Item, Is.EqualTo(stack.Item), "Both stacks should have same item");
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
            Assert.That(remaining, Is.EqualTo(0), "Should merge all items");
            Assert.That(stack1.Quantity, Is.EqualTo(8), "Stack1 should have 8");
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
            Assert.That(component.PrimaryContainer, Is.Not.Null, "Primary container should be initialized");
            Assert.That(component.PrimaryContainer.Capacity, Is.EqualTo(20), "Primary capacity should be 20");
            Assert.That(component.EquipmentSlots.Count, Is.EqualTo(2), "Should have 2 equipment slots");
            Assert.That(component.EquipmentSlots.ContainsKey(TestSlotType.Weapon), Is.True, "Should have weapon slot");
            Assert.That(component.EquipmentSlots.ContainsKey(TestSlotType.Armor), Is.True, "Should have armor slot");
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
            Assert.That(added, Is.True, "Adding to primary container should succeed");
            Assert.That(removed, Is.True, "Removing from primary container should succeed");
            Assert.That(component.PrimaryContainer.IsEmpty, Is.True, "Primary container should be empty");
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
            Assert.That(unequipped, Is.EqualTo("sword"), "Unequipped item should be sword");
            Assert.That(slot.IsOccupied, Is.False, "Slot should be empty after unequip");
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
            Assert.That(component.AdditionalContainers.ContainsKey("bank"), Is.True, "Should contain bank container");
            Assert.That(component.AdditionalContainers["bank"].Capacity, Is.EqualTo(50), "Bank capacity should be 50");
        }
    }
}
