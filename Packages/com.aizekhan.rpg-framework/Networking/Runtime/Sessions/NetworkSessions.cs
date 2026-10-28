using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace RPGFramework.Networking
{
    /// <summary>
    /// How a game is opened to others and found by them: the local network by IP address, a relay service by room
    /// code, a platform's lobbies and invites. A provider hands back a ready transport, so the game's own networking
    /// (messages, replication, rules) never depends on the way players connect. Providers for services live in
    /// assemblies of their own; the game picks one.
    /// </summary>
    public interface INetworkSessionProvider
    {
        /// <summary>
        /// Shown to players ("Local network").
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// What a joining player types, in words ("the host's IP address", "the room code").
        /// </summary>
        string JoinCodeHint { get; }

        /// <summary>
        /// Opens a session on this machine.
        /// </summary>
        /// <returns>A transport already listening, and the code others join with.</returns>
        /// <exception cref="NetworkSessionException">It could not be opened; the message says why, for the player.</exception>
        UniTask<HostedSession> HostAsync(SessionOptions options, CancellationToken cancellation = default);

        /// <summary>
        /// Finds the session behind a code and starts connecting to it.
        /// </summary>
        /// <returns>A transport already connecting; success or failure arrives as its Connected / Disconnected event.</returns>
        /// <exception cref="NetworkSessionException">The code is wrong or the service refused; the message says why.</exception>
        UniTask<INetworkTransport> JoinAsync(string joinCode, CancellationToken cancellation = default);
    }

    /// <summary>
    /// What a hosted session should be like.
    /// </summary>
    public sealed class SessionOptions
    {
        /// <summary>
        /// Most players, the host included.
        /// </summary>
        public int MaxPlayers = 4;

        /// <summary>
        /// The session's name where a provider lists sessions (e.g. the host player's name).
        /// </summary>
        public string Name = "";
    }

    /// <summary>
    /// A session this machine hosts: the listening transport and the code others join with.
    /// </summary>
    public sealed class HostedSession
    {
        public HostedSession(INetworkTransport transport, string joinCode, IReadOnlyList<string> otherJoinCodes = null)
        {
            Transport = transport ?? throw new ArgumentNullException(nameof(transport));
            JoinCode = joinCode ?? "";
            OtherJoinCodes = otherJoinCodes ?? Array.Empty<string>();
        }

        public INetworkTransport Transport { get; }
        public string JoinCode { get; }

        /// <summary>
        /// Other codes that reach the same session (the host's address on a VPN such as Tailscale or Radmin, a second
        /// network card); empty when there is only one.
        /// </summary>
        public IReadOnlyList<string> OtherJoinCodes { get; }
    }

    /// <summary>
    /// A session could not be hosted or joined; <see cref="Exception.Message"/> is written for the player.
    /// </summary>
    public sealed class NetworkSessionException : Exception
    {
        public NetworkSessionException(string message, Exception inner = null) : base(message, inner)
        {
        }
    }

    /// <summary>
    /// Sessions on a <see cref="LoopbackNetwork"/> (tests, a host and clients in one process). The join code is the
    /// port number.
    /// </summary>
    public sealed class LoopbackSessionProvider : INetworkSessionProvider
    {
        private readonly LoopbackNetwork _network;
        private int _nextPort;

        public LoopbackSessionProvider(LoopbackNetwork network, int firstPort = 7777)
        {
            _network = network ?? throw new ArgumentNullException(nameof(network));
            _nextPort = firstPort;
        }

        public string DisplayName => "In-process";
        public string JoinCodeHint => "the session number";

        public UniTask<HostedSession> HostAsync(SessionOptions options, CancellationToken cancellation = default)
        {
            cancellation.ThrowIfCancellationRequested();
            var transport = _network.CreateTransport();
            int port = _nextPort++;
            try
            {
                transport.StartServer(port);
            }
            catch (InvalidOperationException exception)
            {
                throw new NetworkSessionException(exception.Message, exception);
            }
            return UniTask.FromResult(new HostedSession(transport, port.ToString()));
        }

        public UniTask<INetworkTransport> JoinAsync(string joinCode, CancellationToken cancellation = default)
        {
            cancellation.ThrowIfCancellationRequested();
            if (!int.TryParse(joinCode?.Trim(), out int port))
                throw new NetworkSessionException($"'{joinCode}' is not a session number.");
            var transport = _network.CreateTransport();
            transport.Connect("loopback", port);
            return UniTask.FromResult<INetworkTransport>(transport);
        }
    }
}
