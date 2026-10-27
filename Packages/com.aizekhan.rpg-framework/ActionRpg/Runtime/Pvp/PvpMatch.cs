using System;
using System.Collections.Generic;
using RPGFramework.Events;
using RPGFramework.Systems;

namespace RPGFramework.ActionRpg
{
    public enum PvpPhase
    {
        /// <summary>
        /// Fewer than two sides have players.
        /// </summary>
        Waiting = 0,
        Playing = 1,

        /// <summary>
        /// Between Duel rounds.
        /// </summary>
        RoundOver = 2,
        Finished = 3
    }

    /// <summary>
    /// Runs a PvP match when <see cref="ActionRpgSettings.Pvp"/> is set (does nothing otherwise): puts every player
    /// on a side, starts once two sides have players, counts defeats, respawns, plays Duel rounds, ends the match
    /// and starts the next one. Everything it decides is announced on the bus (<see cref="PvpRoundStartedEvent"/>,
    /// <see cref="PvpScoreChangedEvent"/>, <see cref="PvpRoundEndedEvent"/>, <see cref="PvpMatchEndedEvent"/>).
    /// </summary>
    public sealed class PvpMatch : IUpdateSystem
    {
        private readonly ActorWorld _world;
        private readonly SubscriptionBag _subscriptions = new SubscriptionBag();
        private readonly Dictionary<string, int> _scores = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _roundWins = new Dictionary<string, int>();
        private readonly List<Respawn> _respawns = new List<Respawn>();
        private readonly List<string> _sides = new List<string>();
        private readonly PvpStatus _status = new PvpStatus();
        private float _timer;

        public PvpMatch(ActorWorld world, IEventBus bus)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            Bus = bus ?? throw new ArgumentNullException(nameof(bus));
            if (Settings != null)
                bus.Subscribe<PlayerDefeatedEvent>(OnPlayerDefeated).AddTo(_subscriptions);
            world.Match = this;
        }

        /// <summary>
        /// The match as the HUD shows it (the same instance, refreshed on every read).
        /// </summary>
        public PvpStatus Status
        {
            get
            {
                _status.CopyFrom(this);
                return _status;
            }
        }

        private IEventBus Bus { get; }

        /// <summary>
        /// The match settings; null when the game is not PvP.
        /// </summary>
        public PvpSettings Settings => _world.Settings.Pvp;

        public PvpPhase Phase { get; private set; }

        /// <summary>
        /// The current round (Duel), 1 while a TeamDeathmatch or FreeForAll match runs; 0 before the first.
        /// </summary>
        public int Round { get; private set; }

        /// <summary>
        /// Defeats each side scored this match.
        /// </summary>
        public IReadOnlyDictionary<string, int> Scores => _scores;

        /// <summary>
        /// Duel rounds each side won this match.
        /// </summary>
        public IReadOnlyDictionary<string, int> RoundWins => _roundWins;

        /// <summary>
        /// Sides that have players now.
        /// </summary>
        public IReadOnlyList<string> Sides => _sides;

        /// <summary>
        /// The winning side once <see cref="PvpPhase.Finished"/>; null on a draw.
        /// </summary>
        public string Winner { get; private set; }

        /// <summary>
        /// Seconds left of a time-limited match (0 when there is no limit or it is not running).
        /// </summary>
        public float TimeLeft => Phase == PvpPhase.Playing && Settings != null && Settings.TimeLimit > 0f
            && Settings.Mode != PvpMode.Duel
            ? Math.Max(0f, Settings.TimeLimit - _timer)
            : 0f;

        public int ScoreOf(string side) => side != null && _scores.TryGetValue(side, out int score) ? score : 0;

        public int RoundWinsOf(string side) => side != null && _roundWins.TryGetValue(side, out int wins) ? wins : 0;

        public void Initialize()
        {
        }

        public void Update(float deltaTime)
        {
            var settings = Settings;
            if (settings == null || !_world.IsStarted)
                return;

            AssignSides(settings);
            TickRespawns(deltaTime);

            switch (Phase)
            {
                case PvpPhase.Waiting:
                    if (_sides.Count >= 2)
                        StartMatch();
                    break;
                case PvpPhase.Playing:
                    if (_sides.Count < 2)
                    {
                        Finish(_sides.Count == 1 ? _sides[0] : null, forfeit: true);
                        break;
                    }
                    if (settings.Mode != PvpMode.Duel && settings.TimeLimit > 0f)
                    {
                        _timer += deltaTime;
                        if (_timer >= settings.TimeLimit)
                            Finish(Leader(), forfeit: false);
                    }
                    break;
                case PvpPhase.RoundOver:
                    if (_sides.Count < 2)
                    {
                        Finish(_sides.Count == 1 ? _sides[0] : null, forfeit: true);
                        break;
                    }
                    _timer += deltaTime;
                    if (_timer >= settings.RoundBreak)
                        StartRound();
                    break;
                case PvpPhase.Finished:
                    if (settings.RestartDelay <= 0f)
                        break;
                    _timer += deltaTime;
                    if (_timer >= settings.RestartDelay)
                        Restart();
                    break;
            }
        }

        /// <summary>
        /// Clears the score and waits for two sides to start a new match.
        /// </summary>
        public void Restart()
        {
            Phase = PvpPhase.Waiting;
            Round = 0;
            Winner = null;
            _timer = 0f;
            _scores.Clear();
            _roundWins.Clear();
            _respawns.Clear();
        }

        public void Dispose() => _subscriptions.Dispose();

        // Every player plays for a side: their own in FreeForAll, otherwise the team with fewest players
        private void AssignSides(PvpSettings settings)
        {
            _sides.Clear();
            foreach (int id in _world.Players)
            {
                var player = _world.Get(id);
                if (player == null)
                    continue;
                if (string.IsNullOrEmpty(player.Team))
                    _world.SetTeam(id, settings.Mode == PvpMode.FreeForAll ? PvpSettings.FreeForAllSide(player.Seat) : SmallestTeam(settings));
                if (!_sides.Contains(player.Team))
                    _sides.Add(player.Team);
            }
        }

        private string SmallestTeam(PvpSettings settings)
        {
            string smallest = null;
            int fewest = int.MaxValue;
            foreach (var team in settings.Teams)
            {
                if (string.IsNullOrWhiteSpace(team))
                    continue;
                int members = 0;
                foreach (int id in _world.Players)
                {
                    if (_world.Get(id)?.Team == team)
                        members++;
                }
                if (members < fewest)
                {
                    smallest = team;
                    fewest = members;
                }
            }
            return smallest;
        }

        private void StartMatch()
        {
            Restart();
            _world.Announce(Settings.Mode == PvpMode.Duel ? "Duel!" : "Fight!");
            StartRound();
        }

        private void StartRound()
        {
            Round++;
            Phase = PvpPhase.Playing;
            _timer = 0f;
            _respawns.Clear();
            foreach (var side in _sides)
            {
                if (!_scores.ContainsKey(side))
                    _scores[side] = 0;
            }
            StandEveryoneUp();
            if (Settings.Mode == PvpMode.Duel)
                _world.Announce($"Round {Round}");
            Bus.Publish(new PvpRoundStartedEvent(Round));
        }

        private void StandEveryoneUp()
        {
            foreach (int id in _world.Players)
            {
                var player = _world.Get(id);
                if (player == null)
                    continue;
                if (player.IsAlive())
                    player.RestoreFullHealth();
                else
                    _world.RevivePlayer(id);
            }
        }

        private void OnPlayerDefeated(PlayerDefeatedEvent defeat)
        {
            var settings = Settings;
            var victim = _world.Get(defeat.PlayerId);
            if (settings == null || victim == null)
                return;

            if (Phase != PvpPhase.Playing)
            {
                // Waiting or between matches: nobody stays down
                if (Phase != PvpPhase.RoundOver)
                    _respawns.Add(new Respawn(victim.EntityId, settings.RespawnDelay));
                return;
            }

            var killer = defeat.ByPlayer ? _world.Get(defeat.KillerId) : null;
            bool scores = killer != null && !string.IsNullOrEmpty(killer.Team) && killer.Team != victim.Team;
            if (scores)
            {
                int score = ScoreOf(killer.Team) + 1;
                _scores[killer.Team] = score;
                _world.Announce($"{killer.DisplayName} defeats {victim.DisplayName}");
                Bus.Publish(new PvpScoreChangedEvent(killer.Team, score));
            }

            if (settings.Mode == PvpMode.Duel)
            {
                EndRoundIfDecided();
                return;
            }

            if (scores && ScoreOf(killer.Team) >= settings.ScoreToWin)
                Finish(killer.Team, forfeit: false);
            else
                _respawns.Add(new Respawn(victim.EntityId, settings.RespawnDelay));
        }

        // A Duel round is decided when only one side still stands
        private void EndRoundIfDecided()
        {
            string standing = null;
            foreach (var side in _sides)
            {
                if (!HasLivingPlayer(side))
                    continue;
                if (standing != null)
                    return;
                standing = side;
            }

            int wins = 0;
            if (standing != null)
            {
                wins = RoundWinsOf(standing) + 1;
                _roundWins[standing] = wins;
                _world.Announce($"{standing} takes round {Round}");
            }
            Bus.Publish(new PvpRoundEndedEvent(Round, standing, wins));

            if (standing != null && wins >= Settings.RoundsToWin)
            {
                Finish(standing, forfeit: false);
                return;
            }
            Phase = PvpPhase.RoundOver;
            _timer = 0f;
        }

        private bool HasLivingPlayer(string side)
        {
            foreach (int id in _world.Players)
            {
                var player = _world.Get(id);
                if (player != null && player.Team == side && player.IsAlive())
                    return true;
            }
            return false;
        }

        // The side with the highest score; null when it is shared
        private string Leader()
        {
            string leader = null;
            int best = -1;
            bool tied = false;
            foreach (var side in _sides)
            {
                int score = ScoreOf(side);
                if (score > best)
                {
                    leader = side;
                    best = score;
                    tied = false;
                }
                else if (score == best)
                {
                    tied = true;
                }
            }
            return tied ? null : leader;
        }

        private void Finish(string winner, bool forfeit)
        {
            Phase = PvpPhase.Finished;
            Winner = winner;
            _timer = 0f;
            _respawns.Clear();
            // The match usually ends inside the last defeat: those down stand up on the next update
            foreach (int id in _world.Players)
            {
                var player = _world.Get(id);
                if (player == null)
                    continue;
                if (player.IsAlive())
                    player.RestoreFullHealth();
                else
                    _respawns.Add(new Respawn(id, 0f));
            }
            _world.Announce(winner == null ? "Draw" : $"{winner} wins the match");
            Bus.Publish(new PvpMatchEndedEvent(winner, forfeit));
        }

        private void TickRespawns(float deltaTime)
        {
            for (int i = _respawns.Count - 1; i >= 0; i--)
            {
                var respawn = _respawns[i];
                respawn.TimeLeft -= deltaTime;
                if (respawn.TimeLeft > 0f)
                {
                    _respawns[i] = respawn;
                    continue;
                }
                _respawns.RemoveAt(i);
                if (_world.RevivePlayer(respawn.PlayerId))
                    Bus.Publish(new PvpRespawnedEvent(respawn.PlayerId));
            }
        }

        private struct Respawn
        {
            public Respawn(int playerId, float timeLeft)
            {
                PlayerId = playerId;
                TimeLeft = timeLeft;
            }

            public int PlayerId;
            public float TimeLeft;
        }
    }
}
