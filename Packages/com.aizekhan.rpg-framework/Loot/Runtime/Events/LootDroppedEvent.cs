using RPGFramework.Events;

namespace RPGFramework.Loot
{
    /// <summary>
    /// Event raised when a loot item is handed out (dropped in the world or put into an inventory), one per item kind.
    /// Published by the code that hands loot out, since the generator only rolls: <c>ActorWorld</c> publishes it for
    /// loot from enemies the party kills; publish it from your own loot code (chests, rewards) the same way.
    /// </summary>
    public struct LootDroppedEvent : IEvent
    {
        /// <summary>
        /// Item definition ID dropped.
        /// </summary>
        public string ItemId;

        /// <summary>
        /// Quantity dropped.
        /// </summary>
        public int Quantity;

        /// <summary>
        /// Entity ID representing the dropped item in world.
        /// References the ItemComponent entity created for this drop.
        /// -1 = not spawned in the world (it went straight into an inventory).
        /// </summary>
        public int ItemEntityId;

        /// <summary>
        /// Source entity ID (killed enemy, opened chest).
        /// -1 = no source entity.
        /// </summary>
        public int SourceEntityId;

        /// <summary>
        /// Timestamp when loot was dropped (0 when the publisher keeps no clock).
        /// </summary>
        public float Timestamp;
    }
}
