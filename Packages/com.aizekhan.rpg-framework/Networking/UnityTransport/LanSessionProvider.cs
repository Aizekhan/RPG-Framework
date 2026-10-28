using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
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
            var codes = LocalAddresses().Select(address => port == DefaultPort ? address : $"{address}:{port}").ToList();
            if (codes.Count == 0)
                codes.Add(port == DefaultPort ? "127.0.0.1" : $"127.0.0.1:{port}");
            return UniTask.FromResult(new HostedSession(transport, codes[0], codes.Skip(1).ToList()));
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
        /// This machine's first IPv4 address (<see cref="LocalAddresses"/>), or empty when there is none.
        /// </summary>
        public static string LocalAddress() => LocalAddresses().FirstOrDefault() ?? "";

        /// <summary>
        /// Every IPv4 address friends may reach this machine on: the home network first (192.168.x, 10.x,
        /// 172.16-31.x), then VPNs (Tailscale's 100.64-127.x, Radmin's 26.x, ZeroTier) and anything else. Loopback and
        /// self-assigned (169.254.x) addresses and interfaces that are down are left out.
        /// </summary>
        public static List<string> LocalAddresses()
        {
            var found = new List<IPAddress>();
            try
            {
                foreach (var network in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (network.OperationalStatus != OperationalStatus.Up || network.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                        continue;
                    foreach (var unicast in network.GetIPProperties().UnicastAddresses)
                        found.Add(unicast.Address);
                }
            }
            catch (Exception)
            {
                // Some platforms do not list interfaces; the host name lookup below still works there
            }
            if (found.Count == 0)
            {
                try
                {
                    found.AddRange(Dns.GetHostEntry(Dns.GetHostName()).AddressList);
                }
                catch (Exception)
                {
                    return new List<string>();
                }
            }
            return OrderForFriends(found);
        }

        /// <summary>
        /// The usable IPv4 addresses among these, home network first, then VPNs, then the rest; no duplicates.
        /// </summary>
        public static List<string> OrderForFriends(IEnumerable<IPAddress> addresses) =>
            addresses.Where(a => a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a) && !IsSelfAssigned(a))
                .Select(a => a.ToString()).Distinct()
                .Select((text, index) => (Text: text, Rank: Rank(IPAddress.Parse(text)), Index: index))
                .OrderBy(a => a.Rank).ThenBy(a => a.Index)
                .Select(a => a.Text).ToList();

        private static bool IsSelfAssigned(IPAddress address)
        {
            var bytes = address.GetAddressBytes();
            return bytes[0] == 169 && bytes[1] == 254;
        }

        // 0: home network, 1: a VPN's range, 2: anything else
        private static int Rank(IPAddress address)
        {
            var b = address.GetAddressBytes();
            if (b[0] == 192 && b[1] == 168 || b[0] == 10 || b[0] == 172 && b[1] >= 16 && b[1] <= 31)
                return 0;
            if (b[0] == 100 && b[1] >= 64 && b[1] <= 127 || b[0] == 26 || b[0] == 25)
                return 1;
            return 2;
        }
    }
}
