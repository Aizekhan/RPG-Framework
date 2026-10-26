using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace RPGFramework.Networking.UnityTransport
{
    /// <summary>
    /// Sessions on the local network (or a VPN such as Tailscale or ZeroTier): the host listens on a UDP port and
    /// the join code is its IP address, with ":port" when the port is not the usual one. No service, no account.
    /// </summary>
    public sealed class LanSessionProvider : INetworkSessionProvider
    {
        public LanSessionProvider(int port = DefaultPort) => Port = port;

        public const int DefaultPort = 7777;

        /// <summary>
        /// The port hosts listen on and joins go to when the code has none (0 when hosting picks a free one).
        /// </summary>
        public int Port { get; set; }

        public string DisplayName => "Local network";
        public string JoinCodeHint => "the host's IP address";

        public UniTask<HostedSession> HostAsync(SessionOptions options, CancellationToken cancellation = default)
        {
            cancellation.ThrowIfCancellationRequested();
            var transport = new UnityNetworkTransport();
            try
            {
                transport.StartServer(Port);
            }
            catch (InvalidOperationException exception)
            {
                transport.Dispose();
                throw new NetworkSessionException(exception.Message, exception);
            }
            int port = transport.ListeningPort;
            string address = LocalAddress();
            if (address.Length == 0)
                address = "127.0.0.1";
            string code = port == DefaultPort ? address : $"{address}:{port}";
            return UniTask.FromResult(new HostedSession(transport, code));
        }

        public UniTask<INetworkTransport> JoinAsync(string joinCode, CancellationToken cancellation = default)
        {
            cancellation.ThrowIfCancellationRequested();
            if (!TryParse(joinCode, Port > 0 ? Port : DefaultPort, out string address, out int port))
                throw new NetworkSessionException($"'{joinCode}' is not an IP address. Ask the host for the address the co-op panel shows them (like 192.168.1.20).");
            var transport = new UnityNetworkTransport();
            try
            {
                transport.Connect(address, port);
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
            {
                transport.Dispose();
                throw new NetworkSessionException($"Could not join {joinCode}: {exception.Message}", exception);
            }
            return UniTask.FromResult<INetworkTransport>(transport);
        }

        /// <summary>
        /// Splits "192.168.1.20", "192.168.1.20:7790", "::1" or "[::1]:7790" into an address and a port.
        /// </summary>
        public static bool TryParse(string joinCode, int defaultPort, out string address, out int port)
        {
            address = (joinCode ?? "").Trim();
            port = defaultPort;
            if (address.StartsWith("[", StringComparison.Ordinal))
            {
                int close = address.IndexOf(']');
                if (close < 0)
                    return false;
                string rest = address.Substring(close + 1);
                address = address.Substring(1, close - 1);
                if (rest.Length > 0 && !(rest[0] == ':' && int.TryParse(rest.Substring(1), out port)))
                    return false;
            }
            else if (address.Count(c => c == ':') == 1)
            {
                int colon = address.IndexOf(':');
                if (!int.TryParse(address.Substring(colon + 1), out port))
                    return false;
                address = address.Substring(0, colon);
            }
            return port > 0 && port <= 65535 && IPAddress.TryParse(address, out _);
        }

        /// <summary>
        /// This machine's IPv4 address on the local network, or empty when there is none.
        /// </summary>
        public static string LocalAddress()
        {
            try
            {
                var address = Dns.GetHostEntry(Dns.GetHostName()).AddressList
                    .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a));
                return address?.ToString() ?? "";
            }
            catch (Exception)
            {
                return "";
            }
        }
    }
}
