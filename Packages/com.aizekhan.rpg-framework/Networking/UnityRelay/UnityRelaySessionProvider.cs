using System;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using RPGFramework.Networking.UnityTransport;
using Unity.Networking.Transport.Relay;
using UnityEngine;

namespace RPGFramework.Networking.UnityRelay
{
    /// <summary>
    /// Play over the internet without a VPN: the host opens a room on Unity's servers and gets a short code; friends
    /// type it and their traffic goes through Unity Relay. The game's transport, messages and rules are the same as
    /// on a local network. Compiled only when the Multiplayer Services SDK (com.unity.services.multiplayer) is
    /// installed; it then registers itself in <see cref="NetworkSessionProviders"/>, so games offer it with no code.
    /// The project must be linked to a Unity Gaming Services project (see <see cref="RelayErrors.NotSetUp"/>).
    /// </summary>
    public sealed class UnityRelaySessionProvider : INetworkSessionProvider
    {
        public const string Id = "unity-relay";
        public const string Name = "Internet (Unity Relay)";

        /// <summary>
        /// Opens the game's transport from relay data: listening for a host, connecting for a player.
        /// </summary>
        public delegate INetworkTransport TransportFactory(RelayServerData relay, bool host);

        private readonly IRelayService _service;
        private readonly TransportFactory _openTransport;

        public UnityRelaySessionProvider() : this(new MultiplayerRelayService())
        {
        }

        /// <param name="service">The room service (a fake in tests).</param>
        /// <param name="openTransport">How transports are opened; Unity Transport through the relay by default.</param>
        public UnityRelaySessionProvider(IRelayService service, TransportFactory openTransport = null)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _openTransport = openTransport ?? OpenRelayTransport;
        }

        public string DisplayName => Name;
        public string JoinCodeHint => "the code on the host's screen";

        /// <summary>
        /// How long a host waits for the relay to accept it before giving up.
        /// </summary>
        public float RelayTimeoutSeconds { get; set; } = 10f;

        /// <summary>
        /// Offers this provider to games (<see cref="NetworkSessionProviders"/>); runs by itself when the game starts.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Register() =>
            NetworkSessionProviders.Register(Id, Name, () => new UnityRelaySessionProvider());

        public async UniTask<HostedSession> HostAsync(SessionOptions options, CancellationToken cancellation = default)
        {
            ThrowIfNotSetUp();
            options = options ?? new SessionOptions();
            INetworkTransport transport = null;
            try
            {
                string code = await _service.CreateAsync(options.MaxPlayers, options.Name, async relay =>
                {
                    transport = Open(relay, host: true);
                    await WaitForRelay(transport, cancellation);
                }, cancellation);
                if (transport == null)
                    throw new NetworkSessionException("Unity Relay opened the room but not its network. Try hosting again.");
                return new HostedSession(new RelaySessionTransport(transport, _service), code);
            }
            catch
            {
                Abandon(transport);
                throw;
            }
        }

        public async UniTask<INetworkTransport> JoinAsync(string joinCode, CancellationToken cancellation = default)
        {
            ThrowIfNotSetUp();
            string code = CleanCode(joinCode);
            if (code.Length == 0)
                throw new NetworkSessionException("Type the code shown on the host's screen.");
            INetworkTransport transport = null;
            try
            {
                await _service.JoinAsync(code, relay =>
                {
                    transport = Open(relay, host: false);
                    return UniTask.CompletedTask;
                }, cancellation);
                if (transport == null)
                    throw new NetworkSessionException("Unity Relay let you into the room but not its network. Try joining again.");
                return new RelaySessionTransport(transport, _service);
            }
            catch
            {
                Abandon(transport);
                throw;
            }
        }

        /// <summary>
        /// Codes are case-insensitive and may be typed with spaces or dashes ("abc-123" is "ABC123").
        /// </summary>
        public static string CleanCode(string code)
        {
            var clean = new StringBuilder();
            foreach (char c in code ?? "")
            {
                if (char.IsLetterOrDigit(c))
                    clean.Append(char.ToUpperInvariant(c));
            }
            return clean.ToString();
        }

        /// <summary>
        /// A failed start leaves nothing behind: no open driver, no room kept on Unity's servers.
        /// </summary>
        private void Abandon(INetworkTransport transport)
        {
            (transport as IDisposable)?.Dispose();
            _service.LeaveAsync().Forget();
        }

        private void ThrowIfNotSetUp()
        {
            string problem = _service.SetupProblem;
            if (problem != null)
                throw new NetworkSessionException(problem);
        }

        private INetworkTransport Open(RelayServerData relay, bool host)
        {
            try
            {
                return _openTransport(relay, host) ?? throw new NetworkSessionException("No transport for the relay.");
            }
            catch (Exception exception) when (exception is InvalidOperationException || exception is ArgumentException)
            {
                throw new NetworkSessionException($"The relay connection could not be opened: {exception.Message}", exception);
            }
        }

        private static INetworkTransport OpenRelayTransport(RelayServerData relay, bool host)
        {
            var transport = UnityNetworkTransport.ThroughRelay(relay);
            if (host)
                transport.StartServer(0);
            else
                transport.Connect(relay.Endpoint);
            return transport;
        }

        /// <summary>
        /// A host is reachable once the relay has accepted it; a transport that is not Unity Transport has no relay.
        /// </summary>
        private async UniTask WaitForRelay(INetworkTransport transport, CancellationToken cancellation)
        {
            if (!(transport is UnityNetworkTransport relayed) || relayed.RelayStatus == RelayConnectionStatus.NotUsingRelay)
                return;
            var deadline = Time.realtimeSinceStartup + RelayTimeoutSeconds;
            while (true)
            {
                relayed.Update();
                switch (relayed.RelayStatus)
                {
                    case RelayConnectionStatus.Established:
                        return;
                    case RelayConnectionStatus.AllocationInvalid:
                        throw new NetworkSessionException("Unity Relay refused this game's allocation (it may have expired). Host again.");
                }
                if (Time.realtimeSinceStartup >= deadline)
                {
                    throw new NetworkSessionException("Unity Relay did not answer. Check the internet connection and host again.");
                }
                await UniTask.Delay(50, DelayType.Realtime, cancellationToken: cancellation);
            }
        }
    }
}
