using NUnit.Framework;
using RPGFramework.Combat;
using RPGFramework.Serialization;

namespace RPGFramework.Combat.Tests
{
    /// <summary>
    /// Tests for ResourceComponent&lt;TResourceType&gt; generic resource pool component.
    /// </summary>
    [TestFixture]
    public class ResourceComponentTests
    {
        [Test]
        public void ResourceComponent_InitialValues_AreZero()
        {
            var resource = new ResourceComponent<ResourceType>
            {
                ResourceType = ResourceType.Health,
                Current = 0f,
                Max = 0f,
                RegenRate = 0f,
                LastRegenTime = 0f
            };

            Assert.AreEqual(ResourceType.Health, resource.ResourceType);
            Assert.AreEqual(0f, resource.Current);
            Assert.AreEqual(0f, resource.Max);
            Assert.AreEqual(0f, resource.RegenRate);
            Assert.AreEqual(0f, resource.LastRegenTime);
        }

        [Test]
        public void ResourceComponent_SetValues_PreservesData()
        {
            var resource = new ResourceComponent<ResourceType>
            {
                ResourceType = ResourceType.Mana,
                Current = 50f,
                Max = 100f,
                RegenRate = 5f,
                LastRegenTime = 10f
            };

            Assert.AreEqual(ResourceType.Mana, resource.ResourceType);
            Assert.AreEqual(50f, resource.Current);
            Assert.AreEqual(100f, resource.Max);
            Assert.AreEqual(5f, resource.RegenRate);
            Assert.AreEqual(10f, resource.LastRegenTime);
        }

        [Test]
        public void ResourceComponent_Serialization_RoundTrip()
        {
            var original = new ResourceComponent<ResourceType>
            {
                ResourceType = ResourceType.Stamina,
                Current = 75f,
                Max = 150f,
                RegenRate = 10f,
                LastRegenTime = 20f
            };

            byte[] data = original.Serialize();

            Assert.IsNotNull(data);
            Assert.Greater(data.Length, 0);

            var deserialized = new ResourceComponent<ResourceType>();
            deserialized.Deserialize(data);

            Assert.AreEqual(original.ResourceType, deserialized.ResourceType);
            Assert.AreEqual(original.Current, deserialized.Current);
            Assert.AreEqual(original.Max, deserialized.Max);
            Assert.AreEqual(original.RegenRate, deserialized.RegenRate);
            Assert.AreEqual(original.LastRegenTime, deserialized.LastRegenTime);
        }

        [Test]
        public void ResourceComponent_MultipleResourceTypes_DoNotConflict()
        {
            var health = new ResourceComponent<ResourceType>
            {
                ResourceType = ResourceType.Health,
                Current = 100f,
                Max = 100f
            };

            var mana = new ResourceComponent<ResourceType>
            {
                ResourceType = ResourceType.Mana,
                Current = 50f,
                Max = 100f
            };

            Assert.AreEqual(ResourceType.Health, health.ResourceType);
            Assert.AreEqual(ResourceType.Mana, mana.ResourceType);
            Assert.AreNotEqual(health.ResourceType, mana.ResourceType);
            Assert.AreNotEqual(health.Current, mana.Current);
        }

        [Test]
        public void ResourceComponent_CurrentExceedsMax_IsAllowed()
        {
            // Framework does not enforce max cap - that's game layer policy
            // Some games allow temporary overheal or buff-enhanced maximum
            var resource = new ResourceComponent<ResourceType>
            {
                ResourceType = ResourceType.Health,
                Current = 150f,
                Max = 100f
            };

            Assert.Greater(resource.Current, resource.Max);
            Assert.AreEqual(150f, resource.Current);
            Assert.AreEqual(100f, resource.Max);
        }

        [Test]
        public void ResourceComponent_NegativeCurrent_IsAllowed()
        {
            // Framework allows negative values (e.g., overheal debt, resource overconsumption)
            // Game layer decides enforcement policy
            var resource = new ResourceComponent<ResourceType>
            {
                ResourceType = ResourceType.Health,
                Current = -10f,
                Max = 100f
            };

            Assert.Less(resource.Current, 0f);
            Assert.AreEqual(-10f, resource.Current);
        }

        [Test]
        public void ResourceComponent_DeserializeNullData_ThrowsException()
        {
            var resource = new ResourceComponent<ResourceType>();

            Assert.Throws<System.ArgumentException>(() => resource.Deserialize(null));
            Assert.Throws<System.ArgumentException>(() => resource.Deserialize(new byte[0]));
        }

        [Test]
        public void ResourceComponent_AllResourceTypes_SerializeCorrectly()
        {

            foreach (ResourceType resourceType in System.Enum.GetValues(typeof(ResourceType)))
            {
                var original = new ResourceComponent<ResourceType>
                {
                    ResourceType = resourceType,
                    Current = 50f,
                    Max = 100f,
                    RegenRate = 5f,
                    LastRegenTime = 10f
                };

                byte[] data = original.Serialize();
                var deserialized = new ResourceComponent<ResourceType>();
                deserialized.Deserialize(data);

                Assert.AreEqual(original.ResourceType, deserialized.ResourceType, $"ResourceType {resourceType} failed serialization");
                Assert.AreEqual(original.Current, deserialized.Current);
                Assert.AreEqual(original.Max, deserialized.Max);
            }
        }
    }
}
