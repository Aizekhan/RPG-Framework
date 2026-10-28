using System;
using System.Collections.Generic;
using System.IO;
using RPGFramework.Logging;
using RPGFramework.Networking;

namespace RPGFramework.Turns
{
    /// <summary>
    /// Message ids and settings of the turn session. Host and players need the same <see cref="Version"/>.
    /// </summary>
    public static class TurnProtocol
    {
        public const int HelloId = 1300;
        public const int WelcomeId = 1301;
        public const int LobbyId = 1302;
        public const int ReadyId = 1303;
        public const int StartedId = 1304;
        public const int IntentId = 1305;
        public const int AnswerId = 1306;
        public const int ViewId = 1307;
        public const int NoticeId = 1308;
        public const int PromptId = 1309;
        public const int PromptClosedId = 1310;
        public const int ReplyId = 1311;

        public const int Version = 1;
        public const int DefaultPort = 7788;
        public const int DefaultMaxSeats = 6;
        public const float ConnectionTimeout = 10f;

        /// <summary>
        /// Seconds an away player may take to come back to a match and keep their seat.
        /// </summary>
        public const float RejoinWindow = 300f;

        public const int MaxEntries = 4096;

        public static NetworkSerializer CreateSerializer()
        {
            var serializer = new NetworkSerializer();
            serializer.RegisterMessageType<TurnHelloMessage>(HelloId);
            serializer.RegisterMessageType<TurnWelcomeMessage>(WelcomeId);
            serializer.RegisterMessageType<TurnLobbyMessage>(LobbyId);
            serializer.RegisterMessageType<TurnReadyMessage>(ReadyId);
            serializer.RegisterMessageType<TurnStartedMessage>(StartedId);
            serializer.RegisterMessageType<TurnIntentMessage>(IntentId);
            serializer.RegisterMessageType<TurnAnswerMessage>(AnswerId);
            serializer.RegisterMessageType<TurnViewMessage>(ViewId);
            serializer.RegisterMessageType<TurnNoticeMessage>(NoticeId);
            serializer.RegisterMessageType<TurnPromptMessage>(PromptId);
            serializer.RegisterMessageType<TurnPromptClosedMessage>(PromptClosedId);
            serializer.RegisterMessageType<TurnReplyMessage>(ReplyId);
            return serializer;
        }

        public static NetworkAuthority CreateAuthority() => NetworkAuthority.CreateDefault()
            .Set(HelloId, MessageAuthority.Client)
            .Set(ReadyId, MessageAuthority.Client)
            .Set(IntentId, MessageAuthority.Client)
            .Set(AnswerId, MessageAuthority.Client)
            .Set(WelcomeId, MessageAuthority.Server)
            .Set(LobbyId, MessageAuthority.Server)
            .Set(StartedId, MessageAuthority.Server)
            .Set(ViewId, MessageAuthority.Server)
            .Set(NoticeId, MessageAuthority.Server)
            .Set(PromptId, MessageAuthority.Server)
            .Set(PromptClosedId, MessageAuthority.Server)
            .Set(ReplyId, MessageAuthority.Server);

        /// <summary>
        /// A network for a turn session over any transport (LAN, a relay, <c>LoopbackNetwork</c> in tests), for the
        /// host and for every player.
        /// </summary>
        public static NetworkManager CreateNetwork(INetworkTransport transport, ILogger logger = null,
            float connectionTimeout = ConnectionTimeout)
        {
            return new NetworkManager(transport, CreateSerializer(),
                new NetworkConfig { PingInterval = 1f, ConnectionTimeout = connectionTimeout, MaxBadMessages = 20 }, logger)
            {
                Authority = CreateAuthority()
            };
        }

        internal static int ReadCount(BinaryReader reader)
        {
            int count = reader.ReadInt32();
            if (count < 0 || count > MaxEntries)
                throw new InvalidDataException($"A message says it carries {count} entries");
            return count;
        }

        internal static void WriteBytes(BinaryWriter writer, byte[] bytes)
        {
            bytes = bytes ?? Array.Empty<byte>();
            writer.Write(bytes.Length);
            writer.Write(bytes);
        }

        internal static byte[] ReadBytes(BinaryReader reader)
        {
            int length = reader.ReadInt32();
            if (length < 0 || length > 1 << 20)
                throw new InvalidDataException($"A message says it carries {length} bytes");
            var bytes = reader.ReadBytes(length);
            if (bytes.Length != length)
                throw new EndOfStreamException("A message was cut short");
            return bytes;
        }
    }

    public sealed class TurnHelloMessage : NetworkMessage, INetworkPayload
    {
        public override int MessageTypeId => TurnProtocol.HelloId;
        public int Version = TurnProtocol.Version;

        /// <summary>
        /// The game and its rules version; the host refuses players of another.
        /// </summary>
        public string Fingerprint = "";

        public string Name = "";
        public string RejoinToken = "";

        public void Write(BinaryWriter writer)
        {
            writer.Write(Version);
            writer.Write(Fingerprint ?? "");
            writer.Write(Name ?? "");
            writer.Write(RejoinToken ?? "");
        }

        public void Read(BinaryReader reader)
        {
            Version = reader.ReadInt32();
            Fingerprint = reader.ReadString();
            Name = reader.ReadString();
            RejoinToken = reader.ReadString();
        }
    }

    public sealed class TurnWelcomeMessage : NetworkMessage, INetworkPayload
    {
        public override int MessageTypeId => TurnProtocol.WelcomeId;
        public int Seat = -1;
        public string RejoinToken = "";

        /// <summary>
        /// Why the player was not let in; empty when they were.
        /// </summary>
        public string Refusal = "";

        public void Write(BinaryWriter writer)
        {
            writer.Write(Seat);
            writer.Write(RejoinToken ?? "");
            writer.Write(Refusal ?? "");
        }

        public void Read(BinaryReader reader)
        {
            Seat = reader.ReadInt32();
            RejoinToken = reader.ReadString();
            Refusal = reader.ReadString();
        }
    }

    public sealed class TurnLobbyMessage : NetworkMessage, INetworkPayload
    {
        public override int MessageTypeId => TurnProtocol.LobbyId;
        public List<TurnSeat> Seats = new List<TurnSeat>();

        public void Write(BinaryWriter writer)
        {
            writer.Write(Seats.Count);
            foreach (var seat in Seats)
            {
                writer.Write(seat.Seat);
                writer.Write(seat.Name ?? "");
                writer.Write(seat.Ready);
                writer.Write(seat.Connected);
            }
        }

        public void Read(BinaryReader reader)
        {
            int count = TurnProtocol.ReadCount(reader);
            Seats = new List<TurnSeat>(count);
            for (int i = 0; i < count; i++)
            {
                Seats.Add(new TurnSeat
                {
                    Seat = reader.ReadInt32(), Name = reader.ReadString(), Ready = reader.ReadBoolean(), Connected = reader.ReadBoolean()
                });
            }
        }
    }

    public sealed class TurnReadyMessage : NetworkMessage, INetworkPayload
    {
        public override int MessageTypeId => TurnProtocol.ReadyId;
        public bool Ready;

        public void Write(BinaryWriter writer) => writer.Write(Ready);
        public void Read(BinaryReader reader) => Ready = reader.ReadBoolean();
    }

    public sealed class TurnStartedMessage : NetworkMessage, INetworkPayload
    {
        public override int MessageTypeId => TurnProtocol.StartedId;

        /// <summary>
        /// The match options the host chose (the game's bytes).
        /// </summary>
        public byte[] Options = Array.Empty<byte>();

        public void Write(BinaryWriter writer) => TurnProtocol.WriteBytes(writer, Options);
        public void Read(BinaryReader reader) => Options = TurnProtocol.ReadBytes(reader);
    }

    public sealed class TurnIntentMessage : NetworkMessage, INetworkPayload
    {
        public override int MessageTypeId => TurnProtocol.IntentId;
        public int RequestId;
        public TurnIntent Intent = new TurnIntent("");

        public void Write(BinaryWriter writer)
        {
            writer.Write(RequestId);
            (Intent ?? new TurnIntent("")).Write(writer);
        }

        public void Read(BinaryReader reader)
        {
            RequestId = reader.ReadInt32();
            Intent = TurnIntent.Read(reader);
        }
    }

    public sealed class TurnReplyMessage : NetworkMessage, INetworkPayload
    {
        public override int MessageTypeId => TurnProtocol.ReplyId;
        public int RequestId;
        public bool Accepted;
        public string Reason = "";

        public void Write(BinaryWriter writer)
        {
            writer.Write(RequestId);
            writer.Write(Accepted);
            writer.Write(Reason ?? "");
        }

        public void Read(BinaryReader reader)
        {
            RequestId = reader.ReadInt32();
            Accepted = reader.ReadBoolean();
            Reason = reader.ReadString();
        }
    }

    public sealed class TurnAnswerMessage : NetworkMessage, INetworkPayload
    {
        public override int MessageTypeId => TurnProtocol.AnswerId;
        public int WindowId;
        public int Option = TurnPrompt.Pass;

        public void Write(BinaryWriter writer)
        {
            writer.Write(WindowId);
            writer.Write(Option);
        }

        public void Read(BinaryReader reader)
        {
            WindowId = reader.ReadInt32();
            Option = reader.ReadInt32();
        }
    }

    public sealed class TurnViewMessage : NetworkMessage, INetworkPayload
    {
        public override int MessageTypeId => TurnProtocol.ViewId;

        /// <summary>
        /// Counts up with every view the host sends, so an old one arriving late is dropped.
        /// </summary>
        public int Number;

        public byte[] View = Array.Empty<byte>();

        public void Write(BinaryWriter writer)
        {
            writer.Write(Number);
            TurnProtocol.WriteBytes(writer, View);
        }

        public void Read(BinaryReader reader)
        {
            Number = reader.ReadInt32();
            View = TurnProtocol.ReadBytes(reader);
        }
    }

    public sealed class TurnNoticeMessage : NetworkMessage, INetworkPayload
    {
        public override int MessageTypeId => TurnProtocol.NoticeId;
        public string Kind = "";
        public byte[] Data = Array.Empty<byte>();

        public void Write(BinaryWriter writer)
        {
            writer.Write(Kind ?? "");
            TurnProtocol.WriteBytes(writer, Data);
        }

        public void Read(BinaryReader reader)
        {
            Kind = reader.ReadString();
            Data = TurnProtocol.ReadBytes(reader);
        }
    }

    public sealed class TurnPromptMessage : NetworkMessage, INetworkPayload
    {
        public override int MessageTypeId => TurnProtocol.PromptId;
        public int WindowId;
        public string Kind = "";
        public List<string> Options = new List<string>();
        public float Seconds;
        public byte[] Data = Array.Empty<byte>();

        public void Write(BinaryWriter writer)
        {
            writer.Write(WindowId);
            writer.Write(Kind ?? "");
            writer.Write(Options.Count);
            foreach (string option in Options)
                writer.Write(option ?? "");
            writer.Write(Seconds);
            TurnProtocol.WriteBytes(writer, Data);
        }

        public void Read(BinaryReader reader)
        {
            WindowId = reader.ReadInt32();
            Kind = reader.ReadString();
            int count = TurnProtocol.ReadCount(reader);
            Options = new List<string>(count);
            for (int i = 0; i < count; i++)
                Options.Add(reader.ReadString());
            Seconds = reader.ReadSingle();
            Data = TurnProtocol.ReadBytes(reader);
        }
    }

    public sealed class TurnPromptClosedMessage : NetworkMessage, INetworkPayload
    {
        public override int MessageTypeId => TurnProtocol.PromptClosedId;
        public int WindowId;

        public void Write(BinaryWriter writer) => writer.Write(WindowId);
        public void Read(BinaryReader reader) => WindowId = reader.ReadInt32();
    }
}
