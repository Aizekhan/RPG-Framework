using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RPGFramework.Bootstrap;
using RPGFramework.Events;
using RPGFramework.Persistence;

namespace RPGFramework.WorldMap.Tests
{
    /// <summary>
    /// A world of places joined by routes (FG-35): the party stands in a place, runs a route out of it, arrives at its
    /// end and fast-travels back to any place it has reached. Saved per player, moved as one party in co-op.
    /// </summary>
    public sealed class WorldTravelTests
    {
        private const int Runner = 1;
        private const int Friend = 2;

        private static WorldGraph Map() => new WorldGraph()
            .AddPlace("home", "Home", 0f, 0f)
            .AddPlace("village", "Village", 10f, 0f)
            .AddPlace("castle", "Castle", 20f, 5f)
            .AddRoute("forest-road", "home", "village")
            .AddRoute("hill-road", "village", "castle")
            .AddRoute("way-back", "village", "home")
            .AddRoute("trial", "home");

        [Test]
        public void Graph_KnowsItsPlacesAndRoutes_AndRefusesRoutesToNowhere()
        {
            var map = Map();
            Assert.That(map.Start, Is.EqualTo("home"), "the first place unless set");
            Assert.That(map.RoutesFrom("village").Select(r => r.Id), Is.EqualTo(new[] { "hill-road", "way-back" }));
            Assert.That(map.Route("trial").IsLoop, Is.True);
            Assert.That(map.Route("trial").To, Is.EqualTo("home"));
            Assert.That(map.Place("castle").Name, Is.EqualTo("Castle"));
            Assert.That(map.Place("nowhere"), Is.Null);
            Assert.That(map.RoutesFrom("castle"), Is.Empty);
            Assert.That(() => map.AddRoute("bad", "home", "nowhere"), Throws.ArgumentException.With.Message.Contains("nowhere"));
            Assert.That(() => map.AddPlace("home"), Throws.ArgumentException);
            Assert.That(() => map.Start = "nowhere", Throws.ArgumentException);
            map.Start = "village";
            Assert.That(map.Start, Is.EqualTo("village"));
        }

        [Test]
        public void ARunner_RunsRoutesOut_AndFastTravelsOnlyToReachedPlaces()
        {
            var travel = new WorldTravel(Map());
            Assert.That(travel.LocationOf(Runner).Id, Is.EqualTo("home"));
            Assert.That(travel.DiscoveredBy(Runner), Is.EqualTo(new[] { "home" }));
            Assert.That(travel.Players, Is.Empty, "reading does not add a player");
            Assert.That(travel.RoutesHere(Runner).Select(r => r.Id), Is.EqualTo(new[] { "forest-road", "trial" }));

            Assert.That(travel.CanArrive(Runner, "hill-road"), Is.EqualTo(TravelRefusal.NotFromHere));
            Assert.That(travel.CanArrive(Runner, "no-road"), Is.EqualTo(TravelRefusal.UnknownRoute));
            Assert.That(travel.CanTravel(Runner, "village"), Is.EqualTo(TravelRefusal.NotDiscovered));
            Assert.That(travel.CanTravel(Runner, "home"), Is.EqualTo(TravelRefusal.AlreadyThere));
            Assert.That(travel.CanTravel(Runner, "atlantis"), Is.EqualTo(TravelRefusal.UnknownPlace));

            Assert.That(travel.Arrive(Runner, "forest-road"), Is.True);
            Assert.That(travel.Arrive(Runner, "hill-road"), Is.True);
            Assert.That(travel.LocationOf(Runner).Id, Is.EqualTo("castle"));
            Assert.That(travel.DiscoveredBy(Runner), Is.EqualTo(new[] { "home", "village", "castle" }));

            Assert.That(travel.Travel(Runner, "village"), Is.True, "fast travel to a reached place");
            Assert.That(travel.Travel(Runner, "home"), Is.True, "the start is always reached");
            Assert.That(travel.Arrive(Runner, "trial"), Is.True, "a loop brings the runner back");
            Assert.That(travel.LocationOf(Runner).Id, Is.EqualTo("home"));
            Assert.That(travel.LocationOf(Friend).Id, Is.EqualTo("home"), "each player moves alone by default");
        }

        [Test]
        public void TheGamesRule_CanBlockFastTravel()
        {
            var travel = new WorldTravel(Map());
            travel.Arrive(Runner, "forest-road");
            travel.Arrive(Runner, "hill-road");
            var asked = new List<string>();
            travel.TravelRule = (player, from, to) =>
            {
                asked.Add($"{player}:{from.Id}->{to.Id}");
                return to.Id != "home";
            };
            Assert.That(travel.CanTravel(Runner, "home"), Is.EqualTo(TravelRefusal.Blocked));
            Assert.That(travel.Travel(Runner, "home"), Is.False);
            Assert.That(travel.Travel(Runner, "village"), Is.True);
            Assert.That(asked, Does.Contain("1:castle->home").And.Contain("1:castle->village"));
        }

        [Test]
        public void Moves_AreAnnounced_OnTheBusAndAsEvents()
        {
            var bus = new EventBus();
            var entered = new List<PlaceEnteredEvent>();
            var discovered = new List<PlaceDiscoveredEvent>();
            bus.Subscribe<PlaceEnteredEvent>(entered.Add);
            bus.Subscribe<PlaceDiscoveredEvent>(discovered.Add);
            var travel = new WorldTravel(Map(), bus);
            int heard = 0;
            travel.PlaceEntered += _ => heard++;

            travel.Arrive(Runner, "forest-road");
            travel.Travel(Runner, "home");
            travel.Arrive(Runner, "trial");

            Assert.That(entered.Select(e => (e.PlaceId, e.FromPlaceId, e.Move, e.RouteId)), Is.EqualTo(new[]
            {
                ("village", "home", WorldMove.Arrived, "forest-road"),
                ("home", "village", WorldMove.Travelled, ""),
                ("home", "home", WorldMove.Arrived, "trial")
            }));
            Assert.That(discovered.Select(e => e.PlaceId), Is.EqualTo(new[] { "village" }), "the start is not news");
            Assert.That(heard, Is.EqualTo(3));
        }

        [Test]
        public void TravellingTogether_TheWholePartyMoves_AndDiscovers()
        {
            var travel = new WorldTravel(Map()) { TravelTogether = true };
            travel.PlaceAt(Friend, "home");
            Assert.That(travel.Arrive(Runner, "forest-road"), Is.True);
            Assert.That(travel.LocationOf(Friend).Id, Is.EqualTo("village"));
            Assert.That(travel.HasDiscovered(Friend, "village"), Is.True);
            Assert.That(travel.Players, Is.EquivalentTo(new[] { Runner, Friend }));
            Assert.That(travel.Forget(Friend), Is.True);
            Assert.That(travel.Players, Is.EquivalentTo(new[] { Runner }));
        }

        [Test]
        public void ARecord_CarriesAPlayerToAGuestOrASave_AndDropsPlacesNoLongerOnTheMap()
        {
            var host = new WorldTravel(Map());
            host.Arrive(Runner, "forest-road");
            host.Arrive(Runner, "hill-road");
            var record = host.Capture(Runner);
            Assert.That(record.Location, Is.EqualTo("castle"));

            var guest = new WorldTravel(Map());
            guest.Restore(Runner, record);
            Assert.That(guest.LocationOf(Runner).Id, Is.EqualTo("castle"));
            Assert.That(guest.DiscoveredBy(Runner), Is.EqualTo(new[] { "home", "village", "castle" }));

            guest.Restore(Runner, new WorldTravelRecord { Location = "removed-town", Discovered = { "removed-town", "village" } });
            Assert.That(guest.LocationOf(Runner).Id, Is.EqualTo("home"), "a lost place falls back to the start");
            Assert.That(guest.DiscoveredBy(Runner), Is.EqualTo(new[] { "home", "village" }));
        }

        [Test]
        public void Save_KeepsThePlayersPlace_UnderTheirIdOfTheNextSession()
        {
            var serializer = new JsonSaveSerializer();
            var before = new WorldTravel(Map());
            before.Arrive(7, "forest-road");
            ISaveParticipant saving = new WorldTravelSave(before, () => 7);
            byte[] data = saving.Capture(serializer);

            var after = new WorldTravel(Map());
            ISaveParticipant loading = new WorldTravelSave(after, () => 42);
            Assert.That(loading.SaveKey, Is.EqualTo(WorldTravelSave.Key));
            loading.Restore(loading.Read(data, loading.Version, serializer));
            Assert.That(after.LocationOf(42).Id, Is.EqualTo("village"));
            Assert.That(after.HasDiscovered(42, "village"), Is.True);
        }

        [Test]
        public void Module_RegistersTheMapAndTravel_AnnouncedOnTheHostsBus()
        {
            var host = new GameHost();
            host.Start(b => b.UseModule(new WorldMapModule(Map())));
            try
            {
                var travel = host.Container.Resolve<IWorldTravel>();
                Assert.That(travel.Map, Is.SameAs(host.Container.Resolve<WorldGraph>()));
                var entered = new List<PlaceEnteredEvent>();
                host.Container.Resolve<IEventBus>().Subscribe<PlaceEnteredEvent>(entered.Add);
                travel.Arrive(Runner, "forest-road");
                Assert.That(entered.Single().PlaceId, Is.EqualTo("village"));
            }
            finally
            {
                host.Dispose();
            }
        }

        [Test]
        public void AMapWithoutPlaces_CannotBeTravelled()
        {
            Assert.That(() => new WorldTravel(new WorldGraph()), Throws.ArgumentException);
            Assert.That(() => new WorldTravel(Map()).PlaceAt(Runner, "nowhere"), Throws.ArgumentException);
        }
    }
}
