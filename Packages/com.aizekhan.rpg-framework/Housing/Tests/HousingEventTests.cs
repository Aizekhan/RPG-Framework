using System;
using System.Collections.Generic;
using NUnit.Framework;
using RPGFramework.Events;
using RPGFramework.Housing.Runtime.Contracts;
using RPGFramework.Housing.Runtime.Data;
using RPGFramework.Housing.Runtime.Events;
using RPGFramework.Housing.Runtime.Implementations;

namespace RPGFramework.Housing.Tests
{
    /// <summary>
    /// Proves that a HousingManager built with an event bus publishes each housing event exactly once, with the right
    /// values: the purchase itself, and the furniture, upgrade and guest events of the house and permissions it creates.
    /// </summary>
    [TestFixture]
    public class HousingEventTests
    {
        private EventBus _bus;
        private HousingManager _manager;
        private Guid _buyerId;

        [SetUp]
        public void SetUp()
        {
            _bus = new EventBus();
            _manager = new HousingManager(_bus);
            _buyerId = Guid.NewGuid();
        }

        [TearDown]
        public void TearDown()
        {
            _bus.Dispose();
        }

        [Test]
        public void Purchasing_PublishesHousePurchased()
        {
            var received = new List<HousePurchasedEvent>();
            _bus.Subscribe<HousePurchasedEvent>(received.Add);
            var plot = CreatePlot();

            var house = _manager.PurchaseHouse(plot, _buyerId, HouseType.Cottage);

            Assert.That(house, Is.Not.Null);
            Assert.That(received.Count, Is.EqualTo(1));
            Assert.That(received[0].HouseId, Is.EqualTo(house.HouseId));
            Assert.That(received[0].BuyerId, Is.EqualTo(_buyerId));
            Assert.That(received[0].PlotId, Is.EqualTo(plot.PlotId));
            Assert.That(received[0].HouseType, Is.EqualTo(HouseType.Cottage));
            Assert.That(received[0].Price, Is.EqualTo(5000));
        }

        [Test]
        public void PlacingFurniture_PublishesFurniturePlaced()
        {
            var received = new List<FurniturePlacedEvent>();
            _bus.Subscribe<FurniturePlacedEvent>(received.Add);
            var house = _manager.PurchaseHouse(CreatePlot(), _buyerId, HouseType.Cottage);
            var furniture = CreateFurniture();

            Assert.That(house.TryPlaceFurniture(furniture), Is.True);
            Assert.That(house.TryPlaceFurniture(furniture), Is.False); // duplicate: no event

            Assert.That(received.Count, Is.EqualTo(1));
            Assert.That(received[0].HouseId, Is.EqualTo(house.HouseId));
            Assert.That(received[0].FurnitureId, Is.EqualTo(furniture.FurnitureId));
            Assert.That(received[0].TemplateId, Is.EqualTo("chair_oak"));
            Assert.That(received[0].Category, Is.EqualTo(FurnitureCategory.Furniture));
            Assert.That(received[0].PlacedBy, Is.EqualTo(Guid.Empty));
            Assert.That(received[0].Position, Is.EqualTo((1f, 2f, 3f)));
        }

        [Test]
        public void RemovingFurniture_PublishesFurnitureRemoved()
        {
            var received = new List<FurnitureRemovedEvent>();
            _bus.Subscribe<FurnitureRemovedEvent>(received.Add);
            var house = _manager.PurchaseHouse(CreatePlot(), _buyerId, HouseType.Cottage);
            var furniture = CreateFurniture();
            house.TryPlaceFurniture(furniture);

            Assert.That(house.TryRemoveFurniture(furniture.FurnitureId), Is.True);
            Assert.That(house.TryRemoveFurniture(furniture.FurnitureId), Is.False); // already gone: no event

            Assert.That(received.Count, Is.EqualTo(1));
            Assert.That(received[0].HouseId, Is.EqualTo(house.HouseId));
            Assert.That(received[0].FurnitureId, Is.EqualTo(furniture.FurnitureId));
            Assert.That(received[0].RemovedBy, Is.EqualTo(Guid.Empty));
        }

        [Test]
        public void Upgrading_PublishesHouseUpgraded()
        {
            var received = new List<HouseUpgradedEvent>();
            _bus.Subscribe<HouseUpgradedEvent>(received.Add);
            var house = _manager.PurchaseHouse(CreatePlot(), _buyerId, HouseType.Cottage);

            Assert.That(house.TryUpgrade(), Is.True);

            Assert.That(received.Count, Is.EqualTo(1));
            Assert.That(received[0].HouseId, Is.EqualTo(house.HouseId));
            Assert.That(received[0].PreviousLevel, Is.EqualTo(0));
            Assert.That(received[0].NewLevel, Is.EqualTo(1));
            Assert.That(received[0].PreviousCapacity, Is.EqualTo(40));
            Assert.That(received[0].NewCapacity, Is.EqualTo(55));
            Assert.That(received[0].UpgradedBy, Is.EqualTo(Guid.Empty));
        }

        [Test]
        public void GrantingAccess_PublishesGuestInvited()
        {
            var received = new List<GuestInvitedEvent>();
            _bus.Subscribe<GuestInvitedEvent>(received.Add);
            var house = _manager.PurchaseHouse(CreatePlot(), _buyerId, HouseType.Cottage);
            var permissions = _manager.GetPermissions(house.HouseId);
            var guestId = Guid.NewGuid();

            permissions.GrantPermission(guestId, PermissionLevel.Visitor);
            permissions.GrantPermission(guestId, PermissionLevel.Visitor); // same level again: no event
            permissions.GrantPermission(Guid.NewGuid(), PermissionLevel.Banned); // a ban is not an invitation

            Assert.That(received.Count, Is.EqualTo(1));
            Assert.That(received[0].HouseId, Is.EqualTo(house.HouseId));
            Assert.That(received[0].GuestId, Is.EqualTo(guestId));
            Assert.That(received[0].PermissionLevel, Is.EqualTo(PermissionLevel.Visitor));
            Assert.That(received[0].InvitedBy, Is.EqualTo(Guid.Empty));
        }

        private static BuildingPlot CreatePlot()
        {
            return new BuildingPlot(Guid.NewGuid(), "Test Location", 100f, 0f, 200f, 50f, 5000);
        }

        private static IFurniture CreateFurniture()
        {
            var data = new FurniturePlacementData(
                Guid.NewGuid(),
                "chair_oak",
                "Oak Chair",
                FurnitureCategory.Furniture,
                PlacementMode.Floor,
                1f, 2f, 3f
            );
            return new Furniture(data);
        }
    }
}
