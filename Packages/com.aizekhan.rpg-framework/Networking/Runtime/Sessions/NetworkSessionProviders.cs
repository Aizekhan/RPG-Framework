using System;
using System.Collections.Generic;

namespace RPGFramework.Networking
{
    /// <summary>
    /// Session providers that optional modules offer when their service is installed (a relay, a platform's lobbies).
    /// A module registers itself; a game lists <see cref="Available"/> next to its own default (usually the local
    /// network), so installing a service's SDK adds a way to play together without changing the game's code.
    /// </summary>
    public static class NetworkSessionProviders
    {
        private static readonly List<Entry> _entries = new List<Entry>();

        public sealed class Entry
        {
            internal Entry(string id, string displayName, Func<INetworkSessionProvider> create)
            {
                Id = id;
                DisplayName = displayName;
                Create = create;
            }

            /// <summary>
            /// Stable name, e.g. "unity-relay" (command lines, saved choices).
            /// </summary>
            public string Id { get; }

            public string DisplayName { get; }

            /// <summary>
            /// Makes a new provider (each game keeps its own).
            /// </summary>
            public Func<INetworkSessionProvider> Create { get; }
        }

        public static IReadOnlyList<Entry> Available => _entries;

        /// <summary>
        /// Offers a provider; registering the same id again replaces it.
        /// </summary>
        public static void Register(string id, string displayName, Func<INetworkSessionProvider> create)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("A provider needs an id", nameof(id));
            if (create == null)
                throw new ArgumentNullException(nameof(create));
            Unregister(id);
            _entries.Add(new Entry(id, string.IsNullOrWhiteSpace(displayName) ? id : displayName, create));
        }

        public static bool Unregister(string id) => _entries.RemoveAll(entry => entry.Id == id) > 0;

        public static Entry Find(string id) => _entries.Find(entry => entry.Id == id);
    }
}
