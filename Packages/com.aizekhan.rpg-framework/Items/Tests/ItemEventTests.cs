using NUnit.Framework;
using RPGFramework.Events;
using RPGFramework.Items;
using System.Collections.Generic;

namespace RPGFramework.Items.Tests
{
    /// <summary>
    /// Proves that creating an <see cref="ItemInstance"/> with an event publisher publishes
    /// <see cref="ItemCreatedEvent"/> once on a real <see cref="EventBus"/>, and that creating one without a publisher still works.
    /// </summary>
    [TestFixture]
    public class ItemEventTests
    {
        [Test]
        public void CreatingInstance_PublishesItemCreated()
        {
            using var bus = new EventBus();
            var created = new List<ItemCreatedEvent>();
            bus.Subscribe<ItemCreatedEvent>(created.Add);

            var instance = new ItemInstance("health_potion", quantity: 5, createdAt: 12.5f, events: bus);

            Assert.That(created, Has.Count.EqualTo(1));
            Assert.That(created[0].InstanceId, Is.EqualTo(instance.InstanceId));
            Assert.That(created[0].ItemDefinitionId, Is.EqualTo("health_potion"));
            Assert.That(created[0].Quantity, Is.EqualTo(5));
            Assert.That(created[0].Timestamp, Is.EqualTo(12.5f));
        }

        [Test]
        public void CreatingInstance_WithoutPublisher_PublishesNothing()
        {
            using var bus = new EventBus();
            var created = new List<ItemCreatedEvent>();
            bus.Subscribe<ItemCreatedEvent>(created.Add);

            var instance = new ItemInstance("health_potion");

            Assert.That(instance.ItemDefinitionId, Is.EqualTo("health_potion"));
            Assert.That(created, Is.Empty);
        }
    }
}
