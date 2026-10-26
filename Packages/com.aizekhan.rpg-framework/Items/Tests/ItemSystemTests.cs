using NUnit.Framework;
using RPGFramework.Items;
using RPGFramework.Stats;
using System.Collections.Generic;

namespace RPGFramework.Items.Tests
{
    /// <summary>
    /// Comprehensive test suite for RPG Framework Item System.
    /// Tests item definitions, equipment, consumables, instances, and registry.
    /// </summary>
    [TestFixture]
    public class ItemSystemTests
    {
        // Test enum types for equipment slots and stats
        private enum TestEquipmentSlot
        {
            Head,
            Chest,
            Weapon,
            Ring
        }

        private enum TestStatType
        {
            Strength,
            Defense,
            Intelligence
        }

        // Helper: Create a simple stat modifier
        private IStatModifier<TestStatType, float> CreateStatModifier(TestStatType statType, float value, StatModifierType modifierType = StatModifierType.Additive)
        {
            return new StatModifier<TestStatType, float>(
                modifierId: $"{statType}Mod",
                statType: statType,
                value: value,
                modifierType: modifierType,
                priority: 0
            );
        }

        #region ItemTests (6 tests)

        [Test]
        public void CreateItem_WithValidParameters_SetsPropertiesCorrectly()
        {
            // Arrange & Act
            var item = new Item(
                itemId: "item_001",
                name: "Test Item",
                description: "A test item",
                itemType: ItemType.Crafting,
                rarity: ItemRarity.Rare,
                quality: ItemQuality.Magical,
                maxStackSize: 50,
                itemLevel: 5,
                iconId: "icon_test"
            );

            // Assert
            Assert.That(item.ItemId, Is.EqualTo("item_001"), "ItemId should match");
            Assert.That(item.Name, Is.EqualTo("Test Item"), "Name should match");
            Assert.That(item.Description, Is.EqualTo("A test item"), "Description should match");
            Assert.That(item.ItemType, Is.EqualTo(ItemType.Crafting), "ItemType should match");
            Assert.That(item.Rarity, Is.EqualTo(ItemRarity.Rare), "Rarity should match");
            Assert.That(item.Quality, Is.EqualTo(ItemQuality.Magical), "Quality should match");
            Assert.That(item.MaxStackSize, Is.EqualTo(50), "MaxStackSize should match");
            Assert.That(item.ItemLevel, Is.EqualTo(5), "ItemLevel should match");
            Assert.That(item.IconId, Is.EqualTo("icon_test"), "IconId should match");
        }

        [Test]
        public void CreateItem_WithDefaults_UsesExpectedDefaults()
        {
            // Arrange & Act
            var item = new Item(
                itemId: "item_default",
                name: "Default Item",
                description: "Uses defaults",
                itemType: ItemType.Miscellaneous
            );

            // Assert
            Assert.That(item.Rarity, Is.EqualTo(ItemRarity.Common), "Default rarity should be Common");
            Assert.That(item.Quality, Is.EqualTo(ItemQuality.Normal), "Default quality should be Normal");
            Assert.That(item.MaxStackSize, Is.EqualTo(1), "Default max stack size should be 1");
            Assert.That(item.ItemLevel, Is.EqualTo(1), "Default item level should be 1");
            Assert.That(item.IconId, Is.EqualTo(""), "Default icon ID should be empty");
        }

        [Test]
        public void ItemType_Equipment_MatchesExpectedValue()
        {
            // Arrange & Act
            var item = new Item("eq_001", "Equipment Item", "Test", ItemType.Equipment);

            // Assert
            Assert.That(item.ItemType, Is.EqualTo(ItemType.Equipment), "ItemType should be Equipment");
        }

        [Test]
        public void ItemType_Consumable_MatchesExpectedValue()
        {
            // Arrange & Act
            var item = new Item("cons_001", "Consumable Item", "Test", ItemType.Consumable);

            // Assert
            Assert.That(item.ItemType, Is.EqualTo(ItemType.Consumable), "ItemType should be Consumable");
        }

        [Test]
        public void MaxStackSize_ForNonStackableItem_IsOne()
        {
            // Arrange & Act
            var item = new Item("unstackable", "Single Item", "Test", ItemType.Equipment, maxStackSize: 1);

            // Assert
            Assert.That(item.MaxStackSize, Is.EqualTo(1), "Non-stackable item should have MaxStackSize of 1");
        }

        [Test]
        public void MaxStackSize_ForStackableItem_IsGreaterThanOne()
        {
            // Arrange & Act
            var item = new Item("stackable", "Stack Item", "Test", ItemType.Consumable, maxStackSize: 20);

            // Assert
            Assert.That(item.MaxStackSize, Is.EqualTo(20), "Stackable item should have MaxStackSize greater than 1");
        }

        #endregion

        #region EquipmentTests (6 tests)

        [Test]
        public void CreateEquipment_WithStatModifiers_StoresModifiersCorrectly()
        {
            // Arrange
            var modifiers = new List<IStatModifier<TestStatType, float>>
            {
                CreateStatModifier(TestStatType.Strength, 10f),
                CreateStatModifier(TestStatType.Defense, 5f)
            };

            // Act
            var equipment = new Equipment<TestEquipmentSlot, TestStatType, float>(
                itemId: "helmet_001",
                name: "Iron Helmet",
                description: "A sturdy helmet",
                slotType: TestEquipmentSlot.Head,
                statModifiers: modifiers
            );

            // Assert
            Assert.That(equipment.StatModifiers.Count, Is.EqualTo(2), "Should have 2 stat modifiers");
            Assert.That(equipment.StatModifiers[0].StatType, Is.EqualTo(TestStatType.Strength), "First modifier should be Strength");
            Assert.That(equipment.StatModifiers[1].StatType, Is.EqualTo(TestStatType.Defense), "Second modifier should be Defense");
        }

        [Test]
        public void EquipmentSlotType_MatchesConstructorParameter()
        {
            // Arrange & Act
            var helmet = new Equipment<TestEquipmentSlot, TestStatType, float>(
                itemId: "helmet_002",
                name: "Steel Helmet",
                description: "Test",
                slotType: TestEquipmentSlot.Head,
                statModifiers: null
            );

            // Assert
            Assert.That(helmet.SlotType, Is.EqualTo(TestEquipmentSlot.Head), "SlotType should be Head");
        }

        [Test]
        public void MinimumLevel_SetCorrectly()
        {
            // Arrange & Act
            var equipment = new Equipment<TestEquipmentSlot, TestStatType, float>(
                itemId: "sword_legendary",
                name: "Legendary Sword",
                description: "Requires level 10",
                slotType: TestEquipmentSlot.Weapon,
                statModifiers: null,
                minimumLevel: 10
            );

            // Assert
            Assert.That(equipment.MinimumLevel, Is.EqualTo(10), "MinimumLevel should be 10");
        }

        [Test]
        public void MaxDurability_Indestructible_IsNegativeOne()
        {
            // Arrange & Act
            var equipment = new Equipment<TestEquipmentSlot, TestStatType, float>(
                itemId: "artifact_ring",
                name: "Eternal Ring",
                description: "Never breaks",
                slotType: TestEquipmentSlot.Ring,
                statModifiers: null,
                maxDurability: -1
            );

            // Assert
            Assert.That(equipment.MaxDurability, Is.EqualTo(-1), "Indestructible equipment should have MaxDurability of -1");
        }

        [Test]
        public void MaxDurability_Destructible_IsPositive()
        {
            // Arrange & Act
            var equipment = new Equipment<TestEquipmentSlot, TestStatType, float>(
                itemId: "iron_sword",
                name: "Iron Sword",
                description: "Can break",
                slotType: TestEquipmentSlot.Weapon,
                statModifiers: null,
                maxDurability: 100
            );

            // Assert
            Assert.That(equipment.MaxDurability, Is.EqualTo(100), "Destructible equipment should have positive MaxDurability");
        }

        [Test]
        public void EquipmentIsNotStackable_MaxStackSizeIsOne()
        {
            // Arrange & Act
            var equipment = new Equipment<TestEquipmentSlot, TestStatType, float>(
                itemId: "chest_001",
                name: "Plate Chest",
                description: "Test",
                slotType: TestEquipmentSlot.Chest,
                statModifiers: null
            );

            // Assert
            Assert.That(equipment.MaxStackSize, Is.EqualTo(1), "Equipment should always have MaxStackSize of 1");
        }

        #endregion

        #region ConsumableTests (5 tests)

        [Test]
        public void CreateConsumable_WithEffectId_StoresEffectCorrectly()
        {
            // Arrange & Act
            var potion = new Consumable(
                itemId: "potion_hp",
                name: "Health Potion",
                description: "Restores 50 HP",
                effectId: "RestoreHP",
                effectMagnitude: 50f
            );

            // Assert
            Assert.That(potion.EffectId, Is.EqualTo("RestoreHP"), "EffectId should match");
            Assert.That(potion.EffectMagnitude, Is.EqualTo(50f), "EffectMagnitude should be 50");
        }

        [Test]
        public void CooldownCategory_SetCorrectly()
        {
            // Arrange & Act
            var potion = new Consumable(
                itemId: "potion_mana",
                name: "Mana Potion",
                description: "Restores mana",
                effectId: "RestoreMana",
                effectMagnitude: 30f,
                cooldownCategory: "Potion",
                cooldownDuration: 10f
            );

            // Assert
            Assert.That(potion.CooldownCategory, Is.EqualTo("Potion"), "CooldownCategory should be Potion");
            Assert.That(potion.CooldownDuration, Is.EqualTo(10f), "CooldownDuration should be 10 seconds");
        }

        [Test]
        public void UsableInCombat_True_ByDefault()
        {
            // Arrange & Act
            var consumable = new Consumable(
                itemId: "elixir_001",
                name: "Combat Elixir",
                description: "Test",
                effectId: "Buff",
                effectMagnitude: 1f
            );

            // Assert
            Assert.That(consumable.UsableInCombat, Is.True, "UsableInCombat should be true by default");
        }

        [Test]
        public void IsConsumedOnUse_True_ByDefault()
        {
            // Arrange & Act
            var consumable = new Consumable(
                itemId: "scroll_001",
                name: "Scroll of Fire",
                description: "Test",
                effectId: "FireDamage",
                effectMagnitude: 100f
            );

            // Assert
            Assert.That(consumable.IsConsumedOnUse, Is.True, "IsConsumedOnUse should be true by default");
        }

        [Test]
        public void Consumable_IsStackable_MaxStackSizeGreaterThanOne()
        {
            // Arrange & Act
            var consumable = new Consumable(
                itemId: "food_bread",
                name: "Bread",
                description: "Test",
                effectId: "RestoreHP",
                effectMagnitude: 10f,
                maxStackSize: 20
            );

            // Assert
            Assert.That(consumable.MaxStackSize, Is.EqualTo(20), "Consumable should be stackable with MaxStackSize 20");
        }

        #endregion

        #region ItemInstanceTests (4 tests)

        [Test]
        public void CreateItemInstance_GeneratesUniqueInstanceId()
        {
            // Arrange & Act
            var instance1 = new ItemInstance("item_001");
            var instance2 = new ItemInstance("item_001");

            // Assert
            Assert.That(instance1.InstanceId, Is.Not.EqualTo(instance2.InstanceId), "InstanceIds should be unique");
            Assert.That(instance1.InstanceId, Is.Not.Null, "InstanceId should not be null");
            Assert.That(instance2.InstanceId, Is.Not.Null, "InstanceId should not be null");
        }

        [Test]
        public void ItemInstance_InitialQuantity_MatchesConstructorParameter()
        {
            // Arrange & Act
            var instance = new ItemInstance("stackable_item", quantity: 5);

            // Assert
            Assert.That(instance.Quantity, Is.EqualTo(5), "Quantity should be 5");
            Assert.That(instance.ItemDefinitionId, Is.EqualTo("stackable_item"), "ItemDefinitionId should match");
        }

        [Test]
        public void ItemInstance_CurrentDurability_MatchesConstructorParameter()
        {
            // Arrange & Act
            var instance = new ItemInstance("weapon_001", quantity: 1, currentDurability: 75);

            // Assert
            Assert.That(instance.CurrentDurability, Is.EqualTo(75), "CurrentDurability should be 75");
        }

        [Test]
        public void ItemInstance_CustomProperties_IsEmpty_Initially()
        {
            // Arrange & Act
            var instance = new ItemInstance("item_001");

            // Assert
            Assert.That(instance.CustomProperties, Is.Not.Null, "CustomProperties should not be null");
            Assert.That(instance.CustomProperties.Count, Is.EqualTo(0), "CustomProperties should be empty initially");
        }

        #endregion

        #region ItemRegistryTests (6 tests)

        [Test]
        public void RegisterItem_AddsItemToRegistry()
        {
            // Arrange
            var registry = new ItemRegistry();
            var item = new Item("item_001", "Test Item", "Test", ItemType.Miscellaneous);

            // Act
            registry.RegisterItem(item);

            // Assert
            Assert.That(registry.HasItem("item_001"), Is.True, "Registry should contain the registered item");
        }

        [Test]
        public void GetItem_ReturnsRegisteredItem()
        {
            // Arrange
            var registry = new ItemRegistry();
            var item = new Item("item_002", "Another Item", "Test", ItemType.Quest);
            registry.RegisterItem(item);

            // Act
            var retrieved = registry.GetItem("item_002");

            // Assert
            Assert.That(retrieved, Is.Not.Null, "Retrieved item should not be null");
            Assert.That(retrieved.ItemId, Is.EqualTo("item_002"), "Retrieved item should have correct ID");
            Assert.That(retrieved.Name, Is.EqualTo("Another Item"), "Retrieved item should have correct name");
        }

        [Test]
        public void GetItem_UnregisteredItem_ReturnsNull()
        {
            // Arrange
            var registry = new ItemRegistry();

            // Act
            var retrieved = registry.GetItem("nonexistent");

            // Assert
            Assert.That(retrieved, Is.Null, "GetItem should return null for unregistered item");
        }

        [Test]
        public void GetItemsByType_FiltersCorrectly()
        {
            // Arrange
            var registry = new ItemRegistry();
            registry.RegisterItem(new Item("eq_001", "Helmet", "Test", ItemType.Equipment));
            registry.RegisterItem(new Item("eq_002", "Sword", "Test", ItemType.Equipment));
            registry.RegisterItem(new Item("eq_003", "Shield", "Test", ItemType.Equipment));
            registry.RegisterItem(new Item("cons_001", "Potion", "Test", ItemType.Consumable));
            registry.RegisterItem(new Item("cons_002", "Food", "Test", ItemType.Consumable));

            // Act
            var equipment = registry.GetItemsByType(ItemType.Equipment);
            var consumables = registry.GetItemsByType(ItemType.Consumable);

            // Assert
            Assert.That(equipment.Count, Is.EqualTo(3), "Should have 3 equipment items");
            Assert.That(consumables.Count, Is.EqualTo(2), "Should have 2 consumable items");
        }

        [Test]
        public void GetItemsByRarity_FiltersCorrectly()
        {
            // Arrange
            var registry = new ItemRegistry();
            registry.RegisterItem(new Item("item_common1", "Common 1", "Test", ItemType.Miscellaneous, rarity: ItemRarity.Common));
            registry.RegisterItem(new Item("item_common2", "Common 2", "Test", ItemType.Miscellaneous, rarity: ItemRarity.Common));
            registry.RegisterItem(new Item("item_epic", "Epic Item", "Test", ItemType.Equipment, rarity: ItemRarity.Epic));
            registry.RegisterItem(new Item("item_legendary", "Legendary", "Test", ItemType.Equipment, rarity: ItemRarity.Legendary));

            // Act
            var commonItems = registry.GetItemsByRarity(ItemRarity.Common);
            var epicItems = registry.GetItemsByRarity(ItemRarity.Epic);

            // Assert
            Assert.That(commonItems.Count, Is.EqualTo(2), "Should have 2 common items");
            Assert.That(epicItems.Count, Is.EqualTo(1), "Should have 1 epic item");
        }

        [Test]
        public void UnregisterItem_RemovesItemFromRegistry()
        {
            // Arrange
            var registry = new ItemRegistry();
            var item = new Item("temp_item", "Temporary", "Test", ItemType.Miscellaneous);
            registry.RegisterItem(item);
            Assert.That(registry.HasItem("temp_item"), Is.True, "Item should be registered initially");

            // Act
            var removed = registry.UnregisterItem("temp_item");

            // Assert
            Assert.That(removed, Is.True, "UnregisterItem should return true");
            Assert.That(registry.HasItem("temp_item"), Is.False, "Item should no longer be registered");
        }

        #endregion
    }
}
