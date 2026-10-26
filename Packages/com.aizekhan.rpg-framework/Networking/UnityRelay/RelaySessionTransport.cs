using System;
using Cysharp.Threading.Tasks;

namespace RPGFramework.Networking.UnityRelay
{
    /// <summary>
    /// The game's transport while in a relayed room: everything goes to the inner transport, and closing it also
    /// leaves the room (a host's room closes for everyone).
    /// </summary>
    public sealed class RelaySessionTransport : INetworkTransport, IDisposable
    {
        private readonly INetworkTransport _inner;
        private readonly IRelayService _service;
        private bool _left;

        public RelaySessionTransport(INetworkTransport inner, IRelayService service)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        /// <summary>
        /// The transport that carries the bytes (a Unity Transport driver through the relay).
        /// </summary>
        public INetworkTransport Inner => _inner;

        public bool IsActive => _inner.IsActive;

        public void StartServer(int port) => _inner.StartServer(port);

        public void Connect(string address, int port) => _inner.Connect(address, port);

        public void Send(int connectionId, byte[] data, bool reliable) => _inner.Send(connectionId, data, reliable);

        public void Broadcast(byte[] data, bool reliable) => _inner.Broadcast(data, reliable);

        public bool Poll(out NetworkTransportEvent transportEvent) => _inner.Poll(out transportEvent);

        public void Disconnect()
        {
            _inner.Disconnect();
            Leave();
        }

        public void Dispose()
        {
            (_inner as IDisposable)?.Dispose();
            Leave();
        }

        private void Leave()
        {
            if (_left)
                return;
            _left = true;
            _service.LeaveAsync().Forget();
        }
    }
}
