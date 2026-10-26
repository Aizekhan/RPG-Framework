using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using RPGFramework.Logging;
using RPGFramework.Networking;

namespace RPGFramework.ActionRpg.Coop
{
    /// <summary>
    /// Where a character stands and faces, on the ground plane (yaw in degrees).
    /// </summary>
    public struct ActorPose
    {
        public float X;
        public float Y;
        public float Z;
        public float Yaw;

        public ActorPose(float x, float y, float z, float yaw)
        {
            X = x;
            Y = y;
            Z = z;
            Yaw = yaw;
        }
    }

    /// <summary>
    /// The co-op protocol of the action RPG: message ids (1100-1199 are taken), who may send what, the send rate
    /// and the fingerprint that keeps different versions of a game apart.
    /// </summary>
    public static class CoopProtocol
    {
        public const int HelloId = 1100;
        public const int WelcomeId = 1101;
        public const int WorldId = 1102;
        public const int MotionId = 1103;
        public const int IntentId = 1104;

        /// <summary>
        /// Changes whenever the messages change, so older and newer builds refuse each other instead of misreading.
        /// </summary>
        public const int Version = 2;

        public const int DefaultPort = 7777;
        public const int DefaultMaxPlayers = 4;

        /// <summary>
        /// The largest party the co-op is tested with (eight players over real UDP).
        /// </summary>
        public const int MaxSupportedPlayers = 8;

        /// <summary>
        /// World and motion updates per second.
        /// </summary>
        public const float SendRate = 20f;

        public static NetworkSerializer CreateSerializer()
        {
            var serializer = new NetworkSerializer();
            serializer.RegisterMessageType<CoopHelloMessage>(HelloId);
            serializer.RegisterMessageType<CoopWelcomeMessage>(WelcomeId);
            serializer.RegisterMessageType<CoopWorldMessage>(WorldId);
            serializer.RegisterMessageType<CoopMotionMessage>(MotionId);
            serializer.RegisterMessageType<CoopIntentMessage>(IntentId);
            return serializer;
        }

        public static NetworkAuthority CreateAuthority() => NetworkAuthority.CreateDefault()
            .Set(HelloId, MessageAuthority.Client)
            .Set(IntentId, MessageAuthority.Client)
            .Set(WelcomeId, MessageAuthority.Server)
            .Set(WorldId, MessageAuthority.Server)
            .Set(MotionId, MessageAuthority.Any);

        public static NetworkManager CreateNetwork(INetworkTransport transport, ILogger logger = null) =>
            new NetworkManager(transport, CreateSerializer(), new NetworkConfig { PingInterval = 1f }, logger)
            {
                Authority = CreateAuthority()
            };

        /// <summary>
        /// A short id of the game's rules and content: host and client must have the same to play together.
        /// </summary>
        public static string Fingerprint(ActionRpgSettings settings, ActionRpgContent content)
        {
            var text = new StringBuilder().Append(Version).Append('|');
            // Invariant numbers: "1.5" must not become "1,5" on a machine with another language
            void Number(double value) => text.Append(value.ToString("R", CultureInfo.InvariantCulture)).Append(',');
            void Character(CharacterData c)
            {
                text.Append(c.Id).Append(',');
                Number(c.MaxHealth);
                Number(c.Attack);
                Number(c.Defense);
                Number(c.HealthPerLevel);
                Number(c.AttackPerLevel);
                Number(c.DefensePerLevel);
                Number(c.ExperienceReward);
                text.Append('|');
            }
            if (settings.Player != null)
                Character(settings.Player);
            foreach (var enemy in settings.Enemies.Where(e => e != null).OrderBy(e => e.Id, StringComparer.Ordinal))
                Character(enemy);
            Number(settings.InventoryCapacity);
            foreach (var quest in settings.StartingQuests.OrderBy(q => q, StringComparer.Ordinal))
                text.Append(quest).Append('|');
            if (content != null)
            {
                foreach (var item in content.Items.Where(i => i != null).OrderBy(i => i.ItemId, StringComparer.Ordinal))
                    text.Append(item.ItemId).Append('|');
                foreach (var quest in content.Quests.Where(q => q != null).OrderBy(q => q.Id, StringComparer.Ordinal))
                    text.Append(quest.Id).Append('|');
            }

            // FNV-1a: stable across runs and machines, unlike string.GetHashCode
            uint hash = 2166136261;
            foreach (char c in text.ToString())
                hash = (hash ^ c) * 16777619;
            return hash.ToString("x8");
        }
    }

    /// <summary>
    /// Client → host: "I want to play", with the client's game fingerprint.
    /// </summary>
    public sealed class CoopHelloMessage : NetworkMessage, INetworkPayload
    {
        public override int MessageTypeId => CoopProtocol.HelloId;
        public string Fingerprint = "";

        /// <summary>
        /// The name the player wants to be seen by.
        /// </summary>
        public string Name = "";

        public void Write(BinaryWriter writer)
        {
            writer.Write(Fingerprint ?? "");
            writer.Write(Name ?? "");
        }

        public void Read(BinaryReader reader)
        {
            Fingerprint = reader.ReadString();
            Name = reader.ReadString();
        }
    }

    /// <summary>
    /// Host → client: accepted with the client's player id, or refused with the reason.
    /// </summary>
    public sealed class CoopWelcomeMessage : NetworkMessage, INetworkPayload
    {
        public override int MessageTypeId => CoopProtocol.WelcomeId;
        public bool Accepted;
        public int PlayerId = -1;
        public string Reason = "";

        public void Write(BinaryWriter writer)
        {
            writer.Write(Accepted);
            writer.Write(PlayerId);
            writer.Write(Reason ?? "");
        }

        public void Read(BinaryReader reader)
        {
            Accepted = reader.ReadBoolean();
            PlayerId = reader.ReadInt32();
            Reason = reader.ReadString();
        }
    }

    /// <summary>
    /// A character's numbers as the host has them.
    /// </summary>
    public struct ActorState
    {
        public int Id;
        public float Health;
        public float MaxHealth;
        public float Attack;
        public float Defense;
        public bool Alive;

        public static ActorState Of(Actor actor) => new ActorState
        {
            Id = actor.EntityId, Health = actor.Health, MaxHealth = actor.MaxHealth, Attack = actor.Attack,
            Defense = actor.Defense, Alive = actor.IsAlive()
        };

        public bool SameAs(ActorState other) =>
            Health == other.Health && MaxHealth == other.MaxHealth && Attack == other.Attack && Defense == other.Defense
            && Alive == other.Alive;
    }

    /// <summary>
    /// What changed in the host's world, in order: characters entering and leaving, hits, numbers, and the party.
    /// </summary>
    public sealed class CoopWorldMessage : NetworkMessage, INetworkPayload
    {
        public enum ChangeKind : byte
        {
            Spawn = 1,
            Despawn = 2,
            Hit = 3
        }

        public struct Change
        {
            public ChangeKind Kind;
            public int Id;
            public string CharacterId;
            public bool IsPlayer;
            public string Name;
            public int Seat;
            public int AttackerId;
            public float Amount;
            public float HealthLeft;
        }

        public override int MessageTypeId => CoopProtocol.WorldId;
        public List<Change> Changes = new List<Change>();
        public List<ActorState> States = new List<ActorState>();

        /// <summary>
        /// The party's progress, sent when it changed (null otherwise).
        /// </summary>
        public PartyStatus Party;

        public bool IsEmpty => Changes.Count == 0 && States.Count == 0 && Party == null;

        public void Write(BinaryWriter writer)
        {
            writer.Write(Changes.Count);
            foreach (var change in Changes)
            {
                writer.Write((byte)change.Kind);
                writer.Write(change.Id);
                switch (change.Kind)
                {
                    case ChangeKind.Spawn:
                        writer.Write(change.CharacterId ?? "");
                        writer.Write(change.IsPlayer);
                        writer.Write(change.Name ?? "");
                        writer.Write(change.Seat);
                        break;
                    case ChangeKind.Hit:
                        writer.Write(change.AttackerId);
                        writer.Write(change.Amount);
                        writer.Write(change.HealthLeft);
                        break;
                }
            }
            writer.Write(States.Count);
            foreach (var state in States)
            {
                writer.Write(state.Id);
                writer.Write(state.Health);
                writer.Write(state.MaxHealth);
                writer.Write(state.Attack);
                writer.Write(state.Defense);
                writer.Write(state.Alive);
            }
            writer.Write(Party != null);
            Party?.Write(writer);
        }

        public void Read(BinaryReader reader)
        {
            int changes = reader.ReadInt32();
            for (int i = 0; i < changes; i++)
            {
                var change = new Change { Kind = (ChangeKind)reader.ReadByte(), Id = reader.ReadInt32() };
                switch (change.Kind)
                {
                    case ChangeKind.Spawn:
                        change.CharacterId = reader.ReadString();
                        change.IsPlayer = reader.ReadBoolean();
                        change.Name = reader.ReadString();
                        change.Seat = reader.ReadInt32();
                        break;
                    case ChangeKind.Hit:
                        change.AttackerId = reader.ReadInt32();
                        change.Amount = reader.ReadSingle();
                        change.HealthLeft = reader.ReadSingle();
                        break;
                }
                Changes.Add(change);
            }
            int states = reader.ReadInt32();
            for (int i = 0; i < states; i++)
            {
                States.Add(new ActorState
                {
                    Id = reader.ReadInt32(), Health = reader.ReadSingle(), MaxHealth = reader.ReadSingle(),
                    Attack = reader.ReadSingle(), Defense = reader.ReadSingle(), Alive = reader.ReadBoolean()
                });
            }
            if (reader.ReadBoolean())
            {
                Party = new PartyStatus();
                Party.Read(reader);
            }
        }
    }

    /// <summary>
    /// The party's shared progress as the HUD shows it.
    /// </summary>
    public sealed class PartyStatus
    {
        public int Level;
        public long TotalExperience;
        public long ExperienceForNextLevel;
        public int Kills;
        public int Wave;
        public List<string> Items = new List<string>();
        public List<QuestRecord> ActiveQuests = new List<QuestRecord>();
        public List<string> CompletedQuests = new List<string>();
        public List<string> Log = new List<string>();

        public static PartyStatus Of(ActorWorld world)
        {
            var status = new PartyStatus
            {
                Level = world.Level, TotalExperience = world.TotalExperience,
                ExperienceForNextLevel = world.ExperienceForNextLevel, Kills = world.Kills, Wave = world.CurrentWave
            };
            status.Items.AddRange(world.Inventory.GetAllItems().Where(i => i != null).Select(i => i.ItemId));
            foreach (var (quest, progress) in world.ActiveQuests)
            {
                var record = new QuestRecord { QuestId = quest.Id };
                record.Objectives.AddRange(progress.ObjectiveProgress.Select(p => new ObjectiveRecord { ObjectiveId = p.Key, Progress = p.Value }));
                status.ActiveQuests.Add(record);
            }
            status.CompletedQuests.AddRange(world.CompletedQuests);
            status.Log.AddRange(world.Log);
            return status;
        }

        public byte[] ToBytes()
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                Write(writer);
                return stream.ToArray();
            }
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write(Level);
            writer.Write(TotalExperience);
            writer.Write(ExperienceForNextLevel);
            writer.Write(Kills);
            writer.Write(Wave);
            WriteStrings(writer, Items);
            writer.Write(ActiveQuests.Count);
            foreach (var quest in ActiveQuests)
            {
                writer.Write(quest.QuestId ?? "");
                writer.Write(quest.Objectives.Count);
                foreach (var objective in quest.Objectives)
                {
                    writer.Write(objective.ObjectiveId ?? "");
                    writer.Write(objective.Progress);
                }
            }
            WriteStrings(writer, CompletedQuests);
            WriteStrings(writer, Log);
        }

        public void Read(BinaryReader reader)
        {
            Level = reader.ReadInt32();
            TotalExperience = reader.ReadInt64();
            ExperienceForNextLevel = reader.ReadInt64();
            Kills = reader.ReadInt32();
            Wave = reader.ReadInt32();
            Items = ReadStrings(reader);
            int quests = reader.ReadInt32();
            ActiveQuests = new List<QuestRecord>(quests);
            for (int i = 0; i < quests; i++)
            {
                var record = new QuestRecord { QuestId = reader.ReadString() };
                int objectives = reader.ReadInt32();
                for (int o = 0; o < objectives; o++)
                    record.Objectives.Add(new ObjectiveRecord { ObjectiveId = reader.ReadString(), Progress = reader.ReadInt32() });
                ActiveQuests.Add(record);
            }
            CompletedQuests = ReadStrings(reader);
            Log = ReadStrings(reader);
        }

        private static void WriteStrings(BinaryWriter writer, List<string> values)
        {
            writer.Write(values.Count);
            foreach (var value in values)
                writer.Write(value ?? "");
        }

        private static List<string> ReadStrings(BinaryReader reader)
        {
            int count = reader.ReadInt32();
            var values = new List<string>(count);
            for (int i = 0; i < count; i++)
                values.Add(reader.ReadString());
            return values;
        }
    }

    /// <summary>
    /// Where characters are. Host → client: everyone; client → host: its own player. Sent unreliably.
    /// </summary>
    public sealed class CoopMotionMessage : NetworkMessage, INetworkPayload
    {
        public override int MessageTypeId => CoopProtocol.MotionId;
        public List<(int Id, ActorPose Pose)> Poses = new List<(int, ActorPose)>();

        public void Write(BinaryWriter writer)
        {
            writer.Write(Poses.Count);
            foreach (var (id, pose) in Poses)
            {
                writer.Write(id);
                writer.Write(pose.X);
                writer.Write(pose.Y);
                writer.Write(pose.Z);
                writer.Write(pose.Yaw);
            }
        }

        public void Read(BinaryReader reader)
        {
            int count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
                Poses.Add((reader.ReadInt32(), new ActorPose(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle())));
        }
    }

    /// <summary>
    /// Client → host: what the client's player wants to do; the host decides.
    /// </summary>
    public sealed class CoopIntentMessage : NetworkMessage, INetworkPayload
    {
        public enum IntentKind : byte
        {
            Attack = 1,
            Revive = 2
        }

        public override int MessageTypeId => CoopProtocol.IntentId;
        public IntentKind Kind;
        public int TargetId = -1;

        public void Write(BinaryWriter writer)
        {
            writer.Write((byte)Kind);
            writer.Write(TargetId);
        }

        public void Read(BinaryReader reader)
        {
            Kind = (IntentKind)reader.ReadByte();
            TargetId = reader.ReadInt32();
        }
    }
}
