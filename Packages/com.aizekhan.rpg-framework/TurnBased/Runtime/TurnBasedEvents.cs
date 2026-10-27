using System.Collections.Generic;
using RPGFramework.Events;

namespace RPGFramework.TurnBased
{
    // Status effects applied, ticking and expiring are announced by the StatusEffects module (EffectAppliedEvent,
    // EffectTickEvent, EffectExpiredEvent); experience and raw level-ups by Progression. These are the rest.

    public enum BattleOutcome
    {
        /// <summary>
        /// Still being fought.
        /// </summary>
        None,
        Victory,
        Defeat,
        Fled
    }

    /// <summary>
    /// A fight began: who stands on each side, in slot order.
    /// </summary>
    public readonly struct BattleStartedEvent : IEvent
    {
        public BattleStartedEvent(string encounterId, int encounterIndex, IReadOnlyList<int> partyIds, IReadOnlyList<int> enemyIds)
        {
            EncounterId = encounterId;
            EncounterIndex = encounterIndex;
            PartyIds = partyIds;
            EnemyIds = enemyIds;
        }

        public string EncounterId { get; }

        /// <summary>
        /// Place of the encounter in the run (0 = the first fight).
        /// </summary>
        public int EncounterIndex { get; }

        public IReadOnlyList<int> PartyIds { get; }
        public IReadOnlyList<int> EnemyIds { get; }
    }

    /// <summary>
    /// A new round: everyone standing acts once, fastest first.
    /// </summary>
    public readonly struct RoundStartedEvent : IEvent
    {
        public RoundStartedEvent(int round, IReadOnlyList<int> turnOrder)
        {
            Round = round;
            TurnOrder = turnOrder;
        }

        public int Round { get; }
        public IReadOnlyList<int> TurnOrder { get; }
    }

    /// <summary>
    /// A combatant's turn began (its status effects tick right after this).
    /// </summary>
    public readonly struct TurnStartedEvent : IEvent
    {
        public TurnStartedEvent(int round, int combatantId, bool isParty)
        {
            Round = round;
            CombatantId = combatantId;
            IsParty = isParty;
        }

        public int Round { get; }
        public int CombatantId { get; }
        public bool IsParty { get; }
    }

    /// <summary>
    /// A combatant lost its turn (stunned).
    /// </summary>
    public readonly struct TurnSkippedEvent : IEvent
    {
        public TurnSkippedEvent(int round, int combatantId, string reason)
        {
            Round = round;
            CombatantId = combatantId;
            Reason = reason;
        }

        public int Round { get; }
        public int CombatantId { get; }
        public string Reason { get; }
    }

    /// <summary>
    /// A combatant acted: what it did and what happened to each target (damage, healing, misses, who went down).
    /// Published before the status effects of the action are put on (their EffectAppliedEvents follow).
    /// </summary>
    public readonly struct ActionPerformedEvent : IEvent
    {
        public ActionPerformedEvent(int round, int actorId, BattleActionKind kind, string abilityId, string itemId,
            IReadOnlyList<TargetOutcome> targets, bool escaped)
        {
            Round = round;
            ActorId = actorId;
            Kind = kind;
            AbilityId = abilityId;
            ItemId = itemId;
            Targets = targets;
            Escaped = escaped;
        }

        public int Round { get; }
        public int ActorId { get; }
        public BattleActionKind Kind { get; }
        public string AbilityId { get; }
        public string ItemId { get; }
        public IReadOnlyList<TargetOutcome> Targets { get; }

        /// <summary>
        /// A flee attempt worked.
        /// </summary>
        public bool Escaped { get; }
    }

    /// <summary>
    /// A combatant went down and left the turn order.
    /// </summary>
    public readonly struct CombatantDefeatedEvent : IEvent
    {
        public CombatantDefeatedEvent(int combatantId, string characterId, bool isParty, int defeatedById)
        {
            CombatantId = combatantId;
            CharacterId = characterId;
            IsParty = isParty;
            DefeatedById = defeatedById;
        }

        public int CombatantId { get; }
        public string CharacterId { get; }
        public bool IsParty { get; }

        /// <summary>
        /// Who brought it down; -1 for a status effect (poison).
        /// </summary>
        public int DefeatedById { get; }
    }

    /// <summary>
    /// A fight ended. Published after the rewards of a victory were handed out (gold, items, experience and the
    /// level-ups it caused), so the world already shows them.
    /// </summary>
    public readonly struct BattleEndedEvent : IEvent
    {
        public BattleEndedEvent(string encounterId, BattleOutcome outcome, BattleRewards rewards)
        {
            EncounterId = encounterId;
            Outcome = outcome;
            Rewards = rewards;
        }

        public string EncounterId { get; }
        public BattleOutcome Outcome { get; }

        /// <summary>
        /// What the party got (empty unless it won).
        /// </summary>
        public BattleRewards Rewards { get; }
    }

    /// <summary>
    /// A party member reached a new level; its stats already grew.
    /// </summary>
    public readonly struct MemberLevelUpEvent : IEvent
    {
        public MemberLevelUpEvent(int memberId, string characterId, int oldLevel, int newLevel)
        {
            MemberId = memberId;
            CharacterId = characterId;
            OldLevel = oldLevel;
            NewLevel = newLevel;
        }

        public int MemberId { get; }
        public string CharacterId { get; }
        public int OldLevel { get; }
        public int NewLevel { get; }
    }

    /// <summary>
    /// The last encounter of the run was won.
    /// </summary>
    public readonly struct RunCompletedEvent : IEvent
    {
        public RunCompletedEvent(int encountersWon, int gold)
        {
            EncountersWon = encountersWon;
            Gold = gold;
        }

        public int EncountersWon { get; }
        public int Gold { get; }
    }

    /// <summary>
    /// What a won fight gave the party.
    /// </summary>
    public sealed class BattleRewards
    {
        public static readonly BattleRewards None = new BattleRewards();

        /// <summary>
        /// Experience each living member got.
        /// </summary>
        public long Experience { get; internal set; }

        public int Gold { get; internal set; }

        /// <summary>
        /// Item ids put into the inventory, one entry per item.
        /// </summary>
        public IReadOnlyList<string> Items => _items;

        /// <summary>
        /// Members who reached a new level from this fight.
        /// </summary>
        public IReadOnlyList<int> LeveledUp => _leveledUp;

        private readonly List<string> _items = new List<string>();
        private readonly List<int> _leveledUp = new List<int>();

        internal void AddItem(string itemId) => _items.Add(itemId);

        internal void AddLevelUp(int memberId)
        {
            if (!_leveledUp.Contains(memberId))
                _leveledUp.Add(memberId);
        }
    }
}
