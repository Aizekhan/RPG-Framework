using System;
using System.Collections.Generic;

namespace RPGFramework.WorldMap
{
    /// <summary>
    /// A place on the world map: a town, a camp, a dungeon's gate. Where it is drawn (<see cref="X"/>, <see cref="Y"/>)
    /// is up to the game's map screen.
    /// </summary>
    public sealed class WorldPlace
    {
        public WorldPlace(string id, string name = null, float x = 0f, float y = 0f)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("A place needs an id", nameof(id));
            Id = id;
            Name = string.IsNullOrEmpty(name) ? id : name;
            X = x;
            Y = y;
        }

        public string Id { get; }

        /// <summary>
        /// Shown on the map (or a localization key); the id when none is given.
        /// </summary>
        public string Name { get; }

        public float X { get; }
        public float Y { get; }

        public override string ToString() => Id;
    }

    /// <summary>
    /// A way from one place to another: a road, a track, a level the party plays through. One way only; add a second
    /// route for the way back. A route that ends where it starts is a loop (<see cref="IsLoop"/>).
    /// </summary>
    public sealed class WorldRoute
    {
        /// <param name="to">Where it leads; null or empty: back to <paramref name="from"/> (a loop).</param>
        public WorldRoute(string id, string from, string to = null, string name = null)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("A route needs an id", nameof(id));
            if (string.IsNullOrWhiteSpace(from))
                throw new ArgumentException("A route needs the place it leaves", nameof(from));
            Id = id;
            From = from;
            To = string.IsNullOrEmpty(to) ? from : to;
            Name = string.IsNullOrEmpty(name) ? id : name;
        }

        public string Id { get; }
        public string From { get; }
        public string To { get; }
        public string Name { get; }

        public bool IsLoop => From == To;

        public override string ToString() => $"{Id} ({From} -> {To})";
    }

    /// <summary>
    /// The world's places and the routes between them. Players begin at <see cref="Start"/> (the first place added
    /// unless set). Add the places first, then the routes between them.
    /// </summary>
    public sealed class WorldGraph
    {
        private static readonly IReadOnlyList<WorldRoute> NoRoutes = Array.Empty<WorldRoute>();

        private readonly List<WorldPlace> _places = new List<WorldPlace>();
        private readonly List<WorldRoute> _routes = new List<WorldRoute>();
        private readonly Dictionary<string, WorldPlace> _placesById = new Dictionary<string, WorldPlace>();
        private readonly Dictionary<string, WorldRoute> _routesById = new Dictionary<string, WorldRoute>();
        private readonly Dictionary<string, List<WorldRoute>> _routesFrom = new Dictionary<string, List<WorldRoute>>();
        private string _start;

        public IReadOnlyList<WorldPlace> Places => _places;
        public IReadOnlyList<WorldRoute> Routes => _routes;

        /// <summary>
        /// Where players stand before they have moved, discovered from the first moment.
        /// </summary>
        public string Start
        {
            get => _start ?? (_places.Count > 0 ? _places[0].Id : null);
            set
            {
                if (value != null && !_placesById.ContainsKey(value))
                    throw new ArgumentException($"'{value}' is not a place on this map", nameof(value));
                _start = value;
            }
        }

        public WorldGraph AddPlace(WorldPlace place)
        {
            if (place == null)
                throw new ArgumentNullException(nameof(place));
            if (_placesById.ContainsKey(place.Id))
                throw new ArgumentException($"The map already has a place '{place.Id}'", nameof(place));
            _places.Add(place);
            _placesById.Add(place.Id, place);
            return this;
        }

        public WorldGraph AddPlace(string id, string name = null, float x = 0f, float y = 0f) =>
            AddPlace(new WorldPlace(id, name, x, y));

        /// <exception cref="ArgumentException">The id is taken, or an end is not a place on this map.</exception>
        public WorldGraph AddRoute(WorldRoute route)
        {
            if (route == null)
                throw new ArgumentNullException(nameof(route));
            if (_routesById.ContainsKey(route.Id))
                throw new ArgumentException($"The map already has a route '{route.Id}'", nameof(route));
            if (!_placesById.ContainsKey(route.From))
                throw new ArgumentException($"Route '{route.Id}' leaves '{route.From}', which is not a place on this map", nameof(route));
            if (!_placesById.ContainsKey(route.To))
                throw new ArgumentException($"Route '{route.Id}' leads to '{route.To}', which is not a place on this map", nameof(route));
            _routes.Add(route);
            _routesById.Add(route.Id, route);
            if (!_routesFrom.TryGetValue(route.From, out var from))
                _routesFrom.Add(route.From, from = new List<WorldRoute>());
            from.Add(route);
            return this;
        }

        public WorldGraph AddRoute(string id, string from, string to = null, string name = null) =>
            AddRoute(new WorldRoute(id, from, to, name));

        /// <returns>Null when there is no such place.</returns>
        public WorldPlace Place(string id) => id != null && _placesById.TryGetValue(id, out var place) ? place : null;

        /// <returns>Null when there is no such route.</returns>
        public WorldRoute Route(string id) => id != null && _routesById.TryGetValue(id, out var route) ? route : null;

        /// <summary>
        /// The routes that leave a place, in the order they were added (loops included).
        /// </summary>
        public IReadOnlyList<WorldRoute> RoutesFrom(string placeId) =>
            placeId != null && _routesFrom.TryGetValue(placeId, out var routes) ? routes : NoRoutes;
    }
}
