using System.Collections.Generic;
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
    /// away are handled, and a different game version or a full party is refused with a reason.
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
        }

        private sealed class Guest
        {
            public CoopClient Client;
            public EventBus Bus;
            public Motion Motion;
            public NetworkManager Network;
            public readonly List<ActorSpawnedEvent> Spawned = new List<ActorSpawnedEvent>();
            public readonly List<ActorDespawnedEvent> Despawned = new List<ActorDespawnedEvent>();
            public readonly List<ActorDamagedEvent> Damaged = new List<ActorDamagedEvent>();
            public RemoteWorld World => Client.World;
        }

        private Logger _logger;
        private LoopbackNetwork _network;
        private GameHost _game;
        private ActionRpgModule _module;
        private CoopHost _host;
        private Motion _hostMotion;
        private readonly List<Guest> _guests = new List<Guest>();

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

        private static ActionRpgContent Content()
        {
            var content = new ActionRpgContent();
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
            _network = new LoopbackNetwork();
            _game = new GameHost(_logger);
            var settings = Settings();
            var content = Content();
            _game.Start(builder => _module = ActionRpgComposition.Configure(builder, settings, content));
            _hostMotion = new Motion();
            _host = new CoopHost(World, _game.Container.Resolve<IEventBus>(), CoopProtocol.CreateNetwork(_network.CreateTransport()),
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

        private Guest Join(ActionRpgSettings settings = null)
        {
            var guest = new Guest { Bus = new EventBus(), Motion = new Motion() };
            guest.Bus.Subscribe<ActorSpawnedEvent>(guest.Spawned.Add);
            guest.Bus.Subscribe<ActorDespawnedEvent>(guest.Despawned.Add);
            guest.Bus.Subscribe<ActorDamagedEvent>(guest.Damaged.Add);
            guest.Network = CoopProtocol.CreateNetwork(_network.CreateTransport());
            guest.Client = new CoopClient(settings ?? Settings(), Content(), guest.Bus, guest.Network, guest.Motion);
            _guests.Add(guest);
            guest.Client.Join("127.0.0.1", Port);
            Pump();
            return guest;
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
        public void Fingerprint_IsStable_AndChangesWithTheRules()
        {
            Assert.That(CoopProtocol.Fingerprint(Settings(), Content()), Is.EqualTo(CoopProtocol.Fingerprint(Settings(), Content())));
            Assert.That(CoopProtocol.Fingerprint(Settings(7f), Content()), Is.Not.EqualTo(CoopProtocol.Fingerprint(Settings(), Content())));
        }
    }
}
