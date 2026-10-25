using System;
using System.Linq;
using NUnit.Framework;
using RPGFramework.Housing.Runtime.Contracts;
using RPGFramework.Housing.Runtime.Data;
using RPGFramework.Housing.Runtime.Implementations;
using RPGFramework.Housing.Runtime.Events;

namespace RPGFramework.Housing.Tests
{
    [TestFixture]
    public class HousingSystemTests
    {
        private IHousingManager _housingManager;
        private Guid _playerId;

        [SetUp]
        public void SetUp()
        {
            _housingManager = new HousingManager();
            _playerId = Guid.NewGuid();
        }

        #region House Tests

        [Test]
        public void House_Creation_ShouldInitializeCorrectly()
        {
            // Arrange
            var houseId = Guid.NewGuid();
            var plotId = Guid.NewGuid();
            var data = new HouseData(houseId, HouseType.Cottage, "Test Cottage", 40, plotId, 100f, 0f, 200f);

            // Act
            var house = new House(data);

            // Assert
            Assert.That(house.HouseId, Is.EqualTo(houseId));
            Assert.That(house.HouseType, Is.EqualTo(HouseType.Cottage));
            Assert.That(house.Name, Is.EqualTo("Test Cottage"));
            Assert.That(house.MaxFurnitureCapacity, Is.EqualTo(40));
            Assert.That(house.UpgradeLevel, Is.EqualTo(0));
            Assert.That(house.IsAccessible, Is.True);
            Assert.That(house.PlacedFurniture.Count, Is.EqualTo(0));
        }

        [Test]
        public void House_TryPlaceFurniture_ShouldSucceedWhenSpaceAvailable()
        {
            // Arrange
            var house = CreateTestHouse();
            var furniture = CreateTestFurniture();

            // Act
            var result = house.TryPlaceFurniture(furniture);

            // Assert
            Assert.That(result, Is.True);
            Assert.That(house.PlacedFurniture.Count, Is.EqualTo(1));
            Assert.That(house.PlacedFurniture[0].FurnitureId, Is.EqualTo(furniture.FurnitureId));
        }

        [Test]
        public void House_TryPlaceFurniture_ShouldFailWhenCapacityReached()
        {
            // Arrange
            var houseId = Guid.NewGuid();
            var data = new HouseData(houseId, HouseType.Apartment, "Small Apartment", 1, Guid.NewGuid(), 0f, 0f, 0f);
            var house = new House(data);
            var furniture1 = CreateTestFurniture();
            var furniture2 = CreateTestFurniture();

            // Act
            house.TryPlaceFurniture(furniture1);
            var result = house.TryPlaceFurniture(furniture2);

            // Assert
            Assert.That(result, Is.False);
            Assert.That(house.PlacedFurniture.Count, Is.EqualTo(1));
        }

        [Test]
        public void House_TryRemoveFurniture_ShouldRemoveExistingFurniture()
        {
            // Arrange
            var house = CreateTestHouse();
            var furniture = CreateTestFurniture();
            house.TryPlaceFurniture(furniture);

            // Act
            var result = house.TryRemoveFurniture(furniture.FurnitureId);

            // Assert
            Assert.That(result, Is.True);
            Assert.That(house.PlacedFurniture.Count, Is.EqualTo(0));
        }

        [Test]
        public void House_TryUpgrade_ShouldIncreaseCapacityAndLevel()
        {
            // Arrange
            var house = CreateTestHouse();
            var initialCapacity = house.MaxFurnitureCapacity;
            var initialLevel = house.UpgradeLevel;

            // Act
            var result = house.TryUpgrade();

            // Assert
            Assert.That(result, Is.True);
            Assert.That(house.UpgradeLevel, Is.EqualTo(initialLevel + 1));
            Assert.That(house.MaxFurnitureCapacity, Is.GreaterThan(initialCapacity));
        }

        [Test]
        public void House_CanUpgrade_ShouldReturnFalseWhenMaxLevelReached()
        {
            // Arrange
            var house = CreateTestHouse();
            // Upgrade to max level
            while (house.CanUpgrade())
            {
                house.TryUpgrade();
            }

            // Act
            var canUpgrade = house.CanUpgrade();
            var upgradeResult = house.TryUpgrade();

            // Assert
            Assert.That(canUpgrade, Is.False);
            Assert.That(upgradeResult, Is.False);
        }

        #endregion

        #region Furniture Tests

        [Test]
        public void Furniture_Creation_ShouldInitializeCorrectly()
        {
            // Arrange
            var furnitureId = Guid.NewGuid();
            var data = new FurniturePlacementData(
                furnitureId,
                "chair_01",
                "Wooden Chair",
                FurnitureCategory.Furniture,
                PlacementMode.Floor,
                5f, 0f, 10f,
                90f
            );

            // Act
            var furniture = new Furniture(data);

            // Assert
            Assert.That(furniture.FurnitureId, Is.EqualTo(furnitureId));
            Assert.That(furniture.TemplateId, Is.EqualTo("chair_01"));
            Assert.That(furniture.Name, Is.EqualTo("Wooden Chair"));
            Assert.That(furniture.Category, Is.EqualTo(FurnitureCategory.Furniture));
            Assert.That(furniture.PlacementMode, Is.EqualTo(PlacementMode.Floor));
            Assert.That(furniture.Position, Is.EqualTo((5f, 0f, 10f)));
            Assert.That(furniture.Rotation, Is.EqualTo(90f));
        }

        [Test]
        public void Furniture_SetPosition_ShouldUpdatePosition()
        {
            // Arrange
            var furniture = CreateTestFurniture();

            // Act
            furniture.SetPosition(20f, 5f, 30f);

            // Assert
            Assert.That(furniture.Position, Is.EqualTo((20f, 5f, 30f)));
        }

        [Test]
        public void Furniture_SetRotation_ShouldUpdateRotation()
        {
            // Arrange
            var furniture = CreateTestFurniture();

            // Act
            furniture.SetRotation(180f);

            // Assert
            Assert.That(furniture.Rotation, Is.EqualTo(180f));
        }

        [Test]
        public void Furniture_SetRotation_ShouldNormalizeAngle()
        {
            // Arrange
            var furniture = CreateTestFurniture();

            // Act
            furniture.SetRotation(450f); // 450 % 360 = 90

            // Assert
            Assert.That(furniture.Rotation, Is.EqualTo(90f));
        }

        #endregion

        #region Storage Tests

        [Test]
        public void HouseStorage_Creation_ShouldInitializeCorrectly()
        {
            // Arrange
            var houseId = Guid.NewGuid();
            var furnitureId = Guid.NewGuid();

            // Act
            var storage = new HouseStorage(houseId, furnitureId, 50);

            // Assert
            Assert.That(storage.HouseId, Is.EqualTo(houseId));
            Assert.That(storage.FurnitureId, Is.EqualTo(furnitureId));
            Assert.That(storage.Capacity, Is.EqualTo(50));
            Assert.That(storage.CurrentItemCount, Is.EqualTo(0));
            Assert.That(storage.IsFull, Is.False);
        }

        [Test]
        public void HouseStorage_TryAddItem_ShouldAddItemWhenSpaceAvailable()
        {
            // Arrange
            var storage = new HouseStorage(Guid.NewGuid(), Guid.NewGuid(), 10);
            var itemId = Guid.NewGuid();

            // Act
            var result = storage.TryAddItem(itemId);

            // Assert
            Assert.That(result, Is.True);
            Assert.That(storage.CurrentItemCount, Is.EqualTo(1));
            Assert.That(storage.ContainsItem(itemId), Is.True);
        }

        [Test]
        public void HouseStorage_TryAddItem_ShouldFailWhenFull()
        {
            // Arrange
            var storage = new HouseStorage(Guid.NewGuid(), Guid.NewGuid(), 1);
            storage.TryAddItem(Guid.NewGuid());

            // Act
            var result = storage.TryAddItem(Guid.NewGuid());

            // Assert
            Assert.That(result, Is.False);
            Assert.That(storage.IsFull, Is.True);
        }

        [Test]
        public void HouseStorage_TryRemoveItem_ShouldRemoveExistingItem()
        {
            // Arrange
            var storage = new HouseStorage(Guid.NewGuid(), Guid.NewGuid(), 10);
            var itemId = Guid.NewGuid();
            storage.TryAddItem(itemId);

            // Act
            var result = storage.TryRemoveItem(itemId);

            // Assert
            Assert.That(result, Is.True);
            Assert.That(storage.CurrentItemCount, Is.EqualTo(0));
            Assert.That(storage.ContainsItem(itemId), Is.False);
        }

        [Test]
        public void HouseStorage_ClearStorage_ShouldRemoveAllItems()
        {
            // Arrange
            var storage = new HouseStorage(Guid.NewGuid(), Guid.NewGuid(), 10);
            storage.TryAddItem(Guid.NewGuid());
            storage.TryAddItem(Guid.NewGuid());
            storage.TryAddItem(Guid.NewGuid());

            // Act
            storage.ClearStorage();

            // Assert
            Assert.That(storage.CurrentItemCount, Is.EqualTo(0));
        }

        #endregion

        #region Permission Tests

        [Test]
        public void HousePermissions_Creation_ShouldSetOwnerPermission()
        {
            // Arrange
            var houseId = Guid.NewGuid();
            var ownerId = Guid.NewGuid();

            // Act
            var permissions = new HousePermissions(houseId, ownerId);

            // Assert
            Assert.That(permissions.HouseId, Is.EqualTo(houseId));
            Assert.That(permissions.OwnerId, Is.EqualTo(ownerId));
            Assert.That(permissions.GetPermissionLevel(ownerId), Is.EqualTo(PermissionLevel.Owner));
        }

        [Test]
        public void HousePermissions_GrantPermission_ShouldAddGuestPermission()
        {
            // Arrange
            var permissions = new HousePermissions(Guid.NewGuid(), Guid.NewGuid());
            var guestId = Guid.NewGuid();

            // Act
            permissions.GrantPermission(guestId, PermissionLevel.Resident);

            // Assert
            Assert.That(permissions.GetPermissionLevel(guestId), Is.EqualTo(PermissionLevel.Resident));
        }

        [Test]
        public void HousePermissions_RevokePermission_ShouldRemoveGuestPermission()
        {
            // Arrange
            var permissions = new HousePermissions(Guid.NewGuid(), Guid.NewGuid());
            var guestId = Guid.NewGuid();
            permissions.GrantPermission(guestId, PermissionLevel.Visitor);

            // Act
            permissions.RevokePermission(guestId);

            // Assert
            Assert.That(permissions.GetPermissionLevel(guestId), Is.EqualTo(PermissionLevel.Banned));
        }

        [Test]
        public void HousePermissions_HasPermission_ShouldCheckPermissionLevel()
        {
            // Arrange
            var permissions = new HousePermissions(Guid.NewGuid(), Guid.NewGuid());
            var guestId = Guid.NewGuid();
            permissions.GrantPermission(guestId, PermissionLevel.Resident);

            // Act & Assert
            Assert.That(permissions.HasPermission(guestId, PermissionLevel.Visitor), Is.True);
            Assert.That(permissions.HasPermission(guestId, PermissionLevel.Resident), Is.True);
            Assert.That(permissions.HasPermission(guestId, PermissionLevel.CoOwner), Is.False);
        }

        [Test]
        public void HousePermissions_CanModifyFurniture_ShouldRequireCoOwnerLevel()
        {
            // Arrange
            var permissions = new HousePermissions(Guid.NewGuid(), Guid.NewGuid());
            var guestId = Guid.NewGuid();

            // Act & Assert
            permissions.GrantPermission(guestId, PermissionLevel.Visitor);
            Assert.That(permissions.CanModifyFurniture(guestId), Is.False);

            permissions.GrantPermission(guestId, PermissionLevel.CoOwner);
            Assert.That(permissions.CanModifyFurniture(guestId), Is.True);
        }

        [Test]
        public void HousePermissions_CanAccessStorage_ShouldRequireResidentLevel()
        {
            // Arrange
            var permissions = new HousePermissions(Guid.NewGuid(), Guid.NewGuid());
            var guestId = Guid.NewGuid();

            // Act & Assert
            permissions.GrantPermission(guestId, PermissionLevel.Visitor);
            Assert.That(permissions.CanAccessStorage(guestId), Is.False);

            permissions.GrantPermission(guestId, PermissionLevel.Resident);
            Assert.That(permissions.CanAccessStorage(guestId), Is.True);
        }

        #endregion

        #region HousingManager Tests

        [Test]
        public void HousingManager_RegisterHouse_ShouldAddHouseToSystem()
        {
            // Arrange
            var house = CreateTestHouse();

            // Act
            _housingManager.RegisterHouse(house, _playerId);

            // Assert
            var retrievedHouse = _housingManager.GetHouse(house.HouseId);
            Assert.That(retrievedHouse, Is.Not.Null);
            Assert.That(retrievedHouse.HouseId, Is.EqualTo(house.HouseId));
        }

        [Test]
        public void HousingManager_UnregisterHouse_ShouldRemoveHouse()
        {
            // Arrange
            var house = CreateTestHouse();
            _housingManager.RegisterHouse(house, _playerId);

            // Act
            _housingManager.UnregisterHouse(house.HouseId);

            // Assert
            var retrievedHouse = _housingManager.GetHouse(house.HouseId);
            Assert.That(retrievedHouse, Is.Null);
        }

        [Test]
        public void HousingManager_GetHousesOwnedBy_ShouldReturnOwnedHouses()
        {
            // Arrange
            var house1 = CreateTestHouse();
            var house2 = CreateTestHouse();
            _housingManager.RegisterHouse(house1, _playerId);
            _housingManager.RegisterHouse(house2, _playerId);

            // Act
            var ownedHouses = _housingManager.GetHousesOwnedBy(_playerId);

            // Assert
            Assert.That(ownedHouses.Count, Is.EqualTo(2));
            Assert.That(ownedHouses.Any(h => h.HouseId == house1.HouseId), Is.True);
            Assert.That(ownedHouses.Any(h => h.HouseId == house2.HouseId), Is.True);
        }

        [Test]
        public void HousingManager_TryTransferOwnership_ShouldChangeOwner()
        {
            // Arrange
            var house = CreateTestHouse();
            var newOwnerId = Guid.NewGuid();
            _housingManager.RegisterHouse(house, _playerId);

            // Act
            var result = _housingManager.TryTransferOwnership(house.HouseId, newOwnerId);

            // Assert
            Assert.That(result, Is.True);
            var ownedHouses = _housingManager.GetHousesOwnedBy(newOwnerId);
            Assert.That(ownedHouses.Count, Is.EqualTo(1));
        }

        [Test]
        public void HousingManager_PurchaseHouse_ShouldCreateAndRegisterHouse()
        {
            // Arrange
            var plot = CreateTestPlot();

            // Act
            var house = _housingManager.PurchaseHouse(plot, _playerId, HouseType.Cottage);

            // Assert
            Assert.That(house, Is.Not.Null);
            Assert.That(house.HouseType, Is.EqualTo(HouseType.Cottage));
            Assert.That(plot.IsOwned, Is.True);
            Assert.That(plot.OwnerId, Is.EqualTo(_playerId));
            Assert.That(plot.HouseId, Is.EqualTo(house.HouseId));
        }

        [Test]
        public void HousingManager_GetPermissions_ShouldReturnHousePermissions()
        {
            // Arrange
            var house = CreateTestHouse();
            _housingManager.RegisterHouse(house, _playerId);

            // Act
            var permissions = _housingManager.GetPermissions(house.HouseId);

            // Assert
            Assert.That(permissions, Is.Not.Null);
            Assert.That(permissions.OwnerId, Is.EqualTo(_playerId));
        }

        #endregion

        #region BuildingPlot Tests

        [Test]
        public void BuildingPlot_Creation_ShouldInitializeCorrectly()
        {
            // Arrange & Act
            var plot = CreateTestPlot();

            // Assert
            Assert.That(plot.IsOwned, Is.False);
            Assert.That(plot.OwnerId, Is.Null);
            Assert.That(plot.HouseId, Is.Null);
            Assert.That(plot.LocationName, Is.EqualTo("Test Location"));
        }

        [Test]
        public void BuildingPlot_CanBuildHouseType_ShouldCheckPlotSize()
        {
            // Arrange
            var smallPlot = new BuildingPlot(Guid.NewGuid(), "Small Plot", 0f, 0f, 0f, 15f, 1000);
            var largePlot = new BuildingPlot(Guid.NewGuid(), "Large Plot", 0f, 0f, 0f, 150f, 10000);

            // Act & Assert
            Assert.That(smallPlot.CanBuildHouseType(HouseType.Apartment), Is.True);
            Assert.That(smallPlot.CanBuildHouseType(HouseType.Castle), Is.False);
            Assert.That(largePlot.CanBuildHouseType(HouseType.Castle), Is.True);
            Assert.That(largePlot.CanBuildHouseType(HouseType.GuildHall), Is.True);
        }

        [Test]
        public void BuildingPlot_SetOwner_ShouldMarkAsOwned()
        {
            // Arrange
            var plot = CreateTestPlot();

            // Act
            plot.SetOwner(_playerId);

            // Assert
            Assert.That(plot.IsOwned, Is.True);
            Assert.That(plot.OwnerId, Is.EqualTo(_playerId));
        }

        [Test]
        public void BuildingPlot_Reset_ShouldClearOwnership()
        {
            // Arrange
            var plot = CreateTestPlot();
            plot.SetOwner(_playerId);
            plot.SetHouse(Guid.NewGuid());

            // Act
            plot.Reset();

            // Assert
            Assert.That(plot.IsOwned, Is.False);
            Assert.That(plot.OwnerId, Is.Null);
            Assert.That(plot.HouseId, Is.Null);
        }

        #endregion

        #region Helper Methods

        private House CreateTestHouse()
        {
            var houseId = Guid.NewGuid();
            var plotId = Guid.NewGuid();
            var data = new HouseData(houseId, HouseType.Cottage, "Test House", 40, plotId, 0f, 0f, 0f);
            return new House(data);
        }

        private Furniture CreateTestFurniture()
        {
            var furnitureId = Guid.NewGuid();
            var data = new FurniturePlacementData(
                furnitureId,
                "test_item",
                "Test Furniture",
                FurnitureCategory.Furniture,
                PlacementMode.Floor,
                0f, 0f, 0f
            );
            return new Furniture(data);
        }

        private BuildingPlot CreateTestPlot()
        {
            return new BuildingPlot(
                Guid.NewGuid(),
                "Test Location",
                100f, 0f, 200f,
                50f,
                5000
            );
        }

        #endregion
    }
}
