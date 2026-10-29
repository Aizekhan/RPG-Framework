using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using RPGFramework.Localization;
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
        public const int SceneId = 1108;
        public const int LobbyId = 1109;
        public const int LobbyChoiceId = 1110;

        /// <summary>
        /// The first id a game may give its own messages (<see cref="CoopGameMessages"/>).
        /// </summary>
        public const int FirstGameMessageId = 1200;

        /// <summary>
        /// Changes whenever the messages change, so older and newer builds refuse each other instead of misreading.
        /// </summary>
        public const int Version = 16;

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
            serializer.RegisterMessageType<CoopSceneMessage>(SceneId);
            serializer.RegisterMessageType<CoopLobbyMessage>(LobbyId);
            serializer.RegisterMessageType<CoopLobbyChoiceMessage>(LobbyChoiceId);
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
            .Set(SceneId, MessageAuthority.Server)
            .Set(LobbyId, MessageAuthority.Server)
            .Set(LobbyChoiceId, MessageAuthority.Client)
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

        internal static void WriteCurrencies(BinaryWriter writer, List<CurrencyRecord> currencies)
        {
            writer.Write(currencies.Count);
            foreach (var currency in currencies)
            {
                writer.Write(currency.Currency ?? "");
                writer.Write(currency.Amount);
            }
        }

        internal static List<CurrencyRecord> ReadCurrencies(BinaryReader reader)
        {
            int count = ReadCount(reader);
            var currencies = new List<CurrencyRecord>(count);
            for (int i = 0; i < count; i++)
                currencies.Add(new CurrencyRecord { Currency = reader.ReadString(), Amount = reader.ReadInt64() });
            return currencies;
        }

        internal static void WriteProgress(BinaryWriter writer, PlayerProgress progress)
        {
            writer.Write(progress.Level);
            writer.Write(progress.TotalExperience);
            writer.Write(progress.Kills);
            writer.Write(progress.Items.Count);
            foreach (var item in progress.Items)
                writer.Write(item ?? "");
            WriteCurrencies(writer, progress.Currencies);
            writer.Write(progress.ActiveQuests.Count);
            foreach (var quest in progress.ActiveQuests)
            {
                writer.Write(quest.QuestId ?? "");
                writer.Write(quest.Objectives.Count);
                foreach (var objective in quest.Objectives)
                {
                    writer.Write(objective.ObjectiveId ?? "");
                    writer.Write(objective.Progress);
                }
            }
            writer.Write(progress.CompletedQuests.Count);
            foreach (var quest in progress.CompletedQuests)
                writer.Write(quest ?? "");
        }

        internal static PlayerProgress ReadProgress(BinaryReader reader)
        {
            var progress = new PlayerProgress
            {
                Level = reader.ReadInt32(), TotalExperience = reader.ReadInt64(), Kills = reader.ReadInt32()
            };
            int items = ReadCount(reader);
            for (int i = 0; i < items; i++)
                progress.Items.Add(reader.ReadString());
            progress.Currencies = ReadCurrencies(reader);
            int quests = ReadCount(reader);
            for (int i = 0; i < quests; i++)
            {
                var record = new QuestRecord { QuestId = reader.ReadString() };
                int objectives = ReadCount(reader);
                for (int o = 0; o < objectives; o++)
                    record.Objectives.Add(new ObjectiveRecord { ObjectiveId = reader.ReadString(), Progress = reader.ReadInt32() });
                progress.ActiveQuests.Add(record);
            }
            int completed = ReadCount(reader);
            for (int i = 0; i < completed; i++)
                progress.CompletedQuests.Add(reader.ReadString());
            return progress;
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
            Number((int)settings.ProgressMode);
            text.Append(settings.LootToBackpack ? "bag|" : "drop|");
            foreach (var currency in settings.Currencies.OrderBy(c => c, StringComparer.Ordinal))
                text.Append("cur:").Append(currency).Append('|');
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
                // Rolled gear is rebuilt on every machine from the same affixes
                foreach (var affix in content.Affixes?.All ?? (IReadOnlyList<RPGFramework.Items.AffixDefinition>)new RPGFramework.Items.AffixDefinition[0])
                {
                    text.Append("afx:").Append(affix.Id).Append(',').Append(affix.Stat).Append(',')
                        .Append(affix.Min.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
                        .Append(affix.Max.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append('|');
                }
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

        /// <summary>
        /// The progress the player brings (their own save) when every player has their own
        /// (<see cref="ProgressMode.PerPlayer"/>); null for none.
        /// </summary>
        public PlayerProgress Profile;

        public void Write(BinaryWriter writer)
        {
            writer.Write(Fingerprint ?? "");
            writer.Write(Name ?? "");
            writer.Write(RejoinToken ?? "");
            writer.Write(Profile != null);
            if (Profile != null)
                CoopProtocol.WriteProgress(writer, Profile);
        }

        public void Read(BinaryReader reader)
        {
            Fingerprint = reader.ReadString();
            Name = reader.ReadString();
            RejoinToken = reader.ReadString();
            Profile = reader.ReadBoolean() ? CoopProtocol.ReadProgress(reader) : null;
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

        /// <summary>
        /// Let in to wait in the host's lobby (<see cref="CoopHost.UseLobby"/>); a second welcome with the player id
        /// follows when the match starts.
        /// </summary>
        public bool InLobby;

        public void Write(BinaryWriter writer)
        {
            writer.Write(Accepted);
            writer.Write(PlayerId);
            writer.Write(Reason ?? "");
            writer.Write(RejoinToken ?? "");
            writer.Write(Rejoined);
            writer.Write(InLobby);
        }

        public void Read(BinaryReader reader)
        {
            Accepted = reader.ReadBoolean();
            PlayerId = reader.ReadInt32();
            Reason = reader.ReadString();
            RejoinToken = reader.ReadString();
            Rejoined = reader.ReadBoolean();
            InLobby = reader.ReadBoolean();
        }
    }

    /// <summary>
    /// Someone in the host's lobby: their name, the hero (loadout) they picked and whether they are ready. Id 0 is the
    /// host.
    /// </summary>
    public sealed class CoopLobbyEntry
    {
        public int Id;
        public string Name = "";
        public string Loadout = "";
        public bool Ready;
        public bool IsHost => Id == 0;

        public override string ToString() => $"{Name}{(Loadout.Length > 0 ? " (" + Loadout + ")" : "")}{(Ready ? " ready" : "")}";
    }

    /// <summary>
    /// Host → a player in the lobby: who is there, how many may come, and the seconds to the start once the host
    /// started it (below 0: not starting).
    /// </summary>
    public sealed class CoopLobbyMessage : NetworkMessage, INetworkPayload
    {
        public override int MessageTypeId => CoopProtocol.LobbyId;
        public List<CoopLobbyEntry> Entries = new List<CoopLobbyEntry>();
        public int MaxPlayers;
        public float Countdown = -1f;

        /// <summary>
        /// The receiver's own entry.
        /// </summary>
        public int YourId = -1;

        public void Write(BinaryWriter writer)
        {
            writer.Write(Entries.Count);
            foreach (var entry in Entries)
            {
                writer.Write(entry.Id);
                writer.Write(entry.Name ?? "");
                writer.Write(entry.Loadout ?? "");
                writer.Write(entry.Ready);
            }
            writer.Write(MaxPlayers);
            writer.Write(Countdown);
            writer.Write(YourId);
        }

        public void Read(BinaryReader reader)
        {
            int count = CoopProtocol.ReadCount(reader);
            Entries = new List<CoopLobbyEntry>(count);
            for (int i = 0; i < count; i++)
            {
                Entries.Add(new CoopLobbyEntry
                {
                    Id = reader.ReadInt32(), Name = reader.ReadString(), Loadout = reader.ReadString(), Ready = reader.ReadBoolean()
                });
            }
            MaxPlayers = reader.ReadInt32();
            Countdown = reader.ReadSingle();
            YourId = reader.ReadInt32();
        }
    }

    /// <summary>
    /// A player in the lobby → host: ready or not, and the hero (loadout) they picked.
    /// </summary>
    public sealed class CoopLobbyChoiceMessage : NetworkMessage, INetworkPayload
    {
        public override int MessageTypeId => CoopProtocol.LobbyChoiceId;
        public bool Ready;
        public string Loadout = "";

        public void Write(BinaryWriter writer)
        {
            writer.Write(Ready);
            writer.Write(Loadout ?? "");
        }

        public void Read(BinaryReader reader)
        {
            Ready = reader.ReadBoolean();
            Loadout = reader.ReadString();
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
            /// A character's ability went on cooldown (Name = ability id, Amount = seconds). 0 seconds: the cooldown
            /// was cleared; with an empty Name every cooldown of the character was.
            /// </summary>
            Cooldown = 6,

            /// <summary>
            /// A player used a place (Id = the player, Name = place id, CharacterId = its kind).
            /// </summary>
            Interacted = 7
        }

        /// <summary>
        /// A place players use, as the host has it (<see cref="RPGFramework.Interaction.InteractionPoint"/>).
        /// </summary>
        public struct PlaceState
        {
            public string Id;
            public string Kind;
            public string Prompt;
            public float Radius;
            public bool Once;
            public bool Enabled;
            public float X;
            public float Y;
            public float Z;

            public static PlaceState Of(RPGFramework.Interaction.InteractionPoint point) => new PlaceState
            {
                Id = point.Id, Kind = point.Kind, Prompt = point.Prompt, Radius = point.Radius, Once = point.Once,
                Enabled = point.Enabled, X = point.X, Y = point.Y, Z = point.Z
            };

            public RPGFramework.Interaction.InteractionPoint ToPoint() =>
                new RPGFramework.Interaction.InteractionPoint(Id, Kind, Radius, Prompt, Once) { Enabled = Enabled }.At(X, Y, Z);

            public bool SameAs(PlaceState other) =>
                Id == other.Id && Kind == other.Kind && Prompt == other.Prompt && Radius.Equals(other.Radius)
                && Once == other.Once && Enabled == other.Enabled && X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z);
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
            /// A hit: critical, and what multiplied its damage (<see cref="RPGFramework.Combat.AttackContext.Multiplier"/>).
            /// </summary>
            public bool Critical;

            public float Multiplier;

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

        /// <summary>
        /// Places that are new or changed since the last send, and the ids of places that are gone.
        /// </summary>
        public List<PlaceState> Places = new List<PlaceState>();
        public List<string> PlacesGone = new List<string>();

        public bool IsEmpty => Changes.Count == 0 && States.Count == 0 && Party == null && Places.Count == 0 && PlacesGone.Count == 0;

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
                        writer.Write(change.Critical);
                        writer.Write(change.Multiplier);
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
                    case ChangeKind.Interacted:
                        writer.Write(change.Name ?? "");
                        writer.Write(change.CharacterId ?? "");
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
            writer.Write(Places.Count);
            foreach (var place in Places)
            {
                writer.Write(place.Id ?? "");
                writer.Write(place.Kind ?? "");
                writer.Write(place.Prompt ?? "");
                writer.Write(place.Radius);
                writer.Write(place.Once);
                writer.Write(place.Enabled);
                writer.Write(place.X);
                writer.Write(place.Y);
                writer.Write(place.Z);
            }
            writer.Write(PlacesGone.Count);
            foreach (var id in PlacesGone)
                writer.Write(id ?? "");
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
                        change.Critical = reader.ReadBoolean();
                        change.Multiplier = reader.ReadSingle();
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
                    case ChangeKind.Interacted:
                        change.Name = reader.ReadString();
                        change.CharacterId = reader.ReadString();
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
            int places = CoopProtocol.ReadCount(reader);
            for (int i = 0; i < places; i++)
            {
                Places.Add(new PlaceState
                {
                    Id = reader.ReadString(), Kind = reader.ReadString(), Prompt = reader.ReadString(),
                    Radius = reader.ReadSingle(), Once = reader.ReadBoolean(), Enabled = reader.ReadBoolean(),
                    X = reader.ReadSingle(), Y = reader.ReadSingle(), Z = reader.ReadSingle()
                });
            }
            int gone = CoopProtocol.ReadCount(reader);
            for (int i = 0; i < gone; i++)
                PlacesGone.Add(reader.ReadString());
        }
    }

    /// <summary>
    /// The progress a player sees in the HUD: the party's when it is shared, otherwise their own.
    /// </summary>
    public sealed class PartyStatus
    {
        public int Level;
        public long TotalExperience;
        public long ExperienceForNextLevel;
        public int Kills;
        public int Wave;
        public List<string> Items = new List<string>();
        public List<CurrencyRecord> Currencies = new List<CurrencyRecord>();
        public List<QuestRecord> ActiveQuests = new List<QuestRecord>();
        public List<string> CompletedQuests = new List<string>();
        public List<string> Log = new List<string>();

        /// <summary>
        /// The log's lines as texts to translate, one per <see cref="Log"/> line (<see cref="LocalizedTextCodec"/>
        /// strings; empty for a plain line), so a friend reads the log in their own language.
        /// </summary>
        public List<string> LogTexts = new List<string>();

        /// <summary>
        /// The PvP match; null when the game is not PvP.
        /// </summary>
        public PvpStatus Pvp;

        /// <summary>
        /// The host's game is paused (<see cref="ActorWorld.IsPaused"/>): friends' characters stand still too.
        /// </summary>
        public bool Paused;

        /// <summary>
        /// What this player wears, every set (<see cref="ActorGear.RecordsOf"/>), and the set in use.
        /// </summary>
        public List<GearRecord> Gear = new List<GearRecord>();
        public string Loadout = "";

        public static PartyStatus Of(ActorWorld world) => Of(world, world.PlayerId);

        /// <summary>
        /// What this party member sees: their own progress (<see cref="ProgressMode.PerPlayer"/>) or the party's.
        /// </summary>
        public static PartyStatus Of(ActorWorld world, int playerId)
        {
            if (world.ProgressOwnerOf(playerId) < 0)
                playerId = world.PlayerId;
            var status = new PartyStatus
            {
                Level = world.LevelOf(playerId), TotalExperience = world.ExperienceOf(playerId),
                ExperienceForNextLevel = world.ExperienceForNextLevelOf(playerId), Kills = world.KillsOf(playerId),
                Wave = world.CurrentWave, Paused = world.IsPaused, Loadout = world.Gear.LoadoutOf(playerId)
            };
            status.Gear.AddRange(world.Gear.RecordsOf(playerId));
            status.Items.AddRange(world.InventoryOf(playerId).GetAllItems().Where(i => i != null).Select(i => i.ItemId));
            status.Currencies.AddRange(world.Wallet.BalancesOf(world.ProgressOwnerOf(playerId)).Where(b => b.Value > 0)
                .OrderBy(b => b.Key, StringComparer.Ordinal).Select(b => new CurrencyRecord { Currency = b.Key, Amount = b.Value }));
            foreach (var (quest, progress) in world.ActiveQuestsOf(playerId))
            {
                var record = new QuestRecord { QuestId = quest.Id };
                record.Objectives.AddRange(progress.ObjectiveProgress.Select(p => new ObjectiveRecord { ObjectiveId = p.Key, Progress = p.Value }));
                status.ActiveQuests.Add(record);
            }
            status.CompletedQuests.AddRange(world.CompletedQuestsOf(playerId));
            status.Log.AddRange(world.LogLines.Select(l => l.English));
            status.LogTexts.AddRange(world.LogLines.Select(l => l.Text.IsEmpty ? "" : LocalizedTextCodec.ToWire(l.Text)));
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
            CoopProtocol.WriteCurrencies(writer, Currencies);
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
            WriteStrings(writer, LogTexts);
            writer.Write(Pvp != null);
            Pvp?.Write(writer);
            writer.Write(Paused);
            writer.Write(Loadout ?? "");
            writer.Write(Gear.Count);
            foreach (var record in Gear)
            {
                writer.Write(record.Loadout ?? "");
                writer.Write(record.SlotId ?? "");
                writer.Write(record.ItemId ?? "");
            }
        }

        public void Read(BinaryReader reader)
        {
            Level = reader.ReadInt32();
            TotalExperience = reader.ReadInt64();
            ExperienceForNextLevel = reader.ReadInt64();
            Kills = reader.ReadInt32();
            Wave = reader.ReadInt32();
            Items = ReadStrings(reader);
            Currencies = CoopProtocol.ReadCurrencies(reader);
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
            LogTexts = ReadStrings(reader);
            Pvp = null;
            if (reader.ReadBoolean())
            {
                Pvp = new PvpStatus();
                Pvp.Read(reader, CoopProtocol.ReadCount);
            }
            Paused = reader.ReadBoolean();
            Loadout = reader.ReadString();
            int worn = CoopProtocol.ReadCount(reader);
            Gear = new List<GearRecord>(worn);
            for (int i = 0; i < worn; i++)
                Gear.Add(new GearRecord { Loadout = reader.ReadString(), SlotId = reader.ReadString(), ItemId = reader.ReadString() });
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
            UseAbility = 4,

            /// <summary>
            /// The player takes a trader's offer (<see cref="OfferId"/>).
            /// </summary>
            TakeOffer = 5,

            /// <summary>
            /// The player uses a place (<see cref="PointId"/>; empty: the nearest to them on the host).
            /// </summary>
            Interact = 6,

            /// <summary>
            /// The player puts an item (<see cref="ItemId"/>) on, in <see cref="SlotId"/> (empty: the first that takes it).
            /// </summary>
            Equip = 7,

            /// <summary>
            /// The player takes off what is in <see cref="SlotId"/>.
            /// </summary>
            Unequip = 8,

            /// <summary>
            /// The player wears another of their sets (<see cref="Loadout"/>).
            /// </summary>
            UseLoadout = 9
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
        public string OfferId = "";
        public string PointId = "";
        public string ItemId = "";
        public string SlotId = "";
        public string Loadout = "";

        public void Write(BinaryWriter writer)
        {
            writer.Write((byte)Kind);
            writer.Write(TargetId);
            writer.Write(Tags);
            writer.Write(SourceId);
            writer.Write(AbilityId ?? "");
            writer.Write(OfferId ?? "");
            writer.Write(PointId ?? "");
            writer.Write(ItemId ?? "");
            writer.Write(SlotId ?? "");
            writer.Write(Loadout ?? "");
        }

        public void Read(BinaryReader reader)
        {
            Kind = (IntentKind)reader.ReadByte();
            TargetId = reader.ReadInt32();
            Tags = reader.ReadInt32();
            SourceId = reader.ReadInt32();
            AbilityId = reader.ReadString();
            OfferId = reader.ReadString();
            PointId = reader.ReadString();
            ItemId = reader.ReadString();
            SlotId = reader.ReadString();
            Loadout = reader.ReadString();
        }
    }
}
