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

        /// <summary>
        /// Every number is a real number (a broken or hostile message can carry NaN or infinity).
        /// </summary>
        public bool IsFinite => Finite(X) && Finite(Y) && Finite(Z) && Finite(Yaw);

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
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
        public const int CorrectionId = 1105;
        public const int IncomingAttackId = 1106;
        public const int DefenseResultId = 1107;

        /// <summary>
        /// The first id a game may give its own messages (<see cref="CoopGameMessages"/>).
        /// </summary>
        public const int FirstGameMessageId = 1200;

        /// <summary>
        /// Changes whenever the messages change, so older and newer builds refuse each other instead of misreading.
        /// </summary>
        public const int Version = 7;

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

        /// <summary>
        /// Seconds without a word from the other side before the link counts as lost (a crash, a pulled cable).
        /// Both sides ping every second, so a quiet game is not mistaken for a lost one.
        /// </summary>
        public const float ConnectionTimeout = 10f;

        /// <summary>
        /// Seconds without a word after which the link counts as struggling (the panel says so).
        /// </summary>
        public const float UnstableAfter = 2f;

        /// <summary>
        /// Seconds the host keeps the seat of a player whose link was lost, so they come back as themselves.
        /// </summary>
        public const float RejoinWindow = 120f;

        /// <summary>
        /// Most entries a message may say it carries (characters, poses, items); more means it is broken.
        /// </summary>
        public const int MaxEntries = 4096;

        public static NetworkSerializer CreateSerializer()
        {
            var serializer = new NetworkSerializer();
            serializer.RegisterMessageType<CoopHelloMessage>(HelloId);
            serializer.RegisterMessageType<CoopWelcomeMessage>(WelcomeId);
            serializer.RegisterMessageType<CoopWorldMessage>(WorldId);
            serializer.RegisterMessageType<CoopMotionMessage>(MotionId);
            serializer.RegisterMessageType<CoopIntentMessage>(IntentId);
            serializer.RegisterMessageType<CoopCorrectionMessage>(CorrectionId);
            serializer.RegisterMessageType<CoopIncomingAttackMessage>(IncomingAttackId);
            serializer.RegisterMessageType<CoopDefenseResultMessage>(DefenseResultId);
            return serializer;
        }

        public static NetworkAuthority CreateAuthority() => NetworkAuthority.CreateDefault()
            .Set(HelloId, MessageAuthority.Client)
            .Set(IntentId, MessageAuthority.Client)
            .Set(WelcomeId, MessageAuthority.Server)
            .Set(WorldId, MessageAuthority.Server)
            .Set(CorrectionId, MessageAuthority.Server)
            .Set(IncomingAttackId, MessageAuthority.Server)
            .Set(DefenseResultId, MessageAuthority.Client)
            .Set(MotionId, MessageAuthority.Any);

        /// <param name="connectionTimeout">Seconds of silence before the link counts as lost (tests use less).</param>
        /// <param name="gameMessages">The game's own messages; the same set on the host and every client.</param>
        public static NetworkManager CreateNetwork(INetworkTransport transport, ILogger logger = null,
            float connectionTimeout = ConnectionTimeout, CoopGameMessages gameMessages = null)
        {
            var serializer = CreateSerializer();
            var authority = CreateAuthority();
            gameMessages?.RegisterOn(serializer, authority);
            return new NetworkManager(transport, serializer,
                new NetworkConfig { PingInterval = 1f, ConnectionTimeout = connectionTimeout, MaxBadMessages = 20 }, logger)
            {
                Authority = authority
            };
        }

        /// <summary>
        /// Reads how many entries follow, refusing a count no real message has.
        /// </summary>
        internal static int ReadCount(BinaryReader reader)
        {
            int count = reader.ReadInt32();
            if (count < 0 || count > MaxEntries)
                throw new InvalidDataException($"A message says it carries {count} entries");
            return count;
        }

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
                Number(c.MoveSpeed);
                text.Append('|');
            }
            if (settings.Player != null)
                Character(settings.Player);
            foreach (var enemy in settings.Enemies.Where(e => e != null).OrderBy(e => e.Id, StringComparer.Ordinal))
                Character(enemy);
            Number(settings.InventoryCapacity);
            if (settings.Pvp != null)
            {
                var pvp = settings.Pvp;
                text.Append("pvp:").Append((int)pvp.Mode).Append(',');
                Number(pvp.ScoreToWin);
                Number(pvp.RoundsToWin);
                foreach (var team in pvp.Teams)
                    text.Append(team).Append(',');
                text.Append('|');
            }
            foreach (var quest in settings.StartingQuests.OrderBy(q => q, StringComparer.Ordinal))
                text.Append(quest).Append('|');
            if (content != null)
            {
                foreach (var item in content.Items.Where(i => i != null).OrderBy(i => i.ItemId, StringComparer.Ordinal))
                    text.Append(item.ItemId).Append('|');
                foreach (var quest in content.Quests.Where(q => q != null).OrderBy(q => q.Id, StringComparer.Ordinal))
                    text.Append(quest.Id).Append('|');
                foreach (var effect in content.StatusEffects.Where(e => e != null).OrderBy(e => e.Id, StringComparer.Ordinal))
                    text.Append("fx:").Append(effect.Id).Append('|');
                foreach (var ability in content.Abilities.Where(e => e != null).OrderBy(e => e.AbilityId, StringComparer.Ordinal))
                    text.Append("ab:").Append(ability.AbilityId).Append('|');
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

        /// <summary>
        /// From an earlier welcome by this host, when coming back after the link was lost; empty otherwise.
        /// </summary>
        public string RejoinToken = "";

        public void Write(BinaryWriter writer)
        {
            writer.Write(Fingerprint ?? "");
            writer.Write(Name ?? "");
            writer.Write(RejoinToken ?? "");
        }

        public void Read(BinaryReader reader)
        {
            Fingerprint = reader.ReadString();
            Name = reader.ReadString();
            RejoinToken = reader.ReadString();
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

        /// <summary>
        /// Lets the player back into their seat if the link is lost (<see cref="CoopHelloMessage.RejoinToken"/>).
        /// </summary>
        public string RejoinToken = "";

        /// <summary>
        /// The player came back into the seat they had.
        /// </summary>
        public bool Rejoined;

        public void Write(BinaryWriter writer)
        {
            writer.Write(Accepted);
            writer.Write(PlayerId);
            writer.Write(Reason ?? "");
            writer.Write(RejoinToken ?? "");
            writer.Write(Rejoined);
        }

        public void Read(BinaryReader reader)
        {
            Accepted = reader.ReadBoolean();
            PlayerId = reader.ReadInt32();
            Reason = reader.ReadString();
            RejoinToken = reader.ReadString();
            Rejoined = reader.ReadBoolean();
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

        /// <summary>
        /// Its PvP side; null for none.
        /// </summary>
        public string Team;

        /// <summary>
        /// <see cref="Actor.MoveMultiplier"/> from status effects.
        /// </summary>
        public float MoveMultiplier;

        /// <summary>
        /// <see cref="Actor.Controls"/>, sorted and joined by commas; empty for none.
        /// </summary>
        public string Controls;

        public static ActorState Of(Actor actor) => new ActorState
        {
            Id = actor.EntityId, Health = actor.Health, MaxHealth = actor.MaxHealth, Attack = actor.Attack,
            Defense = actor.Defense, Alive = actor.IsAlive(), Team = actor.Team, MoveMultiplier = actor.MoveMultiplier,
            Controls = actor.Controls.Count == 0 ? "" : string.Join(",", actor.Controls.OrderBy(c => c, StringComparer.Ordinal))
        };

        public bool SameAs(ActorState other) =>
            Health == other.Health && MaxHealth == other.MaxHealth && Attack == other.Attack && Defense == other.Defense
            && Alive == other.Alive && Team == other.Team && MoveMultiplier == other.MoveMultiplier
            && (Controls ?? "") == (other.Controls ?? "");
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
            Hit = 3,

            /// <summary>
            /// A status effect went on a character or changed its stacks (Name = effect id).
            /// </summary>
            EffectOn = 4,

            /// <summary>
            /// A status effect came off a character (Name = effect id).
            /// </summary>
            EffectOff = 5,

            /// <summary>
            /// A character's ability went on cooldown (Name = ability id, Amount = seconds).
            /// </summary>
            Cooldown = 6
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

            /// <summary>
            /// The hit's <see cref="RPGFramework.Combat.AttackContext"/>: tags, source, effect damage.
            /// </summary>
            public int Tags;
            public int SourceId;
            public bool FromEffect;

            /// <summary>
            /// Stacks of an effect.
            /// </summary>
            public int Stacks;
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
                        writer.Write(change.Tags);
                        writer.Write(change.SourceId);
                        writer.Write(change.FromEffect);
                        break;
                    case ChangeKind.EffectOn:
                        writer.Write(change.Name ?? "");
                        writer.Write(change.Stacks);
                        writer.Write(change.AttackerId);
                        writer.Write(change.Amount);
                        break;
                    case ChangeKind.EffectOff:
                        writer.Write(change.Name ?? "");
                        break;
                    case ChangeKind.Cooldown:
                        writer.Write(change.Name ?? "");
                        writer.Write(change.Amount);
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
                writer.Write(state.Team ?? "");
                writer.Write(state.MoveMultiplier);
                writer.Write(state.Controls ?? "");
            }
            writer.Write(Party != null);
            Party?.Write(writer);
        }

        public void Read(BinaryReader reader)
        {
            int changes = CoopProtocol.ReadCount(reader);
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
                        change.Tags = reader.ReadInt32();
                        change.SourceId = reader.ReadInt32();
                        change.FromEffect = reader.ReadBoolean();
                        break;
                    case ChangeKind.EffectOn:
                        change.Name = reader.ReadString();
                        change.Stacks = reader.ReadInt32();
                        change.AttackerId = reader.ReadInt32();
                        change.Amount = reader.ReadSingle();
                        break;
                    case ChangeKind.EffectOff:
                        change.Name = reader.ReadString();
                        break;
                    case ChangeKind.Cooldown:
                        change.Name = reader.ReadString();
                        change.Amount = reader.ReadSingle();
                        break;
                }
                Changes.Add(change);
            }
            int states = CoopProtocol.ReadCount(reader);
            for (int i = 0; i < states; i++)
            {
                var state = new ActorState
                {
                    Id = reader.ReadInt32(), Health = reader.ReadSingle(), MaxHealth = reader.ReadSingle(),
                    Attack = reader.ReadSingle(), Defense = reader.ReadSingle(), Alive = reader.ReadBoolean()
                };
                string team = reader.ReadString();
                state.Team = team.Length > 0 ? team : null;
                state.MoveMultiplier = reader.ReadSingle();
                state.Controls = reader.ReadString();
                States.Add(state);
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

        /// <summary>
        /// The PvP match; null when the game is not PvP.
        /// </summary>
        public PvpStatus Pvp;

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
            if (world.Match != null && world.Settings.Pvp != null)
            {
                status.Pvp = new PvpStatus();
                status.Pvp.CopyFrom(world.Match);
            }
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
            writer.Write(Pvp != null);
            Pvp?.Write(writer);
        }

        public void Read(BinaryReader reader)
        {
            Level = reader.ReadInt32();
            TotalExperience = reader.ReadInt64();
            ExperienceForNextLevel = reader.ReadInt64();
            Kills = reader.ReadInt32();
            Wave = reader.ReadInt32();
            Items = ReadStrings(reader);
            int quests = CoopProtocol.ReadCount(reader);
            ActiveQuests = new List<QuestRecord>(quests);
            for (int i = 0; i < quests; i++)
            {
                var record = new QuestRecord { QuestId = reader.ReadString() };
                int objectives = CoopProtocol.ReadCount(reader);
                for (int o = 0; o < objectives; o++)
                    record.Objectives.Add(new ObjectiveRecord { ObjectiveId = reader.ReadString(), Progress = reader.ReadInt32() });
                ActiveQuests.Add(record);
            }
            CompletedQuests = ReadStrings(reader);
            Log = ReadStrings(reader);
            Pvp = null;
            if (reader.ReadBoolean())
            {
                Pvp = new PvpStatus();
                Pvp.Read(reader, CoopProtocol.ReadCount);
            }
        }

        private static void WriteStrings(BinaryWriter writer, List<string> values)
        {
            writer.Write(values.Count);
            foreach (var value in values)
                writer.Write(value ?? "");
        }

        private static List<string> ReadStrings(BinaryReader reader)
        {
            int count = CoopProtocol.ReadCount(reader);
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
            int count = CoopProtocol.ReadCount(reader);
            for (int i = 0; i < count; i++)
                Poses.Add((reader.ReadInt32(), new ActorPose(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle())));
        }
    }

    /// <summary>
    /// Host → client: your player is here now (they moved faster than their character can, or the game put them
    /// at a spawn point). The client puts its player there at once.
    /// </summary>
    public sealed class CoopCorrectionMessage : NetworkMessage, INetworkPayload
    {
        public override int MessageTypeId => CoopProtocol.CorrectionId;
        public ActorPose Pose;

        public void Write(BinaryWriter writer)
        {
            writer.Write(Pose.X);
            writer.Write(Pose.Y);
            writer.Write(Pose.Z);
            writer.Write(Pose.Yaw);
        }

        public void Read(BinaryReader reader)
        {
            Pose = new ActorPose(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
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
            Revive = 2,

            /// <summary>
            /// The player leaves on purpose: no seat is kept for them.
            /// </summary>
            Leave = 3,

            /// <summary>
            /// The player uses an ability (<see cref="AbilityId"/>) on <see cref="TargetId"/>.
            /// </summary>
            UseAbility = 4
        }

        public override int MessageTypeId => CoopProtocol.IntentId;
        public IntentKind Kind;
        public int TargetId = -1;

        /// <summary>
        /// An attack's <see cref="RPGFramework.Combat.AttackContext.Tags"/> and source, as the player claims them.
        /// </summary>
        public int Tags;
        public int SourceId;

        public string AbilityId = "";

        public void Write(BinaryWriter writer)
        {
            writer.Write((byte)Kind);
            writer.Write(TargetId);
            writer.Write(Tags);
            writer.Write(SourceId);
            writer.Write(AbilityId ?? "");
        }

        public void Read(BinaryReader reader)
        {
            Kind = (IntentKind)reader.ReadByte();
            TargetId = reader.ReadInt32();
            Tags = reader.ReadInt32();
            SourceId = reader.ReadInt32();
            AbilityId = reader.ReadString();
        }
    }
}
