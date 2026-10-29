using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace RPGFramework.Networking
{
    /// <summary>
    /// A game found on the local network: who hosts it, how full it is and the code to join it.
    /// </summary>
    public sealed class LanGame
    {
        public string HostName { get; internal set; } = "";

        /// <summary>
        /// What to give the session provider to join (the host's address, with ":port" when not the usual one).
        /// </summary>
        public string JoinCode { get; internal set; } = "";

        public int Players { get; internal set; }
        public int MaxPlayers { get; internal set; }

        /// <summary>
        /// Friends still wait in the host's lobby (the match has not started).
        /// </summary>
        public bool InLobby { get; internal set; }

        /// <summary>
        /// Seconds since the host last answered.
        /// </summary>
        public float Age { get; internal set; }

        internal IPEndPoint From;

        public override string ToString() => $"{HostName} ({Players}/{MaxPlayers}) {JoinCode}";
    }

    /// <summary>
    /// What a hosting game says about itself when asked (<see cref="LanDiscoveryResponder"/>).
    /// </summary>
    public struct LanGameDescription
    {
        public string HostName;
        public int Players;
        public int MaxPlayers;
        public bool InLobby;

        /// <summary>
        /// The port the game itself listens on.
        /// </summary>
        public int GamePort;
    }

    /// <summary>
    /// The small UDP exchange that finds games on the local network: a player's machine asks everyone on
    /// <see cref="DefaultPort"/> for games of a key (the game and its version), hosts of that game answer.
    /// </summary>
    public static class LanDiscoveryProtocol
    {
        public const int DefaultPort = 47777;

        /// <summary>
        /// The usual game port: a join code leaves it out.
        /// </summary>
        public const int UsualGamePort = 7777;

        private static readonly byte[] Magic = Encoding.ASCII.GetBytes("RPGF-LAN");
        private const byte Version = 1;
        private const byte Query = 1;
        private const byte Answer = 2;
        private const int MaxText = 64;

        internal static byte[] WriteQuery(string key) => Write(Query, writer => writer.Write(Clip(key)));

        internal static byte[] WriteAnswer(string key, LanGameDescription game) => Write(Answer, writer =>
        {
            writer.Write(Clip(key));
            writer.Write(Clip(game.HostName));
            writer.Write(game.Players);
            writer.Write(game.MaxPlayers);
            writer.Write(game.InLobby);
            writer.Write(game.GamePort);
        });

        internal static bool TryReadQuery(byte[] data, out string key)
        {
            key = null;
            var reader = Open(data, Query);
            if (reader == null)
                return false;
            string readKey = null;
            bool ok;
            using (reader)
                ok = TryRead(() => readKey = reader.ReadString());
            key = readKey;
            return ok;
        }

        internal static bool TryReadAnswer(byte[] data, out string key, out LanGameDescription game)
        {
            key = null;
            game = default;
            var reader = Open(data, Answer);
            if (reader == null)
                return false;
            string readKey = null;
            var read = new LanGameDescription();
            bool ok;
            using (reader)
            {
                ok = TryRead(() =>
                {
                    readKey = reader.ReadString();
                    read.HostName = reader.ReadString();
                    read.Players = reader.ReadInt32();
                    read.MaxPlayers = reader.ReadInt32();
                    read.InLobby = reader.ReadBoolean();
                    read.GamePort = reader.ReadInt32();
                });
            }
            if (!ok || read.GamePort <= 0 || read.GamePort > 65535)
                return false;
            key = readKey;
            game = read;
            return true;
        }

        /// <summary>
        /// The join code of a game at this address and port.
        /// </summary>
        public static string JoinCodeOf(IPAddress address, int gamePort) =>
            gamePort == UsualGamePort ? address.ToString() : $"{address}:{gamePort}";

        private static string Clip(string text)
        {
            text = text ?? "";
            return text.Length > MaxText ? text.Substring(0, MaxText) : text;
        }

        private static byte[] Write(byte kind, Action<BinaryWriter> body)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.UTF8))
            {
                writer.Write(Magic);
                writer.Write(Version);
                writer.Write(kind);
                body(writer);
                writer.Flush();
                return stream.ToArray();
            }
        }

        private static BinaryReader Open(byte[] data, byte kind)
        {
            if (data == null || data.Length < Magic.Length + 2 || data.Length > 1024)
                return null;
            for (int i = 0; i < Magic.Length; i++)
            {
                if (data[i] != Magic[i])
                    return null;
            }
            if (data[Magic.Length] != Version || data[Magic.Length + 1] != kind)
                return null;
            var reader = new BinaryReader(new MemoryStream(data, Magic.Length + 2, data.Length - Magic.Length - 2), Encoding.UTF8);
            return reader;
        }

        private static bool TryRead(Action read)
        {
            try
            {
                read();
                return true;
            }
            catch (Exception e) when (e is EndOfStreamException || e is IOException || e is FormatException || e is ArgumentException)
            {
                return false;
            }
        }

        internal static UdpClient Bind(int port)
        {
            var client = new UdpClient(AddressFamily.InterNetwork);
            // Several games on one machine may each answer
            client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            client.Client.Bind(new IPEndPoint(IPAddress.Any, port));
            client.EnableBroadcast = true;
            return client;
        }

        internal static IEnumerable<(byte[] Data, IPEndPoint From)> Drain(UdpClient client)
        {
            var received = new List<(byte[], IPEndPoint)>();
            try
            {
                while (client.Available > 0)
                {
                    var from = new IPEndPoint(IPAddress.Any, 0);
                    var data = client.Receive(ref from);
                    received.Add((data, from));
                }
            }
            catch (SocketException)
            {
                // A reply to a closed port, a network that went away: nothing more to read now
            }
            catch (ObjectDisposedException)
            {
            }
            return received;
        }
    }

    /// <summary>
    /// A hosting game's side: answers players on the local network who look for games of its key. Call
    /// <see cref="Poll"/> every frame while hosting.
    /// </summary>
    public sealed class LanDiscoveryResponder : IDisposable
    {
        private readonly string _key;
        private readonly Func<LanGameDescription> _describe;
        private UdpClient _socket;

        /// <param name="key">The game and its version: only players of the same look for it.</param>
        /// <param name="describe">What to say about the game now (asked for each answer).</param>
        public LanDiscoveryResponder(string key, Func<LanGameDescription> describe, int port = LanDiscoveryProtocol.DefaultPort)
        {
            _key = key ?? "";
            _describe = describe ?? throw new ArgumentNullException(nameof(describe));
            Port = port;
        }

        public int Port { get; }

        public bool IsListening => _socket != null;

        /// <summary>
        /// Queries answered so far.
        /// </summary>
        public int Answered { get; private set; }

        /// <returns>False when the port could not be opened (another program has it without sharing).</returns>
        public bool Start()
        {
            if (_socket != null)
                return true;
            try
            {
                _socket = LanDiscoveryProtocol.Bind(Port);
                return true;
            }
            catch (SocketException)
            {
                _socket = null;
                return false;
            }
        }

        public void Poll()
        {
            if (_socket == null)
                return;
            foreach (var (data, from) in LanDiscoveryProtocol.Drain(_socket))
            {
                if (!LanDiscoveryProtocol.TryReadQuery(data, out string key) || key != _key)
                    continue;
                var answer = LanDiscoveryProtocol.WriteAnswer(_key, _describe());
                try
                {
                    _socket.Send(answer, answer.Length, from);
                    Answered++;
                }
                catch (SocketException)
                {
                }
            }
        }

        public void Dispose()
        {
            _socket?.Dispose();
            _socket = null;
        }
    }

    /// <summary>
    /// A player's side: asks the local network for games of a key and lists those that answer, so friends pick a game
    /// instead of typing an address. <see cref="Search"/>, then <see cref="Poll"/> every frame; <see cref="Games"/> fills.
    /// Hosts that stop answering drop out after <see cref="ForgetAfter"/> seconds.
    /// </summary>
    public sealed class LanDiscovery : IDisposable
    {
        private readonly string _key;
        private readonly List<LanGame> _games = new List<LanGame>();
        private UdpClient _socket;

        /// <param name="key">The game and its version, the same the hosts use.</param>
        /// <param name="port">The port hosts answer on.</param>
        public LanDiscovery(string key, int port = LanDiscoveryProtocol.DefaultPort)
        {
            _key = key ?? "";
            Port = port;
        }

        public int Port { get; }

        public IReadOnlyList<LanGame> Games => _games;

        public float ForgetAfter { get; set; } = 6f;

        /// <summary>
        /// A game answered for the first time.
        /// </summary>
        public event Action<LanGame> Found;

        /// <summary>
        /// Asks every machine on the local network (and these addresses too, such as a VPN friend's) for games.
        /// </summary>
        /// <returns>False when this machine cannot send (no network).</returns>
        public bool Search(IEnumerable<IPAddress> alsoAsk = null)
        {
            try
            {
                if (_socket == null)
                    _socket = LanDiscoveryProtocol.Bind(0);
                var query = LanDiscoveryProtocol.WriteQuery(_key);
                var targets = new List<IPAddress> { IPAddress.Broadcast };
                if (alsoAsk != null)
                    targets.AddRange(alsoAsk.Where(a => a != null));
                bool sent = false;
                foreach (var address in targets.Distinct())
                {
                    try
                    {
                        _socket.Send(query, query.Length, new IPEndPoint(address, Port));
                        sent = true;
                    }
                    catch (SocketException)
                    {
                        // No route to that one (a broadcast without a network): the others may still work
                    }
                }
                return sent;
            }
            catch (SocketException)
            {
                return false;
            }
        }

        public void Poll(float deltaTime = 0f)
        {
            foreach (var game in _games)
                game.Age += deltaTime;
            _games.RemoveAll(g => g.Age > ForgetAfter);
            if (_socket == null)
                return;
            foreach (var (data, from) in LanDiscoveryProtocol.Drain(_socket))
            {
                if (!LanDiscoveryProtocol.TryReadAnswer(data, out string key, out var description) || key != _key)
                    continue;
                var endpoint = new IPEndPoint(from.Address, description.GamePort);
                var game = _games.FirstOrDefault(g => g.From.Equals(endpoint));
                bool isNew = game == null;
                if (isNew)
                {
                    game = new LanGame { From = endpoint, JoinCode = LanDiscoveryProtocol.JoinCodeOf(from.Address, description.GamePort) };
                    _games.Add(game);
                }
                game.HostName = description.HostName ?? "";
                game.Players = description.Players;
                game.MaxPlayers = description.MaxPlayers;
                game.InLobby = description.InLobby;
                game.Age = 0f;
                if (isNew)
                    Found?.Invoke(game);
            }
        }

        public void Clear() => _games.Clear();

        public void Dispose()
        {
            _socket?.Dispose();
            _socket = null;
        }
    }
}
