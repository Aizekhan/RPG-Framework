using RPGFramework.Events;

namespace RPGFramework.ActionRpg
{
    /// <summary>
    /// A player fell. <see cref="ByPlayer"/> when another player struck the blow (a PvP defeat).
    /// </summary>
    public readonly struct PlayerDefeatedEvent : IEvent
    {
        public PlayerDefeatedEvent(int playerId, int killerId, bool byPlayer)
        {
            PlayerId = playerId;
            KillerId = killerId;
            ByPlayer = byPlayer;
        }

        public int PlayerId { get; }
        public int KillerId { get; }
        public bool ByPlayer { get; }
    }

    /// <summary>
    /// An actor joined a team (null = none); views recolour it.
    /// </summary>
    public readonly struct ActorTeamChangedEvent : IEvent
    {
        public ActorTeamChangedEvent(int entityId, string team)
        {
            EntityId = entityId;
            Team = team;
        }

        public int EntityId { get; }
        public string Team { get; }
    }

    /// <summary>
    /// A PvP round began: everyone stands at full health (move them to their spawn points).
    /// TeamDeathmatch and FreeForAll have a single round.
    /// </summary>
    public readonly struct PvpRoundStartedEvent : IEvent
    {
        public PvpRoundStartedEvent(int round)
        {
            Round = round;
        }

        public int Round { get; }
    }

    /// <summary>
    /// A defeated player came back (move them to their side's spawn point).
    /// </summary>
    public readonly struct PvpRespawnedEvent : IEvent
    {
        public PvpRespawnedEvent(int playerId)
        {
            PlayerId = playerId;
        }

        public int PlayerId { get; }
    }

    /// <summary>
    /// A side scored a defeat.
    /// </summary>
    public readonly struct PvpScoreChangedEvent : IEvent
    {
        public PvpScoreChangedEvent(string side, int score)
        {
            Side = side;
            Score = score;
        }

        public string Side { get; }
        public int Score { get; }
    }

    /// <summary>
    /// A Duel round ended; <see cref="Winner"/> took it.
    /// </summary>
    public readonly struct PvpRoundEndedEvent : IEvent
    {
        public PvpRoundEndedEvent(int round, string winner, int roundsWon)
        {
            Round = round;
            Winner = winner;
            RoundsWon = roundsWon;
        }

        public int Round { get; }
        public string Winner { get; }
        public int RoundsWon { get; }
    }

    /// <summary>
    /// The match is over. <see cref="Winner"/> is null on a draw; <see cref="Forfeit"/> when the other sides left.
    /// </summary>
    public readonly struct PvpMatchEndedEvent : IEvent
    {
        public PvpMatchEndedEvent(string winner, bool forfeit)
        {
            Winner = winner;
            Forfeit = forfeit;
        }

        public string Winner { get; }
        public bool Forfeit { get; }
    }
}
