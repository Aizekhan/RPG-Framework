using System;
using System.Collections.Generic;

namespace RPGFramework.Interaction
{
    /// <summary>
    /// What became of a player's try to use a place.
    /// </summary>
    public enum InteractionOutcome
    {
        Done,

        /// <summary>
        /// No place with that id, or (asking for the nearest) none near enough.
        /// </summary>
        NotFound,

        /// <summary>
        /// The place is closed (<see cref="InteractionPoint.Enabled"/> is false: a used chest).
        /// </summary>
        Closed,

        TooFar,

        /// <summary>
        /// The rule of the place's kind said no (a locked door without the key).
        /// </summary>
        Refused
    }

    /// <summary>
    /// The places players can use, and which one is nearest to each player. Positions are the game's: it measures
    /// with <see cref="Distance"/> (free 3D, a track, a grid), so the same service fits any world.
    /// </summary>
    public interface IInteractionService
    {
        /// <summary>
        /// Distance from a player to a place, in the units of <see cref="InteractionPoint.Radius"/>; NaN when it
        /// cannot be measured (the player is nowhere), which counts as too far. Null counts every place as in reach.
        /// </summary>
        Func<int, InteractionPoint, float> Distance { get; set; }

        IReadOnlyCollection<InteractionPoint> Points { get; }

        /// <summary>
        /// Adds the place, replacing one with the same id.
        /// </summary>
        void Add(InteractionPoint point);

        bool Remove(string pointId);

        /// <returns>The place, or null.</returns>
        InteractionPoint Get(string pointId);

        void Clear();

        /// <summary>
        /// Whether a player may use places of a kind now (a door needs a key, a shrine a quest); null removes the
        /// rule. Places of a kind without a rule may always be used.
        /// </summary>
        void SetRule(string kind, Func<int, InteractionPoint, bool> allowed);

        /// <summary>
        /// The place is open and the player is within its radius (the kind's rule is not asked).
        /// </summary>
        bool CanReach(int playerId, InteractionPoint point);

        /// <summary>
        /// The open place nearest to the player within its radius (for the prompt); null when there is none. Of
        /// places equally near, the one added first.
        /// </summary>
        InteractionPoint NearestFor(int playerId);

        /// <summary>
        /// The player uses a place (the nearest when <paramref name="pointId"/> is null): it checks the place is
        /// there, open, near enough and allowed, closes a <see cref="InteractionPoint.Once"/> place and publishes
        /// <see cref="InteractedEvent"/>.
        /// </summary>
        InteractionOutcome Interact(int playerId, string pointId = null);
    }
}
