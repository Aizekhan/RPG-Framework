using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RPGFramework.Bootstrap;
using RPGFramework.Events;
using RPGFramework.Factions;
using RPGFramework.Logging;

namespace RPGFramework.ActionRpg.Tests
{
    /// <summary>
    /// PvP as a mode, through the real modules: hostility decides who may hit whom, players get sides, the match
    /// scores defeats, respawns, plays Duel rounds, ends (by score, time, or forfeit) and starts again.
    /// </summary>
    public sealed class PvpTests
    {
        private Logger _logger;
        private GameHost _host;
        private ActionRpgModule _module;
        private ActionRpgSettings _settings;
        private readonly List<IEvent> _events = new List<IEvent>();

        private ActorWorld World => _module.World;
        private PvpMatch Match => _module.Pvp;

        [SetUp]
        public void SetUp()
        {
            _logger = new Logger(LogSeverity.Warning, new MemoryLogSink(256));
            _host = new GameHost(_logger);
            _settings = new ActionRpgSettings
            {
                Player = new CharacterData { Id = "hero", DisplayName = "Hero", MaxHealth = 100f, Attack = 50f, Defense = 5f },
                Waves = new WaveSettings { Enabled = false },
                Seed = 3
            };
            _settings.Enemies.Add(new CharacterData { Id = "goblin", DisplayName = "Goblin", MaxHealth = 30f, Attack = 6f, Defense = 2f });
            _events.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            _host.Dispose();
            _logger.Dispose();
        }

        private void StartGame(PvpSettings pvp)
        {
            _settings.Pvp = pvp;
            _host.Start(builder => _module = ActionRpgComposition.Configure(builder, _settings));
            var bus = _host.Container.Resolve<IEventBus>();
            bus.Subscribe<ActorTeamChangedEvent>(e => _events.Add(e));
            bus.Subscribe<PlayerDefeatedEvent>(e => _events.Add(e));
            bus.Subscribe<PvpRoundStartedEvent>(e => _events.Add(e));
            bus.Subscribe<PvpScoreChangedEvent>(e => _events.Add(e));
            bus.Subscribe<PvpRoundEndedEvent>(e => _events.Add(e));
            bus.Subscribe<PvpMatchEndedEvent>(e => _events.Add(e));
        }

        private void Tick(float seconds)
        {
            for (float time = 0f; time < seconds - 0.001f; time += 0.1f)
                _host.Update(0.1f);
        }

        private void Defeat(int attacker, int target)
        {
            for (int hit = 0; hit < 50 && World.Get(target).IsAlive(); hit++)
                World.Attack(attacker, target);
            Assert.That(World.Get(target).IsDead(), Is.True, "the blow should land");
        }

        private IEnumerable<T> Events<T>() => _events.OfType<T>();

        [Test]
        public void WithoutPvp_PlayersCannotHurtEachOther_EnemiesStillCan()
        {
            StartGame(null);
            int friend = World.AddPlayer(name: "Sam");
            int goblin = World.SpawnEnemy("goblin");

            Assert.That(World.Attack(World.PlayerId, friend), Is.EqualTo(0f));
            Assert.That(World.AreHostile(World.PlayerId, friend), Is.False);
            Assert.That(World.Get(friend).HealthFraction, Is.EqualTo(1f));
            Assert.That(World.Attack(goblin, friend), Is.GreaterThan(0f));
            Assert.That(World.Attack(World.PlayerId, goblin), Is.GreaterThan(0f));
            Assert.That(World.AreHostile(World.PlayerId, World.PlayerId), Is.False);

            Tick(1f);
            Assert.That(Match.Phase, Is.EqualTo(PvpPhase.Waiting), "no match without PvP settings");
            Assert.That(World.Player.Team, Is.Null);
        }

        [Test]
        public void Teams_AreBalanced_AndOnlyOpponentsFight()
        {
            StartGame(new PvpSettings { Mode = PvpMode.TeamDeathmatch });
            int second = World.AddPlayer();
            int third = World.AddPlayer();
            int goblin = World.SpawnEnemy("goblin");
            Tick(0.1f);

            Assert.That(World.Player.Team, Is.EqualTo("Red"));
            Assert.That(World.Get(second).Team, Is.EqualTo("Blue"));
            Assert.That(World.Get(third).Team, Is.EqualTo("Red"));
            Assert.That(Events<ActorTeamChangedEvent>().Count(), Is.EqualTo(3));
            Assert.That(Match.Phase, Is.EqualTo(PvpPhase.Playing));
            Assert.That(Match.Sides, Is.EquivalentTo(new[] { "Red", "Blue" }));

            Assert.That(World.Attack(World.PlayerId, third), Is.EqualTo(0f), "no friendly fire");
            Assert.That(World.Attack(World.PlayerId, second), Is.GreaterThan(0f));
            Assert.That(World.AreHostile(goblin, second), Is.True, "enemies without a team fight every player");
            Assert.That(World.AreHostile(World.PlayerId, goblin), Is.True);
        }

        [Test]
        public void TeamDeathmatch_ScoresDefeats_RespawnsAfterTheDelay_AndEndsAtTheScore()
        {
            StartGame(new PvpSettings { Mode = PvpMode.TeamDeathmatch, ScoreToWin = 2, RespawnDelay = 1f, RestartDelay = 0f });
            int blue = World.AddPlayer(name: "Sam");
            Tick(0.1f);

            Defeat(World.PlayerId, blue);
            var defeat = Events<PlayerDefeatedEvent>().Single();
            Assert.That(defeat.PlayerId, Is.EqualTo(blue));
            Assert.That(defeat.ByPlayer, Is.True);
            Assert.That(Match.ScoreOf("Red"), Is.EqualTo(1));
            Assert.That(Events<PvpScoreChangedEvent>().Single().Side, Is.EqualTo("Red"));

            Tick(0.5f);
            Assert.That(World.Get(blue).IsDead(), Is.True, "still waiting to respawn");
            Tick(0.6f);
            Assert.That(World.Get(blue).IsAlive(), Is.True);
            Assert.That(World.Get(blue).HealthFraction, Is.EqualTo(1f));

            Defeat(World.PlayerId, blue);
            Assert.That(Match.Phase, Is.EqualTo(PvpPhase.Finished));
            Assert.That(Match.Winner, Is.EqualTo("Red"));
            var ended = Events<PvpMatchEndedEvent>().Single();
            Assert.That(ended.Winner, Is.EqualTo("Red"));
            Assert.That(ended.Forfeit, Is.False);
            Tick(0.1f);
            Assert.That(World.Get(blue).IsAlive(), Is.True, "everyone stands up when the match ends");
        }

        [Test]
        public void ThePlayerCannotReviveThemselvesInPvp()
        {
            StartGame(new PvpSettings { Mode = PvpMode.TeamDeathmatch, RespawnDelay = 5f });
            int blue = World.AddPlayer();
            Tick(0.1f);
            Defeat(blue, World.PlayerId);

            Assert.That(World.RevivePlayer(), Is.False);
            Assert.That(World.Player.IsDead(), Is.True);
        }

        [Test]
        public void Duel_IsPlayedInRounds()
        {
            StartGame(new PvpSettings { Mode = PvpMode.Duel, RoundsToWin = 2, RoundBreak = 1f, RestartDelay = 0f });
            int rival = World.AddPlayer(name: "Rival");
            Tick(0.1f);
            Assert.That(Match.Round, Is.EqualTo(1));

            Defeat(rival, World.PlayerId);
            Assert.That(Match.Phase, Is.EqualTo(PvpPhase.RoundOver));
            Assert.That(Match.RoundWinsOf("Blue"), Is.EqualTo(1));
            var roundEnd = Events<PvpRoundEndedEvent>().Single();
            Assert.That(roundEnd.Winner, Is.EqualTo("Blue"));
            Assert.That(roundEnd.RoundsWon, Is.EqualTo(1));

            Tick(0.5f);
            Assert.That(World.Player.IsDead(), Is.True, "nobody respawns within a duel");
            Tick(0.6f);
            Assert.That(Match.Round, Is.EqualTo(2));
            Assert.That(Match.Phase, Is.EqualTo(PvpPhase.Playing));
            Assert.That(World.Player.IsAlive(), Is.True);
            Assert.That(World.Get(rival).HealthFraction, Is.EqualTo(1f), "both start a round at full health");
            Assert.That(Events<PvpRoundStartedEvent>().Select(e => e.Round), Is.EqualTo(new[] { 1, 2 }));

            Defeat(World.PlayerId, rival);
            Tick(1.1f);
            Defeat(rival, World.PlayerId);
            Assert.That(Match.Phase, Is.EqualTo(PvpPhase.Finished));
            Assert.That(Match.Winner, Is.EqualTo("Blue"));
            Assert.That(Match.Round, Is.EqualTo(3));
        }

        [Test]
        public void Duel_TwoOnTwo_RoundEndsOnlyWhenAWholeSideIsDown()
        {
            StartGame(new PvpSettings { Mode = PvpMode.Duel, RoundsToWin = 1 });
            int blueA = World.AddPlayer();
            World.AddPlayer();
            int blueB = World.AddPlayer();
            Tick(0.1f);

            Defeat(World.PlayerId, blueA);
            Assert.That(Match.Phase, Is.EqualTo(PvpPhase.Playing));
            Defeat(World.PlayerId, blueB);
            Assert.That(Match.Phase, Is.EqualTo(PvpPhase.Finished));
            Assert.That(Match.Winner, Is.EqualTo("Red"));
        }

        [Test]
        public void FreeForAll_EveryoneIsTheirOwnSide()
        {
            StartGame(new PvpSettings { Mode = PvpMode.FreeForAll, ScoreToWin = 1 });
            int second = World.AddPlayer();
            int third = World.AddPlayer();
            Tick(0.1f);

            Assert.That(World.Player.Team, Is.EqualTo("P1"));
            Assert.That(World.Get(second).Team, Is.EqualTo("P2"));
            Assert.That(World.Get(third).Team, Is.EqualTo("P3"));
            Assert.That(World.AreHostile(second, third), Is.True);

            Defeat(third, second);
            Assert.That(Match.Winner, Is.EqualTo("P3"));
        }

        [Test]
        public void WhenTheOtherSideLeaves_TheMatchIsForfeited()
        {
            StartGame(new PvpSettings { Mode = PvpMode.TeamDeathmatch, RestartDelay = 0f });
            int blue = World.AddPlayer();
            Tick(0.1f);
            Assert.That(Match.Phase, Is.EqualTo(PvpPhase.Playing));

            World.RemovePlayer(blue);
            Tick(0.1f);

            Assert.That(Match.Phase, Is.EqualTo(PvpPhase.Finished));
            Assert.That(Match.Winner, Is.EqualTo("Red"));
            Assert.That(Events<PvpMatchEndedEvent>().Single().Forfeit, Is.True);
        }

        [Test]
        public void TimeLimit_HighestScoreWins_ATieIsADraw()
        {
            StartGame(new PvpSettings { Mode = PvpMode.TeamDeathmatch, TimeLimit = 2f, RespawnDelay = 0.1f, RestartDelay = 0f });
            int blue = World.AddPlayer();
            Tick(0.1f);
            Assert.That(Match.TimeLeft, Is.EqualTo(2f).Within(0.01f));

            Tick(2.1f);
            Assert.That(Match.Phase, Is.EqualTo(PvpPhase.Finished));
            Assert.That(Match.Winner, Is.Null, "0:0 is a draw");

            Match.Restart();
            Tick(0.1f);
            Defeat(blue, World.PlayerId);
            Tick(2.1f);
            Assert.That(Match.Winner, Is.EqualTo("Blue"));
        }

        [Test]
        public void AFinishedMatch_StartsAgainAfterTheRestartDelay()
        {
            StartGame(new PvpSettings { Mode = PvpMode.FreeForAll, ScoreToWin = 1, RestartDelay = 1f });
            int second = World.AddPlayer();
            Tick(0.1f);
            Defeat(World.PlayerId, second);
            Assert.That(Match.Phase, Is.EqualTo(PvpPhase.Finished));

            Tick(1.2f);
            Assert.That(Match.Phase, Is.EqualTo(PvpPhase.Playing));
            Assert.That(Match.ScoreOf("P1"), Is.EqualTo(0));
            Assert.That(Match.Winner, Is.Null);
        }

        [Test]
        public void Factions_AtWarFight_AlliesDoNot()
        {
            var factions = new FactionRegistry();
            factions.RegisterFaction(new Faction("order", "Order", null, FactionType.Kingdom));
            factions.RegisterFaction(new Faction("chaos", "Chaos", null, FactionType.Kingdom));
            factions.RegisterFaction(new Faction("guild", "Guild", null, FactionType.Guild));
            factions.RegisterRelationship(new FactionRelationship("order", "chaos", FactionRelationType.Enemy));
            factions.RegisterRelationship(new FactionRelationship("order", "guild", FactionRelationType.Allied));
            _settings.Hostility = new FactionHostility(factions);
            var pvp = new PvpSettings { Mode = PvpMode.TeamDeathmatch };
            pvp.Teams.Clear();
            pvp.Teams.AddRange(new[] { "order", "chaos", "guild" });
            StartGame(pvp);
            int chaos = World.AddPlayer();
            int guild = World.AddPlayer();
            Tick(0.1f);

            Assert.That(World.Player.Team, Is.EqualTo("order"));
            Assert.That(World.Get(chaos).Team, Is.EqualTo("chaos"));
            Assert.That(World.Get(guild).Team, Is.EqualTo("guild"));
            Assert.That(World.AreHostile(World.PlayerId, chaos), Is.True);
            Assert.That(World.AreHostile(chaos, World.PlayerId), Is.True);
            Assert.That(World.AreHostile(World.PlayerId, guild), Is.False, "allied");
            Assert.That(World.AreHostile(chaos, guild), Is.False, "no relationship is neutral");

            World.SetTeam(guild, "chaos");
            Assert.That(World.AreHostile(chaos, guild), Is.False, "same faction");
            Assert.That(World.AreHostile(World.PlayerId, guild), Is.True);
        }

        [Test]
        public void Validate_ReportsBrokenPvpSettings()
        {
            _settings.Pvp = new PvpSettings { Mode = PvpMode.TeamDeathmatch, ScoreToWin = 0, RespawnDelay = -1f };
            _settings.Pvp.Teams.Clear();
            _settings.Pvp.Teams.Add("Solo");

            var problems = _settings.Validate(null);

            Assert.That(problems, Has.Some.Contains("at least two teams"));
            Assert.That(problems, Has.Some.Contains("Score To Win"));
            Assert.That(problems, Has.Some.Contains("cannot be negative"));
            Assert.That(new PvpSettings().Validate(), Is.Empty);
        }
    }
}
