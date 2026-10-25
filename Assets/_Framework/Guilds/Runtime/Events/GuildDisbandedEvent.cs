using System;

namespace RPGFramework.Guilds.Runtime
{
    /// <summary>
    /// Event raised when a guild is disbanded.
    /// </summary>
    public class GuildDisbandedEvent
    {
        /// <summary>
        /// Guild ID that was disbanded.
        /// </summary>
        public string GuildId { get; }

        /// <summary>
        /// Guild name that was disbanded.
        /// </summary>
        public string GuildName { get; }

        /// <summary>
        /// Entity ID of who disbanded the guild (leader or admin).
        /// </summary>
        public int DisbandedBy { get; }

        /// <summary>
        /// Timestamp when the guild was disbanded.
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// Reason for disbanding (optional).
        /// </summary>
        public string Reason { get; }

        public GuildDisbandedEvent(string guildId, string guildName, int disbandedBy, string reason = null)
        {
            GuildId = guildId ?? throw new ArgumentNullException(nameof(guildId));
            GuildName = guildName ?? string.Empty;
            DisbandedBy = disbandedBy;
            Timestamp = DateTime.UtcNow;
            Reason = reason;
        }
    }
}
