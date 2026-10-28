using RPGFramework.Events;

namespace RPGFramework.Interaction
{
    /// <summary>
    /// A player used a place. Published by <see cref="InteractionService.Interact"/> once the place has accepted
    /// (near enough, open, the kind's rule allows it); a <see cref="InteractionPoint.Once"/> place is already closed.
    /// </summary>
    public struct InteractedEvent : IEvent
    {
        public InteractedEvent(int playerId, string pointId, string kind)
        {
            PlayerId = playerId;
            PointId = pointId;
            Kind = kind;
        }

        /// <summary>Entity id of the player who used it</summary>
        public int PlayerId;

        /// <summary><see cref="InteractionPoint.Id"/></summary>
        public string PointId;

        /// <summary><see cref="InteractionPoint.Kind"/></summary>
        public string Kind;
    }
}
