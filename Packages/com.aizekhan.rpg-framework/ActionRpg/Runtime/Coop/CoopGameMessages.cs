using System;
using System.Collections.Generic;
using RPGFramework.Networking;

namespace RPGFramework.ActionRpg.Coop
{
    /// <summary>
    /// A game's own messages over the co-op link (a vote, a seed, a chest opened): each with an id from
    /// <see cref="CoopProtocol.FirstGameMessageId"/> up and who may send it. Give the same set to
    /// <see cref="CoopProtocol.CreateNetwork"/> on the host and on every client, then handle and send them with
    /// <see cref="CoopHost.On{T}"/> / <see cref="CoopHost.Send"/> and <see cref="CoopClient.On{T}"/> /
    /// <see cref="CoopClient.Send"/>. The framework's own ids (1100-1199) cannot be taken.
    /// </summary>
    public sealed class CoopGameMessages
    {
        private readonly List<Action<NetworkSerializer, NetworkAuthority>> _registrations =
            new List<Action<NetworkSerializer, NetworkAuthority>>();
        private readonly HashSet<int> _ids = new HashSet<int>();

        /// <param name="id"><see cref="CoopProtocol.FirstGameMessageId"/> or more, unique in the set.</param>
        /// <param name="authority">Client: players send it to the host; Server: the host sends it; Any: both.</param>
        public CoopGameMessages Add<T>(int id, MessageAuthority authority) where T : INetworkMessage, new()
        {
            if (id < CoopProtocol.FirstGameMessageId)
                throw new ArgumentOutOfRangeException(nameof(id),
                    $"Game messages use ids from {CoopProtocol.FirstGameMessageId}; lower ones are the framework's");
            if (!_ids.Add(id))
                throw new ArgumentException($"Message id {id} is already in the set", nameof(id));
            if (new T().MessageTypeId != id)
                throw new ArgumentException($"{typeof(T).Name}.MessageTypeId must be {id}", nameof(id));
            _registrations.Add((serializer, rules) =>
            {
                serializer.RegisterMessageType<T>(id);
                rules.Set(id, authority);
            });
            return this;
        }

        public int Count => _ids.Count;

        internal void RegisterOn(NetworkSerializer serializer, NetworkAuthority authority)
        {
            foreach (var register in _registrations)
                register(serializer, authority);
        }
    }
}
