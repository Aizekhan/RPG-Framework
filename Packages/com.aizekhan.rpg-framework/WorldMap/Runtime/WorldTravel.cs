using System;
using System.Collections.Generic;
using RPGFramework.Events;

namespace RPGFramework.WorldMap
{
    /// <summary>
    /// Why a move on the world map did not happen; <see cref="None"/>: it can.
    /// </summary>
    public enum TravelRefusal
    {
        None,

        /// <summary>No such place on the map.</summary>
        UnknownPlace,

        /// <summary>No such route on the map.</summary>
        UnknownRoute,

        /// <summary>The player already stands there.</summary>
        AlreadyThere,

        /// <summary>Fast travel goes only to places the player has discovered (reached by a route, or told).</summary>
        NotDiscovered,

        /// <summary>The route does not leave the place the player stands in.</summary>
        NotFromHere,

        /// <summary>The game's <see cref="IWorldTravel.TravelRule"/> said no (an unpaid fare, a locked gate).</summary>
        Blocked
    }

    /// <summary>
    /// How a player came to stand somewhere.
    /// </summary>
    public enum WorldMove
    {
        /// <summary>At the end of a route (<see cref="IWorldTravel.Arrive"/>).</summary>
        Arrived,

        /// <summary>By fast travel (<see cref="IWorldTravel.Travel"/>).</summary>
        Travelled,

        /// <summary>Put there by the game, without its rules (<see cref="IWorldTravel.PlaceAt"/>, a loaded save).</summary>
        Placed
    }

    /// <summary>
    /// A player stands in another place.
    /// </summary>
    public readonly struct PlaceEnteredEvent : IEvent
    {
        public PlaceEnteredEvent(int playerId, string placeId, string fromPlaceId, WorldMove move, string routeId)
        {
            PlayerId = playerId;
            PlaceId = placeId;
            FromPlaceId = fromPlaceId;
            Move = move;
            RouteId = routeId;
        }

        public int PlayerId { get; }
        public string PlaceId { get; }
        public string FromPlaceId { get; }
        public WorldMove Move { get; }

        /// <summary>
        /// The route run to get there (<see cref="WorldMove.Arrived"/>); empty otherwise.
        /// </summary>
        public string RouteId { get; }
    }

    /// <summary>
    /// A player discovered a place: fast travel can take them there from now on.
    /// </summary>
    public readonly struct PlaceDiscoveredEvent : IEvent
    {
        public PlaceDiscoveredEvent(int playerId, string placeId)
        {
            PlayerId = playerId;
            PlaceId = placeId;
        }

        public int PlayerId { get; }
        public string PlaceId { get; }
    }

    /// <summary>
    /// One player's place on the map and the places they discovered: what a save keeps, and what a co-op host sends
    /// a guest (the game's own message, <c>CoopGameMessages</c>) so the guest's map shows the party.
    /// </summary>
    public sealed class WorldTravelRecord
    {
        public string Location { get; set; } = "";
        public List<string> Discovered { get; set; } = new List<string>();
    }

    /// <summary>
    /// Where each player stands on the <see cref="WorldGraph"/>, the places they discovered, and the moves between
    /// them: running a route out of the place they stand in (<see cref="Arrive"/>) and fast travel to a discovered place
    /// (<see cref="Travel"/>). In co-op the host keeps it for every player; with <see cref="TravelTogether"/> the party
    /// moves as one.
    /// </summary>
    public interface IWorldTravel
    {
        WorldGraph Map { get; }

        /// <summary>
        /// Any player's move takes every known player along (each discovers the place). Off: each player moves alone.
        /// </summary>
        bool TravelTogether { get; set; }

        /// <summary>
        /// The game's say on fast travel (player, from, to); false refuses with <see cref="TravelRefusal.Blocked"/>.
        /// Null: any discovered place.
        /// </summary>
        Func<int, WorldPlace, WorldPlace, bool> TravelRule { get; set; }

        /// <summary>
        /// The players this service knows: they moved, discovered a place, or were placed or restored.
        /// </summary>
        IReadOnlyCollection<int> Players { get; }

        /// <summary>
        /// Where a player stands; the map's start for a player who has not moved yet.
        /// </summary>
        WorldPlace LocationOf(int playerId);

        /// <summary>
        /// The places a player discovered, the start included, in the order they were found.
        /// </summary>
        IReadOnlyList<string> DiscoveredBy(int playerId);

        bool HasDiscovered(int playerId, string placeId);

        /// <summary>
        /// The routes out of the place the player stands in.
        /// </summary>
        IReadOnlyList<WorldRoute> RoutesHere(int playerId);

        TravelRefusal CanArrive(int playerId, string routeId);

        /// <summary>
        /// The player came to the end of a route that leaves where they stand: they stand where it leads, now
        /// discovered. A loop brings them back.
        /// </summary>
        bool Arrive(int playerId, string routeId);

        TravelRefusal CanTravel(int playerId, string placeId);

        /// <summary>
        /// Fast travel to a discovered place, if <see cref="TravelRule"/> allows.
        /// </summary>
        bool Travel(int playerId, string placeId);

        /// <returns>False when it was discovered already or is not on the map.</returns>
        bool Discover(int playerId, string placeId);

        /// <summary>
        /// Puts a player in a place without the rules (a story scene, a guest following the host); it is discovered.
        /// Moves the whole party with <see cref="TravelTogether"/>.
        /// </summary>
        /// <exception cref="ArgumentException">Not a place on the map.</exception>
        void PlaceAt(int playerId, string placeId);

        WorldTravelRecord Capture(int playerId);

        /// <summary>
        /// Sets one player's place and discoveries (a loaded save, the host's word). Places no longer on the map are
        /// dropped; a lost location falls back to the start.
        /// </summary>
        void Restore(int playerId, WorldTravelRecord record);

        /// <summary>
        /// Drops a player (they left the game).
        /// </summary>
        bool Forget(int playerId);
    }

    /// <inheritdoc cref="IWorldTravel"/>
    public sealed class WorldTravel : IWorldTravel
    {
        private sealed class Traveller
        {
            public string Location;
            public readonly List<string> Discovered = new List<string>();
            public readonly HashSet<string> Known = new HashSet<string>();
        }

        private readonly Dictionary<int, Traveller> _travellers = new Dictionary<int, Traveller>();
        private readonly IEventPublisher _events;

        public WorldTravel(WorldGraph map, IEventPublisher events = null)
        {
            Map = map ?? throw new ArgumentNullException(nameof(map));
            if (map.Start == null)
                throw new ArgumentException("The map has no places", nameof(map));
            _events = events;
        }

        public WorldGraph Map { get; }
        public bool TravelTogether { get; set; }
        public Func<int, WorldPlace, WorldPlace, bool> TravelRule { get; set; }
        public IReadOnlyCollection<int> Players => _travellers.Keys;

        /// <summary>
        /// The same as <see cref="PlaceEnteredEvent"/> on the bus, for a game without one.
        /// </summary>
        public event Action<PlaceEnteredEvent> PlaceEntered;

        public event Action<PlaceDiscoveredEvent> PlaceDiscovered;

        public WorldPlace LocationOf(int playerId) => Map.Place(Peek(playerId).Location);

        public IReadOnlyList<string> DiscoveredBy(int playerId) => Peek(playerId).Discovered;

        public bool HasDiscovered(int playerId, string placeId) => placeId != null && Peek(playerId).Known.Contains(placeId);

        public IReadOnlyList<WorldRoute> RoutesHere(int playerId) => Map.RoutesFrom(Peek(playerId).Location);

        public TravelRefusal CanArrive(int playerId, string routeId)
        {
            var route = Map.Route(routeId);
            if (route == null)
                return TravelRefusal.UnknownRoute;
            return route.From == Peek(playerId).Location ? TravelRefusal.None : TravelRefusal.NotFromHere;
        }

        public bool Arrive(int playerId, string routeId)
        {
            if (CanArrive(playerId, routeId) != TravelRefusal.None)
                return false;
            var route = Map.Route(routeId);
            Move(playerId, route.To, WorldMove.Arrived, route.Id);
            return true;
        }

        public TravelRefusal CanTravel(int playerId, string placeId)
        {
            var to = Map.Place(placeId);
            if (to == null)
                return TravelRefusal.UnknownPlace;
            var traveller = Peek(playerId);
            if (traveller.Location == to.Id)
                return TravelRefusal.AlreadyThere;
            if (!traveller.Known.Contains(to.Id))
                return TravelRefusal.NotDiscovered;
            if (TravelRule != null && !TravelRule(playerId, Map.Place(traveller.Location), to))
                return TravelRefusal.Blocked;
            return TravelRefusal.None;
        }

        public bool Travel(int playerId, string placeId)
        {
            if (CanTravel(playerId, placeId) != TravelRefusal.None)
                return false;
            Move(playerId, placeId, WorldMove.Travelled, "");
            return true;
        }

        public bool Discover(int playerId, string placeId)
        {
            if (Map.Place(placeId) == null)
                return false;
            var traveller = Get(playerId);
            if (!traveller.Known.Add(placeId))
                return false;
            traveller.Discovered.Add(placeId);
            var discovered = new PlaceDiscoveredEvent(playerId, placeId);
            _events?.Publish(discovered);
            PlaceDiscovered?.Invoke(discovered);
            return true;
        }

        public void PlaceAt(int playerId, string placeId)
        {
            if (Map.Place(placeId) == null)
                throw new ArgumentException($"'{placeId}' is not a place on this map", nameof(placeId));
            Move(playerId, placeId, WorldMove.Placed, "");
        }

        public WorldTravelRecord Capture(int playerId)
        {
            var traveller = Peek(playerId);
            return new WorldTravelRecord { Location = traveller.Location, Discovered = new List<string>(traveller.Discovered) };
        }

        public void Restore(int playerId, WorldTravelRecord record)
        {
            if (record == null)
                throw new ArgumentNullException(nameof(record));
            var traveller = new Traveller();
            _travellers[playerId] = traveller;
            Learn(traveller, Map.Start);
            if (record.Discovered != null)
            {
                foreach (string place in record.Discovered)
                {
                    if (Map.Place(place) != null)
                        Learn(traveller, place);
                }
            }
            string from = Map.Start;
            traveller.Location = Map.Place(record.Location) != null ? record.Location : Map.Start;
            Learn(traveller, traveller.Location);
            if (traveller.Location != from)
                Announce(new PlaceEnteredEvent(playerId, traveller.Location, from, WorldMove.Placed, ""));
        }

        public bool Forget(int playerId) => _travellers.Remove(playerId);

        private void Move(int playerId, string placeId, WorldMove how, string routeId)
        {
            Get(playerId);
            var party = TravelTogether ? new List<int>(_travellers.Keys) : new List<int> { playerId };
            foreach (int member in party)
            {
                var traveller = _travellers[member];
                string from = traveller.Location;
                traveller.Location = placeId;
                Discover(member, placeId);
                if (from != placeId || how == WorldMove.Arrived)
                    Announce(new PlaceEnteredEvent(member, placeId, from, how, routeId));
            }
        }

        private void Announce(PlaceEnteredEvent entered)
        {
            _events?.Publish(entered);
            PlaceEntered?.Invoke(entered);
        }

        // A player not met yet reads as one at the start, without being added
        private Traveller Peek(int playerId)
        {
            if (_travellers.TryGetValue(playerId, out var traveller))
                return traveller;
            traveller = new Traveller { Location = Map.Start };
            Learn(traveller, Map.Start);
            return traveller;
        }

        private Traveller Get(int playerId)
        {
            if (!_travellers.TryGetValue(playerId, out var traveller))
            {
                traveller = new Traveller { Location = Map.Start };
                Learn(traveller, Map.Start);
                _travellers.Add(playerId, traveller);
            }
            return traveller;
        }

        private static void Learn(Traveller traveller, string placeId)
        {
            if (traveller.Known.Add(placeId))
                traveller.Discovered.Add(placeId);
        }
    }
}
