using System.Collections.Generic;

namespace RPGFramework.Guilds.Runtime
{
    /// <summary>
    /// Registry for all guilds in the game.
    /// Manages guild storage and retrieval.
    /// </summary>
    public interface IGuildRegistry
    {
        /// <summary>
        /// Register a new guild.
        /// </summary>
        /// <param name="guild">Guild to register.</param>
        void RegisterGuild(IGuild guild);

        /// <summary>
        /// Unregister a guild (when disbanded).
        /// </summary>
        /// <param name="guildId">Guild ID to unregister.</param>
        void UnregisterGuild(string guildId);

        /// <summary>
        /// Get a guild by ID.
        /// </summary>
        /// <param name="guildId">Guild identifier.</param>
        /// <returns>The guild, or null if not found.</returns>
        IGuild GetGuild(string guildId);

        /// <summary>
        /// Check if a guild exists.
        /// </summary>
        /// <param name="guildId">Guild ID to check.</param>
        /// <returns>True if guild exists.</returns>
        bool HasGuild(string guildId);

        /// <summary>
        /// Get all registered guilds.
        /// </summary>
        /// <returns>Read-only collection of all guilds.</returns>
        IReadOnlyList<IGuild> GetAllGuilds();

        /// <summary>
        /// Get guilds by name search (partial match).
        /// </summary>
        /// <param name="searchTerm">Search term (case-insensitive).</param>
        /// <returns>Guilds matching the search.</returns>
        IReadOnlyList<IGuild> SearchGuildsByName(string searchTerm);

        /// <summary>
        /// Get guilds by tag.
        /// </summary>
        /// <param name="tag">Guild tag (case-insensitive).</param>
        /// <returns>Guild with matching tag, or null.</returns>
        IGuild GetGuildByTag(string tag);

        /// <summary>
        /// Check if a guild name is already in use.
        /// </summary>
        /// <param name="name">Guild name to check.</param>
        /// <returns>True if name is taken.</returns>
        bool IsNameTaken(string name);

        /// <summary>
        /// Check if a guild tag is already in use.
        /// </summary>
        /// <param name="tag">Guild tag to check.</param>
        /// <returns>True if tag is taken.</returns>
        bool IsTagTaken(string tag);
    }
}
