using RPGFramework.Events;

namespace RPGFramework.Loot
{
    /// <summary>
    /// Event raised when loot is generated from a loot table.
    /// Published by the code that rolls loot for something that happened (the generator itself only rolls):
    /// <c>ActorWorld</c> publishes it when the party kills an enemy with a loot table, before the
    /// <see cref="LootDroppedEvent"/>s for what was handed out.
    /// </summary>
    public struct LootGeneratedEvent : IEvent
    {
        /// <summary>
        /// Loot table ID that was rolled.
        /// </summary>
        public string TableId;

        /// <summary>
        /// Entity ID that triggered loot generation (killer, chest opener, etc.).
        /// -1 = no specific entity (manual generation, scripted event).
        /// </summary>
        public int TriggerEntityId;

        /// <summary>
        /// Source entity ID (killed enemy, opened chest, etc.).
        /// -1 = no source entity.
        /// </summary>
        public int SourceEntityId;

        /// <summary>
        /// Number of items generated.
        /// </summary>
        public int ItemCount;

        /// <summary>
        /// Timestamp when loot was generated (0 when the publisher keeps no clock).
        /// </summary>
        public float Timestamp;
    }
}
