using System;
using RPGFramework.Events;

namespace RPGFramework.Guilds.Runtime
{
    /// <summary>
    /// Event raised when a new guild is created.
    /// </summary>
    public class GuildCreatedEvent : IEvent
    {
        /// <summary>
        /// Guild that was created.
        /// </summary>
        public IGuild Guild { get; }

        /// <summary>
        /// Entity ID of the guild founder/leader.
        /// </summary>
        public int FounderId { get; }

        /// <summary>
        /// Timestamp when the guild was created.
        /// </summary>
        public DateTime Timestamp { get; }

        public GuildCreatedEvent(IGuild guild, int founderId)
        {
            Guild = guild ?? throw new ArgumentNullException(nameof(guild));
            FounderId = founderId;
            Timestamp = DateTime.UtcNow;
        }
    }
}
