using System;
using System.Collections.Generic;
using System.Linq;

namespace RPGFramework.Guilds.Runtime
{
    /// <summary>
    /// Concrete implementation of IGuildRegistry.
    /// Stores and manages all guilds in the game.
    /// </summary>
    public class GuildRegistry : IGuildRegistry
    {
        private readonly Dictionary<string, IGuild> _guilds;
        private readonly Dictionary<string, string> _nameToIdMap; // lowercase name -> guildId
        private readonly Dictionary<string, string> _tagToIdMap; // lowercase tag -> guildId

        /// <summary>
        /// Create a new guild registry.
        /// </summary>
        public GuildRegistry()
        {
            _guilds = new Dictionary<string, IGuild>();
            _nameToIdMap = new Dictionary<string, string>();
            _tagToIdMap = new Dictionary<string, string>();
        }

        public void RegisterGuild(IGuild guild)
        {
            if (guild == null)
                throw new ArgumentNullException(nameof(guild));

            if (_guilds.ContainsKey(guild.GuildId))
                throw new InvalidOperationException($"Guild '{guild.GuildId}' is already registered");

            _guilds[guild.GuildId] = guild;
            _nameToIdMap[guild.Name.ToLowerInvariant()] = guild.GuildId;
            _tagToIdMap[guild.Tag.ToLowerInvariant()] = guild.GuildId;
        }

        public void UnregisterGuild(string guildId)
        {
            if (_guilds.TryGetValue(guildId, out var guild))
            {
                _guilds.Remove(guildId);
                _nameToIdMap.Remove(guild.Name.ToLowerInvariant());
                _tagToIdMap.Remove(guild.Tag.ToLowerInvariant());
            }
        }

        public IGuild GetGuild(string guildId)
        {
            return _guilds.TryGetValue(guildId, out var guild) ? guild : null;
        }

        public bool HasGuild(string guildId)
        {
            return _guilds.ContainsKey(guildId);
        }

        public IReadOnlyList<IGuild> GetAllGuilds()
        {
            return _guilds.Values.ToList();
        }

        public IReadOnlyList<IGuild> SearchGuildsByName(string searchTerm)
        {
            if (string.IsNullOrEmpty(searchTerm))
                return new List<IGuild>();

            var lowerSearch = searchTerm.ToLowerInvariant();
            return _guilds.Values
                .Where(g => g.Name.ToLowerInvariant().Contains(lowerSearch))
                .ToList();
        }

        public IGuild GetGuildByTag(string tag)
        {
            if (string.IsNullOrEmpty(tag))
                return null;

            var lowerTag = tag.ToLowerInvariant();
            if (_tagToIdMap.TryGetValue(lowerTag, out string guildId))
            {
                return _guilds[guildId];
            }

            return null;
        }

        public bool IsNameTaken(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;

            return _nameToIdMap.ContainsKey(name.ToLowerInvariant());
        }

        public bool IsTagTaken(string tag)
        {
            if (string.IsNullOrEmpty(tag))
                return false;

            return _tagToIdMap.ContainsKey(tag.ToLowerInvariant());
        }
    }
}
