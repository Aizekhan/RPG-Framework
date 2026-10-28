using System;

namespace RPGFramework.Interaction
{
    /// <summary>
    /// A place a player uses when they are near it: a chest, a door, a lever, a trader, a shrine. What using it does
    /// is the game's (<see cref="InteractedEvent"/>); the place only knows how near is near enough and whether it is
    /// still open.
    /// </summary>
    public sealed class InteractionPoint
    {
        /// <param name="id">Unique among the places in the world (a chest's id in the scene, "shop_door").</param>
        /// <param name="kind">What sort of place it is ("chest", "door", "trader"): rules and prompts go by it.</param>
        /// <param name="radius">How near a player has to be, in the distance the game measures.</param>
        /// <param name="prompt">What the screen shows when it is the nearest ("Open the chest"); the kind when empty.</param>
        /// <param name="once">It closes after the first use (a chest, a pickup).</param>
        public InteractionPoint(string id, string kind, float radius, string prompt = null, bool once = false)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("A place needs an id", nameof(id));
            if (string.IsNullOrWhiteSpace(kind))
                throw new ArgumentException("A place needs a kind", nameof(kind));
            if (float.IsNaN(radius) || float.IsInfinity(radius) || radius < 0f)
                throw new ArgumentOutOfRangeException(nameof(radius), radius, "The radius must be 0 or more");
            Id = id;
            Kind = kind;
            Radius = radius;
            Prompt = string.IsNullOrWhiteSpace(prompt) ? kind : prompt;
            Once = once;
        }

        public string Id { get; }
        public string Kind { get; }
        public float Radius { get; }
        public string Prompt { get; }
        public bool Once { get; }

        /// <summary>
        /// Players may use it; a <see cref="Once"/> place closes itself after its first use.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Where it stands, for games that measure distance in free space (the scene sets it); a game with its own
        /// measure (a track, a grid) may leave it at 0 and keep the position elsewhere.
        /// </summary>
        public float X { get; private set; }

        public float Y { get; private set; }
        public float Z { get; private set; }

        /// <summary>
        /// Moves the place (returns it, so a new place can be placed in one line).
        /// </summary>
        public InteractionPoint At(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
            return this;
        }
    }
}
