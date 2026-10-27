using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using RPGFramework.ActionRpg.Coop;
using RPGFramework.Bootstrap;
using RPGFramework.Events;
using RPGFramework.Items;
using RPGFramework.Logging;
using RPGFramework.Loot;
using RPGFramework.Networking;
using RPGFramework.Quests;

namespace RPGFramework.ActionRpg.Tests
{
    /// <summary>
    /// Co-op over an in-memory network: a friend joins and sees the whole world, their attacks go to the host which
    /// decides, hits, deaths and the party's progress reach them, movement goes both ways, leaving and a host going
    /// away are handled, and a different game version or a full party is refused with a reason. A lost link (silence,
    /// as when a cable is pulled) is noticed on both sides; the host keeps the lost player's seat for a while and they
    /// come back as themselves; broken or hostile messages do not break the game.
    /// </summary>
    public sealed class CoopTests
    {
        private const string Goblin = "goblin";
        private const string Ear = "goblin_ear";
        private const string HuntQuest = "hunt";
        private const int Port = 7777;

        private sealed class Motion : IActorMotion
        {
            public readonly Dictionary<int, ActorPose> Poses = new Dictionary<int, ActorPose>();
            public readonly List<(int Id, ActorPose Pose)> Targets = new List<(int, ActorPose)>();

            public bool TryGetPose(int entityId, out ActorPose pose) => Poses.TryGetValue(entityId, out pose);

            public void SetTargetPose(int entityId, ActorPose pose)
            {
                Targets.Add((entityId, pose));
                Poses[entityId] = pose;
            }

            public readonly List<(int Id, ActorPose Pose)> Teleports = new List<(int, ActorPose)>();

            public void Teleport(int entityId, ActorPose pose)
            {
                Teleports.Add((entityId, pose));
                Poses[entityId] = pose;
            }
        }

        private sealed class Guest
        {
            public CoopClient Client;
            public EventBus Bus;
            public Motion Motion;
            public NetworkManager Network;
            public LoopbackTransport Transport;
            public readonly List<ActorSpawnedEvent> Spawned = new List<ActorSpawnedEvent>();
            public readonly List<ActorDespawnedEvent> Despawned = new List<ActorDespawnedEvent>();
            public readonly List<ActorDamagedEvent> Damaged = new List<ActorDamagedEvent>();
            public RemoteWorld World => Client.World;
        }

        /// <summary>
        /// A game's own message: players vote for the next map.
        /// </summary>
        public sealed class VoteMessage : NetworkMessage, INetworkPayload
        {
            public override int MessageTypeId => CoopProtocol.FirstGameMessageId;
            public int Choice;

            public void Write(BinaryWriter writer) => writer.Write(Choice);

            public void Read(BinaryReader reader) => Choice = reader.ReadInt32();
        }

        /// <summary>
        /// A game's own message: the host tells everyone the seed of the next map.
        /// </summary>
        public sealed class SeedMessage : NetworkMessage, INetworkPayload
        {
            public override int MessageTypeId => CoopProtocol.FirstGameMessageId + 1;
            public int Seed;

            public void Write(BinaryWriter writer) => writer.Write(Seed);

            public void Read(BinaryReader reader) => Seed = reader.ReadInt32();
        }

        private static CoopGameMessages GameMessages() => new CoopGameMessages()
            .Add<VoteMessage>(CoopProtocol.FirstGameMessageId, MessageAuthority.Client)
            .Add<SeedMessage>(CoopProtocol.FirstGameMessageId + 1, MessageAuthority.Server);

        private Logger _logger;
        private LoopbackNetwork _network;
        private GameHost _game;
        private ActionRpgModule _module;
        private CoopHost _host;
        private Motion _hostMotion;
        private LoopbackTransport _hostTransport;
        private readonly List<Guest> _guests = new List<Guest>();
        private System.Func<ActionRpgSettings> _settings = () => Settings();

        private ActorWorld World => _module.World;

        private static ActionRpgSettings Settings(float goblinAttack = 6f)
        {
            var settings = new ActionRpgSettings
            {
                Player = new CharacterData
                {
                    Id = "hero", DisplayName = "Hero", MaxHealth = 100f, Attack = 50f, Defense = 5f, HealthPerLevel = 20f
                },
                Waves = new WaveSettings { Enabled = false },
                Seed = 7
            };
            settings.Enemies.Add(new CharacterData
            {
                Id = Goblin, DisplayName = "Goblin", MaxHealth = 30f, Attack = goblinAttack, Defense = 2f,
                ExperienceReward = 40, LootTableId = Goblin
            });
            settings.StartingQuests.Add(HuntQuest);
            return settings;
        }

        // Games with status effects and abilities (PlayWithPowers)
        private bool _powers;

        private ActionRpgContent Content()
        {
            var content = new ActionRpgContent();
            if (_powers)
            {
                content.StatusEffects.Add(RPGFramework.StatusEffects.StatusEffectData.CreateDoT("poison", "Poison", 10f, 1f, 2f));
                var haste = RPGFramework.StatusEffects.StatusEffectData.Create("haste", "Haste", RPGFramework.StatusEffects.EffectType.Buff);
                haste.Duration = 5f;
                haste.Modifiers.Add(RPGFramework.StatusEffects.EffectModifierData.Create(RPGFramework.StatusEffects.ModifierType.Movement,
                    "Speed", 2f, RPGFramework.StatusEffects.ModifierOperation.Multiply));
                content.StatusEffects.Add(haste);
                content.Abilities.Add(new RPGFramework.Abilities.Ability(new RPGFramework.Abilities.AbilityMetadata("bolt", "Bolt", cooldown: 3f),
                    targetType: RPGFramework.Abilities.TargetType.Enemy));
            }
            content.Items.Add(new Item(Ear, "Goblin Ear", "Proof", ItemType.Crafting, maxStackSize: 99));
            content.LootTables.Add(LootTableBuilder.Create(Goblin, "Goblin").AddGuaranteedDrop(Ear).WithDrops(0, 0).Build());
            content.Quests.Add(new Quest(HuntQuest, "The Hunt", "Slay 2 goblins",
                new List<IQuestObjective> { new QuestObjective("slay", ObjectiveType.Kill, Goblin, 2, "Slay goblins") },
                new List<IQuestReward> { new QuestReward(RewardType.Experience, null, 100) },
                new List<string>(), minimumLevel: 1, isRepeatable: false));
            return content;
        }

        [SetUp]
        public void SetUp()
        {
            _logger = new Logger(LogSeverity.Warning, new MemoryLogSink(256));
            _settings = () => Settings();
            _powers = false;
            StartHost();
        }

        /// <summary>
        /// Plays a game with other rules: the host starts over with them, friends who join use the same.
        /// </summary>
        private void PlayWith(System.Func<ActionRpgSettings> settings)
        {
            _host.Dispose();
            _game.Dispose();
            _settings = settings;
            StartHost();
        }

        private void StartHost()
        {
            _network = new LoopbackNetwork();
            _game = new GameHost(_logger);
            var settings = _settings();
            var content = Content();
            _game.Start(builder => _module = ActionRpgComposition.Configure(builder, settings, content));
            _hostMotion = new Motion();
            _hostTransport = _network.CreateTransport();
            _host = new CoopHost(World, _game.Container.Resolve<IEventBus>(), CoopProtocol.CreateNetwork(_hostTransport, gameMessages: GameMessages()),
                _hostMotion, CoopProtocol.Fingerprint(settings, content));
            _host.Open(Port);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var guest in _guests)
                guest.Client.Dispose();
            _guests.Clear();
            _host.Dispose();
            _game.Dispose();
            _logger.Dispose();
        }

        private Guest Join(ActionRpgSettings settings = null, string name = "", string rejoinToken = "", PlayerProgress profile = null)
        {
            var guest = new Guest { Bus = new EventBus(), Motion = new Motion() };
            guest.Bus.Subscribe<ActorSpawnedEvent>(guest.Spawned.Add);
            guest.Bus.Subscribe<ActorDespawnedEvent>(guest.Despawned.Add);
            guest.Bus.Subscribe<ActorDamagedEvent>(guest.Damaged.Add);
            guest.Transport = _network.CreateTransport();
            guest.Network = CoopProtocol.CreateNetwork(guest.Transport, gameMessages: GameMessages());
            guest.Client = new CoopClient(settings ?? _settings(), Content(), guest.Bus, guest.Network, guest.Motion)
            {
                PlayerName = name, RejoinToken = rejoinToken, Profile = profile
            };
            _guests.Add(guest);
            guest.Client.Join("127.0.0.1", Port);
            Pump();
            return guest;
        }

        /// <summary>
        /// Lets game time pass on every machine, in 0.1 s frames.
        /// </summary>
        private void Run(float seconds)
        {
            for (float time = 0f; time < seconds; time += 0.1f)
            {
                _host.Update(0.1f);
                foreach (var guest in _guests)
                    guest.Client.Update(0.1f);
            }
        }

        private void Pump()
        {
            for (int round = 0; round < 4; round++)
            {
                _host.Update(0.06f);
                foreach (var guest in _guests)
                    guest.Client.Update(0.06f);
            }
        }

        [Test]
        public void Join_TheFriendGetsAPlayerAndSeesTheWholeWorld()
        {
            int first = World.SpawnEnemy(Goblin);
            int second = World.SpawnEnemy(Goblin);

            var guest = Join();

            Assert.That(guest.Client.State, Is.EqualTo(CoopClientState.Playing));
            Assert.That(World.Players, Has.Count.EqualTo(2));
            Assert.That(guest.World.PlayerId, Is.EqualTo(World.Players[1]));
            Assert.That(guest.World.Player.IsPlayer, Is.True);
            Assert.That(guest.World.Actors.Select(a => a.EntityId), Is.EquivalentTo(World.Actors.Select(a => a.EntityId)));
            Assert.That(guest.World.LivingEnemies, Is.EquivalentTo(new[] { first, second }));
            Assert.That(guest.World.LivingPlayers, Is.EquivalentTo(World.Players));
            Assert.That(guest.Spawned, Has.Count.EqualTo(4));
            Assert.That(guest.World.Level, Is.EqualTo(1));
            Assert.That(guest.World.ActiveQuests.Select(q => q.Quest.Id), Is.EqualTo(new[] { HuntQuest }));
            Assert.That(guest.World.Log, Has.Some.Contains("joins the party"));
            Assert.That(_host.PlayerCount, Is.EqualTo(2));
        }

        [Test]
        public void FriendsAttack_TheHostDecides_TheHitTheKillAndTheLootReachThem()
        {
            var guest = Join();
            int goblin = World.SpawnEnemy(Goblin);
            Pump();

            for (int round = 0; round < 10 && World.Get(goblin) != null; round++)
            {
                Assert.That(guest.World.Attack(guest.World.PlayerId, goblin), Is.EqualTo(0f), "a client never deals damage itself");
                Pump();
            }

            Assert.That(World.Get(goblin), Is.Null, "the host's rules killed it");
            Assert.That(guest.Damaged.Any(d => d.TargetId == goblin && d.AttackerId == guest.World.PlayerId), Is.True);
            Assert.That(guest.Despawned.Select(d => d.EntityId), Has.Member(goblin));
            Assert.That(guest.World.Get(goblin), Is.Null);
            Assert.That(guest.World.Kills, Is.EqualTo(1));
            Assert.That(guest.World.TotalExperience, Is.EqualTo(World.TotalExperience).And.GreaterThan(0));
            Assert.That(guest.World.Inventory.GetAllItems().Select(i => i.ItemId), Has.Member(Ear));
            var hunt = guest.World.ActiveQuests.Single();
            Assert.That(hunt.Progress.ObjectiveProgress["slay"], Is.EqualTo(1));
        }

        [Test]
        public void FriendFarAway_CannotStrike()
        {
            _host.MaxAttackDistance = 2f;
            var guest = Join();
            int goblin = World.SpawnEnemy(Goblin);
            Pump();
            _hostMotion.Poses[guest.World.PlayerId] = new ActorPose(0f, 0f, 0f, 0f);
            _hostMotion.Poses[goblin] = new ActorPose(10f, 0f, 0f, 0f);

            guest.World.Attack(guest.World.PlayerId, goblin);
            Pump();

            Assert.That(World.Get(goblin).Health, Is.EqualTo(World.Get(goblin).MaxHealth));
        }

        [Test]
        public void FriendFalls_SeesIt_AndRevives()
        {
            var guest = Join();
            int goblin = World.SpawnEnemy(Goblin);
            int friend = guest.World.PlayerId;
            for (int round = 0; round < 200 && World.Get(friend).IsAlive(); round++)
                World.Attack(goblin, friend);
            Pump();

            Assert.That(guest.World.Player.IsAlive(), Is.False);
            Assert.That(guest.World.Player.Health, Is.EqualTo(0f));
            Assert.That(guest.World.LivingPlayers, Is.EqualTo(new[] { World.PlayerId }));

            Assert.That(guest.World.RevivePlayer(), Is.True);
            Pump();

            Assert.That(World.Get(friend).IsAlive(), Is.True);
            Assert.That(guest.World.Player.IsAlive(), Is.True);
            Assert.That(guest.World.Player.Health, Is.EqualTo(guest.World.Player.MaxHealth));
        }

        [Test]
        public void Movement_GoesBothWays_AndNobodyMovesSomeoneElse()
        {
            var guest = Join();
            int goblin = World.SpawnEnemy(Goblin);
            Pump();
            _hostMotion.Poses[World.PlayerId] = new ActorPose(1f, 0f, 2f, 90f);
            _hostMotion.Poses[goblin] = new ActorPose(5f, 0f, 5f, 180f);
            guest.Motion.Poses[guest.World.PlayerId] = new ActorPose(-3f, 0f, 4f, 45f);
            Pump();

            Assert.That(guest.Motion.Targets.Any(t => t.Id == goblin && t.Pose.X == 5f && t.Pose.Yaw == 180f), Is.True);
            Assert.That(guest.Motion.Targets.Any(t => t.Id == World.PlayerId && t.Pose.Z == 2f), Is.True);
            Assert.That(guest.Motion.Targets.Any(t => t.Id == guest.World.PlayerId), Is.False, "the friend moves themselves");
            Assert.That(_hostMotion.Targets.Any(t => t.Id == guest.World.PlayerId && t.Pose.X == -3f), Is.True);

            // A modified client trying to move the goblin is ignored
            var cheat = new CoopMotionMessage();
            cheat.Poses.Add((goblin, new ActorPose(99f, 0f, 99f, 0f)));
            _hostMotion.Targets.Clear();
            guest.Network.SendToServer(cheat, reliable: false);
            _host.Update(0.01f);
            Assert.That(_hostMotion.Targets.Select(t => t.Id), Has.No.Member(goblin));
        }

        [Test]
        public void MovingFasterThanTheCharacterCan_IsPutBack_AndTheFriendIsTold()
        {
            PlayWith(() =>
            {
                var settings = Settings();
                settings.Player.MoveSpeed = 5f;
                return settings;
            });
            var guest = Join();
            int friend = guest.World.PlayerId;
            var pose = new ActorPose(0f, 0f, 0f, 0f);
            guest.Motion.Poses[friend] = pose;
            Run(0.3f);

            // Walking at full speed for a second is fine
            for (int step = 0; step < 10; step++)
            {
                pose.X += 0.5f;
                guest.Motion.Poses[friend] = pose;
                Run(0.1f);
            }
            Run(0.2f);
            Assert.That(_host.MovementCorrections, Is.EqualTo(0));
            Assert.That(_hostMotion.Poses[friend].X, Is.EqualTo(5f));

            // Jumping 20 units at once is not: the host lets them go only as far as they could, and tells them
            guest.Motion.Poses[friend] = new ActorPose(25f, 0f, 0f, 0f);
            guest.Client.SendMotion();
            Pump();

            Assert.That(_host.MovementCorrections, Is.EqualTo(1));
            Assert.That(_hostMotion.Poses[friend].X, Is.GreaterThan(5f).And.LessThan(8f));
            Assert.That(guest.Client.Corrections, Is.EqualTo(1));
            var correction = guest.Motion.Teleports.Single();
            Assert.That(correction.Id, Is.EqualTo(friend));
            Assert.That(correction.Pose.X, Is.EqualTo(_hostMotion.Poses[friend].X).Within(0.001f));

            // From the corrected place, walking on is fine again
            Run(0.5f);
            Assert.That(_host.MovementCorrections, Is.EqualTo(1));
        }

        [Test]
        public void WithoutAMoveSpeed_MovementIsNotChecked()
        {
            var guest = Join();
            int friend = guest.World.PlayerId;
            guest.Motion.Poses[friend] = new ActorPose(0f, 0f, 0f, 0f);
            Pump();
            guest.Motion.Poses[friend] = new ActorPose(500f, 0f, 0f, 0f);
            Pump();

            Assert.That(_host.MovementCorrections, Is.EqualTo(0));
            Assert.That(_hostMotion.Poses[friend].X, Is.EqualTo(500f));
        }

        [Test]
        public void TheHostTeleportsAFriend_TheyArriveOnTheirOwnMachine()
        {
            var guest = Join();
            int friend = guest.World.PlayerId;
            var spawn = new ActorPose(7f, 0f, -7f, 90f);

            _host.Teleport(friend, spawn);
            Pump();

            Assert.That(_hostMotion.Teleports.Single(), Is.EqualTo((friend, spawn)));
            Assert.That(guest.Motion.Teleports.Single(), Is.EqualTo((friend, spawn)));
            Assert.That(() => _host.Teleport(friend, new ActorPose(float.NaN, 0f, 0f, 0f)), Throws.ArgumentException);
        }

        [Test]
        public void Pvp_TeamsAndScoreReachFriends_AndTheHostRefereesEveryBlow()
        {
            PlayWith(() =>
            {
                var settings = Settings();
                settings.Pvp = new PvpSettings { Mode = PvpMode.TeamDeathmatch, ScoreToWin = 2, RespawnDelay = 1f };
                return settings;
            });
            var blue = Join(name: "Blue");
            var red = Join(name: "Red");
            _game.Update(0.1f);
            Pump();

            int host = World.PlayerId, bluePlayer = blue.World.PlayerId, redPlayer = red.World.PlayerId;
            Assert.That(blue.World.Get(host).Team, Is.EqualTo("Red"));
            Assert.That(blue.World.Player.Team, Is.EqualTo("Blue"));
            Assert.That(red.World.Player.Team, Is.EqualTo("Red"));
            Assert.That(blue.World.PvpStatus, Is.Not.Null);
            Assert.That(blue.World.PvpStatus.Phase, Is.EqualTo(PvpPhase.Playing));
            Assert.That(blue.World.PvpStatus.Sides.Select(s => s.Name), Is.EquivalentTo(new[] { "Red", "Blue" }));

            // A teammate cannot be hit: the client does not ask, and the host refuses a modified client that does
            Assert.That(red.World.AreHostile(redPlayer, host), Is.False);
            red.Network.SendToServer(new CoopIntentMessage { Kind = CoopIntentMessage.IntentKind.Attack, TargetId = host });
            Pump();
            Assert.That(World.Player.HealthFraction, Is.EqualTo(1f));

            // An opponent can
            for (int round = 0; round < 20 && World.Player.IsAlive(); round++)
            {
                blue.World.Attack(bluePlayer, host);
                Pump();
            }
            Assert.That(World.Player.IsDead(), Is.True);
            _game.Update(0.1f);
            Pump();
            Assert.That(blue.World.PvpStatus.ScoreOf("Blue"), Is.EqualTo(1));
            Assert.That(red.World.Log, Has.Some.Contains("defeats"));

            // Nobody revives on demand in PvP: the match brings them back after the delay
            for (int round = 0; round < 20 && World.Get(redPlayer).IsAlive(); round++)
                World.Attack(bluePlayer, redPlayer);
            red.Network.SendToServer(new CoopIntentMessage { Kind = CoopIntentMessage.IntentKind.Revive });
            Assert.That(red.World.RevivePlayer(), Is.False);
            Pump();
            Assert.That(World.Get(redPlayer).IsDead(), Is.True);
            for (int frame = 0; frame < 12; frame++)
                _game.Update(0.1f);
            Pump();
            Assert.That(red.World.Player.IsAlive(), Is.True);

            Assert.That(red.World.PvpStatus.Phase, Is.EqualTo(PvpPhase.Finished));
            Assert.That(red.World.PvpStatus.Winner, Is.EqualTo("Blue"));
        }

        [Test]
        public void FriendLeaves_TheirCharacterGoes_HostLeaves_TheFriendIsTold()
        {
            var first = Join();
            var second = Join();
            int leaving = first.World.PlayerId;

            first.Client.Leave();
            Pump();

            Assert.That(World.Players, Has.Count.EqualTo(2));
            Assert.That(World.Get(leaving), Is.Null);
            Assert.That(second.World.Get(leaving), Is.Null);
            Assert.That(second.World.Log, Has.Some.Contains("leaves the party"));
            Assert.That(first.Client.State, Is.EqualTo(CoopClientState.Ended));
            Assert.That(first.World.Actors, Is.Empty);

            string reason = null;
            second.Client.Ended += r => reason = r;
            _host.Dispose();
            Pump();

            Assert.That(reason, Does.Contain("host left"));
            Assert.That(second.World.Actors, Is.Empty);
            Assert.That(second.Despawned.Select(d => d.EntityId), Has.Member(World.PlayerId));
        }

        [Test]
        public void LostLink_TheSeatWaits_AndThePlayerComesBackAsThemselves()
        {
            var ann = Join(name: "Ann");
            var bob = Join(name: "Bob");
            int annId = ann.World.PlayerId;
            World.Get(annId).SetHealth(40f);
            Pump();
            string token = ann.Client.RejoinToken;
            Assert.That(token, Is.Not.Empty, "the host gives every player a way back");

            ann.Transport.GoSilent(); // the cable is pulled: nobody is told
            Run(CoopProtocol.UnstableAfter + 0.5f);

            Assert.That(_host.IsUnstable(annId), Is.True, "the host sees the link struggle first");
            Assert.That(ann.Client.IsUnstable, Is.True);
            Assert.That(World.Get(annId), Is.Not.Null, "a short silence is not a goodbye");

            Run(CoopProtocol.ConnectionTimeout);

            Assert.That(World.Get(annId), Is.Null);
            Assert.That(_host.KeptSeats, Is.EqualTo(1));
            Assert.That(bob.World.Get(annId), Is.Null);
            Assert.That(ann.Client.State, Is.EqualTo(CoopClientState.Ended));
            Assert.That(ann.Client.EndKind, Is.EqualTo(CoopEndKind.ConnectionLost));
            Assert.That(ann.Client.EndReason, Does.Contain("Lost the connection"));

            var back = Join(name: "Someone else", rejoinToken: token);

            Assert.That(back.Client.State, Is.EqualTo(CoopClientState.Playing));
            Assert.That(back.Client.Rejoined, Is.True);
            var player = World.Get(back.World.PlayerId);
            Assert.That(player.Seat, Is.EqualTo(1), "the same seat, so the same colour");
            Assert.That(player.DisplayName, Is.EqualTo("Ann"));
            Assert.That(player.Health, Is.EqualTo(40f), "reconnecting does not heal");
            Assert.That(bob.World.Get(back.World.PlayerId).Seat, Is.EqualTo(1));
            Assert.That(_host.KeptSeats, Is.Zero);
            Assert.That(back.Client.RejoinToken, Is.Not.Empty.And.Not.EqualTo(token), "a token works once");
        }

        [Test]
        public void ComingBackBeforeTheHostNoticed_StillGetsTheSeat()
        {
            var ann = Join(name: "Ann");
            World.Get(ann.World.PlayerId).SetHealth(40f);
            ann.Transport.GoSilent();

            // The friend's machine gave up first and joined again while the host still counts the old link
            var back = Join(name: "Ann", rejoinToken: ann.Client.RejoinToken);

            Assert.That(back.Client.Rejoined, Is.True);
            Assert.That(World.Players, Has.Count.EqualTo(2));
            var player = World.Get(back.World.PlayerId);
            Assert.That(player.Seat, Is.EqualTo(1));
            Assert.That(player.DisplayName, Is.EqualTo("Ann"));
            Assert.That(player.Health, Is.EqualTo(40f));

            // The old link times out later: nothing changes
            Run(CoopProtocol.ConnectionTimeout + 1f);
            Assert.That(World.Players, Has.Count.EqualTo(2));
            Assert.That(_host.KeptSeats, Is.Zero);
            Assert.That(back.Client.State, Is.EqualTo(CoopClientState.Playing));
        }

        [Test]
        public void LeavingOnPurpose_KeepsNoSeat()
        {
            var ann = Join(name: "Ann");
            string token = ann.Client.RejoinToken;

            ann.Client.Leave();
            Pump();

            Assert.That(_host.KeptSeats, Is.Zero);
            var again = Join(name: "Ann", rejoinToken: token);
            Assert.That(again.Client.State, Is.EqualTo(CoopClientState.Playing));
            Assert.That(again.Client.Rejoined, Is.False);
        }

        [Test]
        public void AKeptSeat_IsNotGivenToNewcomers_UntilItsTimeIsUp()
        {
            _host.MaxPlayers = 3;
            _host.RejoinWindow = 5f;
            var ann = Join(name: "Ann");
            Join(name: "Bob");

            ann.Transport.GoSilent();
            Run(CoopProtocol.ConnectionTimeout + 1f);
            var newcomer = Join(name: "Cid");

            Assert.That(newcomer.Client.State, Is.EqualTo(CoopClientState.Ended));
            Assert.That(newcomer.Client.EndKind, Is.EqualTo(CoopEndKind.Refused));
            Assert.That(newcomer.Client.EndReason, Does.Contain("full"));

            Run(5f);
            var later = Join(name: "Dee");

            Assert.That(_host.KeptSeats, Is.Zero);
            Assert.That(later.Client.State, Is.EqualTo(CoopClientState.Playing));
            Assert.That(World.Get(later.World.PlayerId).Seat, Is.EqualTo(1));
        }

        [Test]
        public void TheHostVanishes_TheFriendSeesTheLinkStruggle_ThenLost()
        {
            var guest = Join();
            Run(5f);
            Assert.That(guest.Client.IsUnstable, Is.False, "a quiet game is not a lost one: pings keep it heard");

            _hostTransport.GoSilent();
            Run(CoopProtocol.UnstableAfter + 0.5f);

            Assert.That(guest.Client.State, Is.EqualTo(CoopClientState.Playing));
            Assert.That(guest.Client.IsUnstable, Is.True);

            Run(CoopProtocol.ConnectionTimeout);

            Assert.That(guest.Client.EndKind, Is.EqualTo(CoopEndKind.ConnectionLost));
            Assert.That(guest.World.Actors, Is.Empty);
        }

        [Test]
        public void TheHostClosing_IsNotALostLink()
        {
            var guest = Join();

            _host.Dispose();
            Pump();

            Assert.That(guest.Client.EndKind, Is.EqualTo(CoopEndKind.HostLeft));
        }

        [Test]
        public void BrokenOrHostileMessages_DoNotBreakTheGame()
        {
            var guest = Join();
            int id = guest.World.PlayerId;

            // Not a number: ignored, the character stays where it was
            var motion = new CoopMotionMessage();
            motion.Poses.Add((id, new ActorPose(float.NaN, 0f, float.PositiveInfinity, 0f)));
            guest.Network.SendToServer(motion, reliable: false);
            // Bytes that are no message at all
            for (int i = 0; i < 3; i++)
                guest.Transport.Send(0, new byte[] { 1, 2, 3, 4, 5 }, reliable: true);
            Pump();

            Assert.That(_hostMotion.Targets.Where(t => t.Id == id).Select(t => t.Pose), Has.None.Matches<ActorPose>(p => !p.IsFinite));
            Assert.That(World.Get(id), Is.Not.Null, "a few bad messages are survivable");
            Assert.That(guest.Client.State, Is.EqualTo(CoopClientState.Playing));

            // A message claiming a billion entries is refused before anything is allocated
            var stream = new MemoryStream();
            var writer = new BinaryWriter(stream);
            writer.Write(1_000_000_000);
            stream.Position = 0;
            Assert.Throws<InvalidDataException>(() => new CoopMotionMessage().Read(new BinaryReader(stream)));
        }

        [Test]
        public void DifferentGameVersion_IsRefusedWithAReason()
        {
            var guest = Join(Settings(goblinAttack: 99f));

            Assert.That(guest.Client.State, Is.EqualTo(CoopClientState.Ended));
            Assert.That(guest.Client.EndReason, Does.Contain("different version"));
            Assert.That(World.Players, Has.Count.EqualTo(1));
        }

        [Test]
        public void FullParty_IsRefusedWithAReason()
        {
            _host.MaxPlayers = 2;
            var first = Join();
            var second = Join();

            Assert.That(first.Client.State, Is.EqualTo(CoopClientState.Playing));
            Assert.That(second.Client.State, Is.EqualTo(CoopClientState.Ended));
            Assert.That(second.Client.EndReason, Does.Contain("full"));
            Assert.That(World.Players, Has.Count.EqualTo(2));
        }

        [Test]
        public void EightPlayers_AllPlay_SeeEachOther_AndANinthIsRefused()
        {
            _host.MaxPlayers = CoopProtocol.MaxSupportedPlayers;
            var guests = Enumerable.Range(1, 7).Select(i => Join(name: $"P{i}")).ToList();
            var ninth = Join(name: "Late");

            Assert.That(guests.Select(g => g.Client.State), Is.All.EqualTo(CoopClientState.Playing));
            Assert.That(ninth.Client.State, Is.EqualTo(CoopClientState.Ended));
            Assert.That(ninth.Client.EndReason, Does.Contain("full (8 players)"));
            Assert.That(World.Players.Select(id => World.Get(id).Seat), Is.EquivalentTo(Enumerable.Range(0, 8)));

            // Everyone walks; every other machine sees each move
            for (int i = 0; i < guests.Count; i++)
                guests[i].Motion.Poses[guests[i].World.PlayerId] = new ActorPose(i + 1f, 0f, -i, 0f);
            _hostMotion.Poses[World.PlayerId] = new ActorPose(0f, 0f, 9f, 0f);
            Pump();

            for (int i = 0; i < guests.Count; i++)
            {
                var guest = guests[i];
                Assert.That(guest.World.Actors.Count(a => a.IsPlayer), Is.EqualTo(8), $"players seen by P{i + 1}");
                Assert.That(guest.Motion.Targets.Any(t => t.Id == World.PlayerId && t.Pose.Z == 9f), Is.True);
                foreach (var other in guests.Where(g => g != guest))
                {
                    int index = guests.IndexOf(other);
                    Assert.That(guest.Motion.Targets.Any(t => t.Id == other.World.PlayerId && t.Pose.X == index + 1f), Is.True,
                        $"P{i + 1} sees P{index + 1} move");
                }
            }

            // The last to join strikes a goblin; the kill reaches everyone
            int goblin = World.SpawnEnemy(Goblin);
            Pump();
            var last = guests[6];
            for (int round = 0; round < 10 && World.Get(goblin) != null; round++)
            {
                last.World.Attack(last.World.PlayerId, goblin);
                Pump();
            }
            Assert.That(World.Get(goblin), Is.Null);
            Assert.That(guests.Select(g => g.World.Kills), Is.All.EqualTo(1));
        }

        [Test]
        public void NoGameAtThatAddress_SaysSo()
        {
            var guest = new Guest { Bus = new EventBus() };
            guest.Client = new CoopClient(Settings(), Content(), guest.Bus,
                CoopProtocol.CreateNetwork(new LoopbackNetwork().CreateTransport()), null);
            _guests.Add(guest);

            guest.Client.Join("10.0.0.9", Port);
            Pump();

            Assert.That(guest.Client.State, Is.EqualTo(CoopClientState.Ended));
            Assert.That(guest.Client.EndReason, Does.Contain("10.0.0.9"));
        }

        [Test]
        public void NamesAndSeats_ReachEveryone_AndNamesStayUnique()
        {
            World.SetPlayerName(World.PlayerId, "Maks");
            var first = Join(name: "Oleh");
            var second = Join(name: "oleh");

            Assert.That(World.Player.DisplayName, Is.EqualTo("Maks"));
            Assert.That(World.Get(first.World.PlayerId).DisplayName, Is.EqualTo("Oleh"));
            Assert.That(World.Get(second.World.PlayerId).DisplayName, Is.EqualTo("oleh 2"));
            Assert.That(World.Log, Has.Some.EqualTo("Oleh joins the party"));

            foreach (var guest in new[] { first, second })
            {
                var seen = guest.World.Actors.Where(a => a.IsPlayer).OrderBy(a => a.Seat)
                    .Select(a => (a.Seat, a.DisplayName)).ToList();
                Assert.That(seen, Is.EqualTo(new[] { (0, "Maks"), (1, "Oleh"), (2, "oleh 2") }));
            }
            Assert.That(first.World.Player.DisplayName, Is.EqualTo("Oleh"));
            Assert.That(first.World.LivingEnemies, Is.Empty);
        }

        [Test]
        public void Seats_StayWhileOthersLeave_AndAFreedSeatIsReused()
        {
            var first = Join(name: "A");
            var second = Join(name: "B");
            var third = Join(name: "C");
            int secondId = second.World.PlayerId, thirdId = third.World.PlayerId;

            first.Client.Leave();
            Pump();
            var fourth = Join(name: "D");

            Assert.That(World.Get(secondId).Seat, Is.EqualTo(2));
            Assert.That(World.Get(thirdId).Seat, Is.EqualTo(3));
            Assert.That(World.Get(fourth.World.PlayerId).Seat, Is.EqualTo(1));
            Assert.That(World.Player.Seat, Is.EqualTo(0));
            Assert.That(second.World.Get(fourth.World.PlayerId).Seat, Is.EqualTo(1));
        }

        [Test]
        public void Names_AreCleaned_AndAnEmptyNameGetsANumber()
        {
            var noName = Join(name: "   ");
            var longName = Join(name: " \tA very long name that goes on\n");

            Assert.That(World.Get(noName.World.PlayerId).DisplayName, Is.EqualTo("Player 2"));
            string cleaned = World.Get(longName.World.PlayerId).DisplayName;
            Assert.That(cleaned, Is.EqualTo("A very long name"));
            Assert.That(cleaned.Length, Is.LessThanOrEqualTo(ActorWorld.MaxNameLength));
            Assert.That(World.Player.DisplayName, Is.EqualTo("Hero"), "playing alone the first player has the character's name");
        }

        [Test]
        public void ThroughASessionProvider_TheSameGamePlays()
        {
            // The co-op does not care how players connect: here a provider hands over ready transports
            var provider = new LoopbackSessionProvider(_network, firstPort: 9000);
            var settings = Settings();
            var content = Content();
            var session = provider.HostAsync(new SessionOptions()).GetAwaiter().GetResult();
            var host = new CoopHost(World, _game.Container.Resolve<IEventBus>(), CoopProtocol.CreateNetwork(session.Transport),
                null, CoopProtocol.Fingerprint(settings, content));
            host.Open();
            var guest = new Guest { Bus = new EventBus() };
            guest.Network = CoopProtocol.CreateNetwork(provider.JoinAsync(session.JoinCode).GetAwaiter().GetResult());
            guest.Client = new CoopClient(settings, content, guest.Bus, guest.Network, null) { PlayerName = "Ira" };
            _guests.Add(guest);
            guest.Client.JoinSession(session.JoinCode);
            for (int round = 0; round < 4; round++)
            {
                host.Update(0.06f);
                guest.Client.Update(0.06f);
            }

            Assert.That(guest.Client.State, Is.EqualTo(CoopClientState.Playing));
            Assert.That(guest.World.Player.DisplayName, Is.EqualTo("Ira"));
            host.Dispose();
        }

        [Test]
        public void Fingerprint_IsStable_AndChangesWithTheRules()
        {
            Assert.That(CoopProtocol.Fingerprint(Settings(), Content()), Is.EqualTo(CoopProtocol.Fingerprint(Settings(), Content())));
            Assert.That(CoopProtocol.Fingerprint(Settings(7f), Content()), Is.Not.EqualTo(CoopProtocol.Fingerprint(Settings(), Content())));
            var faster = Settings();
            faster.Player.MoveSpeed = 9f;
            Assert.That(CoopProtocol.Fingerprint(faster, Content()), Is.Not.EqualTo(CoopProtocol.Fingerprint(Settings(), Content())));
            var pvp = Settings();
            pvp.Pvp = new PvpSettings();
            Assert.That(CoopProtocol.Fingerprint(pvp, Content()), Is.Not.EqualTo(CoopProtocol.Fingerprint(Settings(), Content())));
        }

        [Test]
        public void PartyColours_ComeFromThePalette_ThenEverySeatGetsItsOwn()
        {
            var palette = PlayerTagView.DefaultPalette();
            var colours = Enumerable.Range(0, CoopProtocol.MaxSupportedPlayers).Select(seat => PlayerTagView.ColorOf(seat, palette)).ToList();

            Assert.That(colours.Take(palette.Length), Is.EqualTo(palette));
            for (int a = 0; a < colours.Count; a++)
                for (int b = a + 1; b < colours.Count; b++)
                {
                    var difference = colours[a] - colours[b];
                    float distance = UnityEngine.Mathf.Abs(difference.r) + UnityEngine.Mathf.Abs(difference.g) + UnityEngine.Mathf.Abs(difference.b);
                    Assert.That(distance, Is.GreaterThan(0.25f), $"seats {a} and {b} look alike");
                }
            Assert.That(PlayerTagView.ColorOf(3, null), Is.Not.EqualTo(UnityEngine.Color.white), "no palette still colours seats");
        }
    
        private sealed class Checkpoints : IRespawnPointProvider
        {
            public readonly ActorPose Point = new ActorPose(100f, 1f, 0f, 90f);
            public readonly List<(int PlayerId, int Seat, RespawnReason Reason, ActorPose? Last)> Asked =
                new List<(int, int, RespawnReason, ActorPose?)>();

            public bool TryGetRespawnPoint(int playerId, int seat, RespawnReason reason, ActorPose? lastPose, out ActorPose pose)
            {
                Asked.Add((playerId, seat, reason, lastPose));
                pose = Point;
                return true;
            }
        }

        private sealed class HeadshotCalculator : RPGFramework.Combat.DamageCalculator
        {
            public override float CalculateDamage(float baseDamage, float defenseValue, RPGFramework.Combat.AttackContext context) =>
                context.HasAny(1) ? 20f : 5f;
        }

        [Test]
        public void FriendsAttack_CarriesTagsAndSource_TheHostMayRefuseAClaim()
        {
            PlayWith(() =>
            {
                var settings = Settings();
                settings.Enemies[0].MaxHealth = 1000f;
                settings.DamageCalculator = new HeadshotCalculator();
                return settings;
            });
            _host.AcceptAttack = (player, target, context) => !context.HasAny(4);
            var guest = Join();
            int goblin = World.SpawnEnemy(Goblin);
            Pump();

            guest.World.Attack(guest.World.PlayerId, goblin, new RPGFramework.Combat.AttackContext(tags: 1, sourceId: 9));
            Pump();

            Assert.That(World.Get(goblin).Health, Is.EqualTo(980f), "the host's calculator saw the head tag");
            var hit = guest.Damaged.Single(d => d.TargetId == goblin);
            Assert.That(hit.Context.Tags, Is.EqualTo(1));
            Assert.That(hit.Context.SourceId, Is.EqualTo(9));
            Assert.That(hit.Context.FromEffect, Is.False);

            guest.World.Attack(guest.World.PlayerId, goblin, new RPGFramework.Combat.AttackContext(tags: 4));
            Pump();
            Assert.That(World.Get(goblin).Health, Is.EqualTo(980f), "the game refused that claim");

            World.DealDamage(World.PlayerId, goblin, 3f, RPGFramework.Combat.AttackContext.Effect(5));
            Pump();
            Assert.That(guest.Damaged.Last().Context.FromEffect, Is.True, "effect damage is shown as such");
        }

        [Test]
        public void ABoost_LetsAFriendRunFaster_ForItsTimeOnly()
        {
            PlayWith(() =>
            {
                var settings = Settings();
                settings.Player.MoveSpeed = 5f;
                return settings;
            });
            var guest = Join();
            int friend = guest.World.PlayerId;
            var pose = new ActorPose(0f, 0f, 0f, 0f);
            guest.Motion.Poses[friend] = pose;
            Run(0.3f);
            Assert.That(_host.SetAllowedSpeedMultiplier(friend, 3f, 1f), Is.True);
            Assert.That(_host.AllowedSpeedMultiplier(friend), Is.EqualTo(3f));
            Assert.That(_host.SetAllowedSpeedMultiplier(World.PlayerId, 3f, 1f), Is.False, "the host moves itself");

            // 14 units a second: too fast on foot, fine with the boost
            for (int step = 0; step < 8; step++)
            {
                pose.X += 1.4f;
                guest.Motion.Poses[friend] = pose;
                Run(0.1f);
            }
            Assert.That(_host.MovementCorrections, Is.EqualTo(0));

            Run(1f);
            Assert.That(_host.AllowedSpeedMultiplier(friend), Is.EqualTo(1f));
            for (int step = 0; step < 8; step++)
            {
                pose.X += 1.4f;
                guest.Motion.Poses[friend] = pose;
                Run(0.1f);
            }
            Assert.That(_host.MovementCorrections, Is.GreaterThan(0), "the boost is over");
        }

        [Test]
        public void PosesSentBeforeATeleportArrived_AreNotCorrected()
        {
            PlayWith(() =>
            {
                var settings = Settings();
                settings.Player.MoveSpeed = 5f;
                return settings;
            });
            var guest = Join();
            int friend = guest.World.PlayerId;
            guest.Motion.Poses[friend] = new ActorPose(0f, 0f, 0f, 0f);
            Run(0.3f);

            _host.Teleport(friend, new ActorPose(50f, 0f, 0f, 0f));
            // The friend has not heard yet and still reports the old place
            guest.Client.SendMotion();
            _host.Update(0.01f);

            Assert.That(_host.MovementCorrections, Is.Zero);
            Assert.That(_hostMotion.Poses[friend].X, Is.EqualTo(50f));

            Run(_host.SettleTime + 0.5f);
            Assert.That(guest.Motion.Poses[friend].X, Is.EqualTo(50f));
            Assert.That(_host.MovementCorrections, Is.Zero);
        }

        [Test]
        public void Rejoin_AndRevive_GoToTheGamesRespawnPoint()
        {
            var checkpoints = new Checkpoints();
            _host.RespawnPoints = checkpoints;
            var ann = Join(name: "Ann");
            int annId = ann.World.PlayerId;
            ann.Motion.Poses[annId] = new ActorPose(3f, 0f, 4f, 0f);
            Run(0.3f);
            string token = ann.Client.RejoinToken;

            ann.Transport.GoSilent();
            Run(CoopProtocol.ConnectionTimeout + 0.5f);
            var back = Join(name: "Ann", rejoinToken: token);
            Pump();

            int backId = back.World.PlayerId;
            var asked = checkpoints.Asked.Single();
            Assert.That(asked.PlayerId, Is.EqualTo(backId));
            Assert.That(asked.Seat, Is.EqualTo(1));
            Assert.That(asked.Reason, Is.EqualTo(RespawnReason.Rejoin));
            Assert.That(asked.Last.HasValue && asked.Last.Value.X == 3f && asked.Last.Value.Z == 4f, Is.True,
                "the game learns where they were");
            Assert.That(_hostMotion.Teleports, Has.Member((backId, checkpoints.Point)));
            Assert.That(back.Motion.Teleports, Has.Member((backId, checkpoints.Point)));

            int goblin = World.SpawnEnemy(Goblin);
            for (int round = 0; round < 200 && World.Get(backId).IsAlive(); round++)
                World.Attack(goblin, backId);
            Pump();
            back.World.RevivePlayer();
            Pump();

            Assert.That(checkpoints.Asked.Last().Reason, Is.EqualTo(RespawnReason.Revive));
            Assert.That(back.Motion.Teleports.Count(t => t.Id == backId), Is.EqualTo(2));
        }

        [Test]
        public void WithoutRespawnPoints_ARejoiningPlayerIsNotMoved()
        {
            var ann = Join(name: "Ann");
            string token = ann.Client.RejoinToken;
            ann.Transport.GoSilent();
            Run(CoopProtocol.ConnectionTimeout + 0.5f);

            var back = Join(name: "Ann", rejoinToken: token);
            Pump();

            Assert.That(back.Client.Rejoined, Is.True);
            Assert.That(back.Motion.Teleports, Is.Empty);
        }
    
        [Test]
        public void EffectsAndCooldowns_ReachFriends_AndFriendsUseAbilitiesThroughTheHost()
        {
            _powers = true;
            PlayWith(() => Settings());
            int goblin = World.SpawnEnemy(Goblin);
            World.Effects.Apply(goblin, "poison", World.PlayerId);
            var bolts = new List<(int Caster, int Target)>();
            World.Abilities.SetEffect("bolt", (caster, target) => bolts.Add((caster, target)));

            var guest = Join();
            var applied = new List<RPGFramework.StatusEffects.EffectAppliedEvent>();
            var removed = new List<RPGFramework.StatusEffects.EffectRemovedEvent>();
            guest.Bus.Subscribe<RPGFramework.StatusEffects.EffectAppliedEvent>(applied.Add);
            guest.Bus.Subscribe<RPGFramework.StatusEffects.EffectRemovedEvent>(removed.Add);
            Pump();

            Assert.That(guest.World.EffectsOn(goblin).Select(e => e.EffectId), Is.EqualTo(new[] { "poison" }),
                "a friend joining late sees effects already on");
            Assert.That(guest.World.EffectsOn(goblin).Single().SourceId, Is.EqualTo(World.PlayerId));

            int friend = guest.World.PlayerId;
            World.Effects.Apply(friend, "haste");
            Pump();
            Assert.That(applied.Select(a => a.EffectId), Has.Member("haste"));
            Assert.That(guest.World.Player.MoveMultiplier, Is.EqualTo(2f));

            World.Effects.Remove(friend, "haste");
            Pump();
            Assert.That(removed.Select(r => r.EffectId), Is.EqualTo(new[] { "haste" }));
            Assert.That(guest.World.EffectsOn(friend), Is.Empty);
            Assert.That(guest.World.Player.MoveMultiplier, Is.EqualTo(1f));

            Assert.That(guest.World.UseAbility("bolt", goblin), Is.True);
            Pump();
            Assert.That(bolts, Is.EqualTo(new[] { (friend, goblin) }), "the host ran it for the friend");
            Assert.That(guest.World.CooldownLeft("bolt"), Is.GreaterThan(2.5f).And.LessThanOrEqualTo(3f));
            Assert.That(guest.World.UseAbility("bolt", goblin), Is.False, "the friend sees the cooldown too");

            Run(3.2f);
            Assert.That(guest.World.CooldownLeft("bolt"), Is.Zero);
        }

        [Test]
        public void AHasteEffect_RaisesTheSpeedTheHostAllows()
        {
            _powers = true;
            PlayWith(() =>
            {
                var settings = Settings();
                settings.Player.MoveSpeed = 5f;
                return settings;
            });
            var guest = Join();
            int friend = guest.World.PlayerId;
            var pose = new ActorPose(0f, 0f, 0f, 0f);
            guest.Motion.Poses[friend] = pose;
            Run(0.3f);
            World.Effects.Apply(friend, "haste");

            // 15 units a second: too fast on foot, fine at twice the speed
            for (int step = 0; step < 8; step++)
            {
                pose.X += 1.5f;
                guest.Motion.Poses[friend] = pose;
                Run(0.1f);
            }

            Assert.That(_host.MovementCorrections, Is.Zero);
        }
    
        [Test]
        public void GameMessages_GoBothWays_OnlyTheWayTheyMay()
        {
            var votes = new List<(int Player, int Choice)>();
            _host.On<VoteMessage>((player, vote) => votes.Add((player, vote.Choice)));
            var guest = Join();
            var seeds = new List<int>();
            guest.Client.On<SeedMessage>(seed => seeds.Add(seed.Seed));

            Assert.That(guest.Client.Send(new VoteMessage { Choice = 2 }), Is.True);
            _host.SendToAll(new SeedMessage { Seed = 42 });
            Assert.That(_host.Send(guest.World.PlayerId, new SeedMessage { Seed = 7 }), Is.True);
            Assert.That(_host.Send(World.PlayerId, new SeedMessage { Seed = 1 }), Is.False, "the host is not a guest");
            Pump();

            Assert.That(votes, Is.EqualTo(new[] { (guest.World.PlayerId, 2) }), "the host knows who voted");
            Assert.That(seeds, Is.EqualTo(new[] { 42, 7 }));

            // Only the host may send a seed: a client refuses to send one at all
            Assert.That(() => guest.Client.Send(new SeedMessage { Seed = 666 }), Throws.InvalidOperationException);

            Assert.That(() => new CoopGameMessages().Add<VoteMessage>(CoopProtocol.HelloId, MessageAuthority.Client),
                Throws.TypeOf<System.ArgumentOutOfRangeException>(), "the framework's ids are taken");
            Assert.That(() => new CoopGameMessages().Add<VoteMessage>(CoopProtocol.FirstGameMessageId + 5, MessageAuthority.Client),
                Throws.ArgumentException, "the id must be the message's own");
        }

        [Test]
        public void OwnerDefense_TheFriendsMachineDodgesAndBlocks_SilenceLands_ForgeriesAreRejected()
        {
            _host.DefenseAuthority = DefenseAuthority.Owner;
            _host.DefenseWindow = 1f;
            var resolved = new List<DefenseResolvedEvent>();
            _game.Container.Resolve<IEventBus>().Subscribe<DefenseResolvedEvent>(resolved.Add);
            var guest = Join();
            var incoming = new List<IncomingAttackEvent>();
            guest.Bus.Subscribe<IncomingAttackEvent>(incoming.Add);
            int friend = guest.World.PlayerId;
            int goblin = World.SpawnEnemy(Goblin);
            Pump();
            float full = World.Get(friend).Health;

            // Dodged on the friend's own machine
            Assert.That(World.Attack(goblin, friend), Is.Zero, "the host waits for the friend's answer");
            Assert.That(_host.PendingAttacks, Is.EqualTo(1));
            Pump();
            var attack = incoming.Single();
            Assert.That(attack.AttackerId, Is.EqualTo(goblin));
            Assert.That(guest.Client.Defend(attack.AttackId, DefenseOutcome.Dodged), Is.True);
            Assert.That(guest.Client.Defend(attack.AttackId, DefenseOutcome.Dodged), Is.False, "answered once");
            Pump();
            Assert.That(World.Get(friend).Health, Is.EqualTo(full));
            Assert.That(resolved.Single().Outcome, Is.EqualTo(DefenseOutcome.Dodged));
            Assert.That(resolved.Single().Answered, Is.True);

            // Blocked: half of it lands
            World.Attack(goblin, friend);
            Pump();
            guest.Client.Defend(incoming.Last().AttackId, DefenseOutcome.Blocked, 0.5f);
            Pump();
            float halfHit = full - World.Get(friend).Health;
            Assert.That(halfHit, Is.GreaterThan(0f));

            // No answer: it lands in full once the window is over
            float before = World.Get(friend).Health;
            World.Attack(goblin, friend);
            Run(1.3f);
            Assert.That(before - World.Get(friend).Health, Is.EqualTo(halfHit * 2f).Within(0.01f));
            Assert.That(resolved.Last().Answered, Is.False);
            Assert.That(resolved.Last().Outcome, Is.EqualTo(DefenseOutcome.Hit));
            Assert.That(_host.PendingAttacks, Is.Zero);

            // An answer to an attack that never was, or came too late
            guest.Network.SendToServer(new CoopDefenseResultMessage { AttackId = 999, Outcome = DefenseOutcome.Dodged });
            guest.Network.SendToServer(new CoopDefenseResultMessage { AttackId = incoming.Last().AttackId, Outcome = DefenseOutcome.Dodged });
            Pump();
            Assert.That(_host.RejectedDefenseResults, Is.EqualTo(2));

            // The host's own player is defended by the host's rules at once
            Assert.That(World.Attack(goblin, World.PlayerId), Is.GreaterThan(0f));
        }

        [Test]
        public void AnnouncedAttack_LandsWhenItsWindUpIsOver()
        {
            var guest = Join();
            int friend = guest.World.PlayerId;
            int goblin = World.SpawnEnemy(Goblin);
            float full = World.Get(friend).Health;

            _host.AnnounceAttack(goblin, friend, 1f, new RPGFramework.Combat.AttackContext(tags: 2));
            Run(0.5f);
            Assert.That(World.Get(friend).Health, Is.EqualTo(full));

            Run(0.7f);
            Assert.That(World.Get(friend).Health, Is.LessThan(full));
            Assert.That(guest.Damaged.Last().Context.Tags, Is.EqualTo(2));
        }
    
        [Test]
        public void PerPlayer_TheFriendBringsTheirProgress_SeesTheirOwn_AndComesBackWithIt()
        {
            PlayWith(() =>
            {
                var settings = Settings();
                settings.ProgressMode = ProgressMode.PerPlayer;
                return settings;
            });
            var brought = new PlayerProgress { Level = 3 };
            brought.Items.Add(Ear);
            var guest = Join(name: "Ann", profile: brought);
            int friend = guest.World.PlayerId;

            Assert.That(World.LevelOf(friend), Is.EqualTo(3));
            Assert.That(guest.World.Level, Is.EqualTo(3), "the friend sees their own level");
            Assert.That(World.Level, Is.EqualTo(1));
            Assert.That(guest.World.Inventory.GetAllItems().Select(i => i.ItemId), Is.EqualTo(new[] { Ear }));

            int goblin = World.SpawnEnemy(Goblin);
            for (int round = 0; round < 10 && World.Get(goblin) != null; round++)
            {
                guest.World.Attack(friend, goblin);
                Pump();
            }
            Assert.That(guest.World.TotalExperience, Is.EqualTo(World.ExperienceOf(friend)).And.GreaterThan(0));
            Assert.That(World.TotalExperience, Is.Zero);
            Assert.That(guest.World.Inventory.Count, Is.EqualTo(2), "the kill's loot is theirs");

            // What they take home
            var home = guest.World.CaptureProgress();
            Assert.That(home.Level, Is.EqualTo(3));
            Assert.That(home.TotalExperience, Is.EqualTo(World.ExperienceOf(friend)));
            Assert.That(home.Items, Is.EqualTo(new[] { Ear, Ear }));

            // A lost link: they come back with it, whatever their client brings
            string token = guest.Client.RejoinToken;
            guest.Transport.GoSilent();
            Run(CoopProtocol.ConnectionTimeout + 0.5f);
            Assert.That(guest.World.LastProgress.TotalExperience, Is.EqualTo(home.TotalExperience),
                "the last report is kept after the link is gone");
            var back = Join(name: "Ann", rejoinToken: token, profile: new PlayerProgress());
            Assert.That(World.LevelOf(back.World.PlayerId), Is.EqualTo(3));
            Assert.That(World.InventoryOf(back.World.PlayerId).Count, Is.EqualTo(2));
            Assert.That(back.World.CaptureProgress().TotalExperience, Is.EqualTo(home.TotalExperience));
        }
    
        [Test]
        public void SceneChange_FriendsAreTold_AndSoAreLateJoiners_TheGameGoesOn()
        {
            var guest = Join(name: "Ann");
            var scenes = new List<string>();
            guest.Client.SceneChanged += scenes.Add;
            Assert.That(guest.Client.Scene, Is.Empty, "the host named no scene");
            int friend = guest.World.PlayerId;

            _host.ChangeScene("Run");
            Pump();

            Assert.That(scenes, Is.EqualTo(new[] { "Run" }));
            Assert.That(guest.Client.Scene, Is.EqualTo("Run"));
            Assert.That(_host.Scene, Is.EqualTo("Run"));
            Assert.That(guest.Client.State, Is.EqualTo(CoopClientState.Playing), "the link stays");
            Assert.That(guest.World.PlayerId, Is.EqualTo(friend));
            Assert.That(World.Get(friend), Is.Not.Null, "the party stays");

            var late = Join(name: "Bo");
            Pump();
            Assert.That(late.Client.Scene, Is.EqualTo("Run"), "a late joiner plays where the game is");

            Assert.That(() => guest.Network.SendToServer(new CoopSceneMessage { Scene = "Hub" }),
                Throws.InvalidOperationException, "only the host moves the game");
            Assert.That(() => _host.ChangeScene(""), Throws.ArgumentException);
        }
    }
}
