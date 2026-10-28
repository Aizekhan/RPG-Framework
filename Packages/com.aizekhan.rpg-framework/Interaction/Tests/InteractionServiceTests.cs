using System.Collections.Generic;
using NUnit.Framework;
using RPGFramework.Events;

namespace RPGFramework.Interaction.Tests
{
    /// <summary>
    /// Places with a radius, the nearest open one per player, rules per kind, places used once, and one
    /// InteractedEvent per accepted use. Distance is the game's: here players and places stand on a line.
    /// </summary>
    public sealed class InteractionServiceTests
    {
        private EventBus _bus;
        private InteractionService _places;
        private List<InteractedEvent> _used;
        private Dictionary<int, float> _standsAt;

        [SetUp]
        public void SetUp()
        {
            _bus = new EventBus();
            _places = new InteractionService(_bus);
            _used = new List<InteractedEvent>();
            _bus.Subscribe<InteractedEvent>(_used.Add);
            _standsAt = new Dictionary<int, float> { [1] = 0f, [2] = 10f };
            _places.Distance = (player, point) =>
                _standsAt.TryGetValue(player, out float x) ? System.Math.Abs(point.X - x) : float.NaN;
        }

        [TearDown]
        public void TearDown() => _bus.Dispose();

        [Test]
        public void EachPlayer_SeesTheNearestOpenPlaceInReach()
        {
            _places.Add(new InteractionPoint("door", "door", 2f).At(1.5f, 0, 0));
            _places.Add(new InteractionPoint("chest", "chest", 2f, "Open the chest").At(0.5f, 0, 0));
            _places.Add(new InteractionPoint("far", "chest", 2f).At(5f, 0, 0));
            _places.Add(new InteractionPoint("lever", "lever", 1f).At(9.5f, 0, 0));

            Assert.That(_places.NearestFor(1).Id, Is.EqualTo("chest"));
            Assert.That(_places.NearestFor(1).Prompt, Is.EqualTo("Open the chest"));
            Assert.That(_places.NearestFor(2).Id, Is.EqualTo("lever"));
            Assert.That(_places.NearestFor(3), Is.Null, "a player who is nowhere reaches nothing");

            _places.Get("chest").Enabled = false;
            Assert.That(_places.NearestFor(1).Id, Is.EqualTo("door"), "closed places are skipped");
        }

        [Test]
        public void UsingAPlace_ChecksItIsThereOpenAndNear_ThenTellsEveryone()
        {
            _places.Add(new InteractionPoint("shrine", "shrine", 1f).At(3f, 0, 0));

            Assert.That(_places.Interact(1, "nothing"), Is.EqualTo(InteractionOutcome.NotFound));
            Assert.That(_places.Interact(1, "shrine"), Is.EqualTo(InteractionOutcome.TooFar));
            Assert.That(_places.Interact(1), Is.EqualTo(InteractionOutcome.NotFound), "nothing near to use");
            Assert.That(_used, Is.Empty);

            _standsAt[1] = 2.5f;
            Assert.That(_places.Interact(1), Is.EqualTo(InteractionOutcome.Done));
            Assert.That(_places.Interact(1, "shrine"), Is.EqualTo(InteractionOutcome.Done), "it stays open");
            Assert.That(_used.Count, Is.EqualTo(2));
            Assert.That(_used[0].PlayerId, Is.EqualTo(1));
            Assert.That(_used[0].PointId, Is.EqualTo("shrine"));
            Assert.That(_used[0].Kind, Is.EqualTo("shrine"));
        }

        [Test]
        public void APlaceUsedOnce_ClosesForEveryone()
        {
            _places.Add(new InteractionPoint("chest", "chest", 20f, once: true));

            Assert.That(_places.Interact(1, "chest"), Is.EqualTo(InteractionOutcome.Done));
            Assert.That(_places.Interact(2, "chest"), Is.EqualTo(InteractionOutcome.Closed));
            Assert.That(_places.Get("chest").Enabled, Is.False);
            Assert.That(_places.NearestFor(2), Is.Null);
            Assert.That(_used.Count, Is.EqualTo(1));
        }

        [Test]
        public void AKindsRule_DecidesWhoMayUseIt()
        {
            var hasKey = new HashSet<int> { 2 };
            _places.SetRule("door", (player, point) => hasKey.Contains(player));
            _places.Add(new InteractionPoint("gate", "door", 20f));

            Assert.That(_places.NearestFor(1).Id, Is.EqualTo("gate"), "the prompt still shows (the door is there)");
            Assert.That(_places.Interact(1, "gate"), Is.EqualTo(InteractionOutcome.Refused));
            Assert.That(_places.Interact(2, "gate"), Is.EqualTo(InteractionOutcome.Done));

            _places.SetRule("door", null);
            Assert.That(_places.Interact(1, "gate"), Is.EqualTo(InteractionOutcome.Done));
        }

        [Test]
        public void WithoutADistance_EveryPlaceIsInReach_AndPlacesAreReplacedById()
        {
            var places = new InteractionService();
            places.Add(new InteractionPoint("a", "use", 0f).At(100f, 0, 0));
            places.Add(new InteractionPoint("b", "use", 0f));
            places.Add(new InteractionPoint("a", "talk", 1f));

            Assert.That(places.Points.Count, Is.EqualTo(2));
            Assert.That(places.Get("a").Kind, Is.EqualTo("talk"));
            Assert.That(places.NearestFor(7).Id, Is.EqualTo("a"), "equally near: the one added first");
            Assert.That(places.Interact(7, "b"), Is.EqualTo(InteractionOutcome.Done));
            Assert.That(places.Remove("a"), Is.True);
            Assert.That(places.Remove("a"), Is.False);
            Assert.That(() => new InteractionPoint("", "use", 1f), Throws.ArgumentException);
            Assert.That(() => new InteractionPoint("x", "use", -1f), Throws.InstanceOf<System.ArgumentOutOfRangeException>());
        }
    }
}
