using System;
using System.Collections.Generic;
using RPGFramework.Localization;

namespace RPGFramework.TurnBased
{
    public enum BattleActionKind
    {
        /// <summary>
        /// A plain weapon hit on one enemy.
        /// </summary>
        Attack,

        /// <summary>
        /// An ability, through the Abilities module (costs, cooldown, target).
        /// </summary>
        Ability,

        /// <summary>
        /// Takes less damage until its next turn.
        /// </summary>
        Defend,

        /// <summary>
        /// A consumable from the party inventory.
        /// </summary>
        Item,

        /// <summary>
        /// The party tries to run (chance; refused when the encounter forbids it).
        /// </summary>
        Flee
    }

    /// <summary>
    /// What the combatant whose turn it is does. Build one with the factory methods.
    /// </summary>
    public readonly struct BattleAction
    {
        private BattleAction(BattleActionKind kind, int targetId, string abilityId, string itemId, int actorId)
        {
            Kind = kind;
            TargetId = targetId;
            AbilityId = abilityId;
            ItemId = itemId;
            ActorId = actorId;
        }

        public BattleActionKind Kind { get; }

        /// <summary>
        /// Chosen target for single-target actions; -1 for none.
        /// </summary>
        public int TargetId { get; }

        public string AbilityId { get; }
        public string ItemId { get; }

        /// <summary>
        /// Who means to act; -1 = whoever's turn it is. Set it (with <see cref="By"/>) so an action sent for the
        /// wrong combatant is refused instead of being done by the current one.
        /// </summary>
        public int ActorId { get; }

        public static BattleAction Attack(int targetId) => new BattleAction(BattleActionKind.Attack, targetId, null, null, -1);

        /// <param name="targetId">The target for a single-target ability; ignored by abilities that hit a whole side or the caster.</param>
        public static BattleAction Ability(string abilityId, int targetId = -1) =>
            new BattleAction(BattleActionKind.Ability, targetId, abilityId, null, -1);

        public static BattleAction Defend() => new BattleAction(BattleActionKind.Defend, -1, null, null, -1);

        public static BattleAction Item(string itemId, int targetId) => new BattleAction(BattleActionKind.Item, targetId, null, itemId, -1);

        public static BattleAction Flee() => new BattleAction(BattleActionKind.Flee, -1, null, null, -1);

        /// <summary>
        /// The same action, meant for this combatant.
        /// </summary>
        public BattleAction By(int actorId) => new BattleAction(Kind, TargetId, AbilityId, ItemId, actorId);

        public override string ToString()
        {
            switch (Kind)
            {
                case BattleActionKind.Ability: return $"Ability {AbilityId} → {TargetId}";
                case BattleActionKind.Item: return $"Item {ItemId} → {TargetId}";
                case BattleActionKind.Attack: return $"Attack → {TargetId}";
                default: return Kind.ToString();
            }
        }
    }

    /// <summary>
    /// What happened to one target of an action.
    /// </summary>
    public readonly struct TargetOutcome
    {
        public TargetOutcome(int targetId, int damage = 0, int healing = 0, int manaRestored = 0, bool missed = false,
            bool defeated = false, bool revived = false, bool critical = false)
        {
            Critical = critical;
            TargetId = targetId;
            Damage = damage;
            Healing = healing;
            ManaRestored = manaRestored;
            Missed = missed;
            Defeated = defeated;
            Revived = revived;
        }

        public int TargetId { get; }
        public int Damage { get; }
        public int Healing { get; }
        public int ManaRestored { get; }
        public bool Missed { get; }

        /// <summary>
        /// The action brought it down.
        /// </summary>
        public bool Defeated { get; }

        public bool Revived { get; }

        /// <summary>
        /// The hit was critical (<see cref="HitResult.Critical"/>).
        /// </summary>
        public bool Critical { get; }

        internal TargetOutcome WithDefeated() => new TargetOutcome(TargetId, Damage, Healing, ManaRestored, Missed, true, Revived, Critical);
    }

    /// <summary>
    /// The answer to <see cref="Battle.Perform"/>: done (with what it did), or refused with the reason (a code, a
    /// text to translate and plain English words), in which case nothing changed.
    /// </summary>
    public sealed class ActionResult
    {
        private static readonly IReadOnlyList<TargetOutcome> NoTargets = Array.Empty<TargetOutcome>();

        private ActionResult(bool done, BattleRefusal refusal, int actorId, BattleAction action, IReadOnlyList<TargetOutcome> targets, bool escaped)
        {
            Done = done;
            Why = refusal;
            ActorId = actorId;
            Action = action;
            Targets = targets ?? NoTargets;
            Escaped = escaped;
        }

        /// <summary>
        /// The action was taken (the turn is over).
        /// </summary>
        public bool Done { get; }

        /// <summary>
        /// Why the action was refused, in English; null when it was done.
        /// </summary>
        public string Refusal => Why.Message;

        /// <summary>
        /// Why the action was refused, as a code (<see cref="BattleRefusalReason.None"/> when it was done).
        /// </summary>
        public BattleRefusalReason Reason => Why.Reason;

        /// <summary>
        /// Why the action was refused, as a text to translate (<see cref="BattleTexts"/> keys); empty when it was done.
        /// </summary>
        public LocalizedText RefusalText => Why.Text;

        /// <summary>
        /// The refusal whole: code, text and English.
        /// </summary>
        public BattleRefusal Why { get; }

        public int ActorId { get; }
        public BattleAction Action { get; }
        public IReadOnlyList<TargetOutcome> Targets { get; }

        /// <summary>
        /// A flee attempt worked.
        /// </summary>
        public bool Escaped { get; }

        internal static ActionResult Refused(BattleRefusal reason, BattleAction action) =>
            new ActionResult(false, reason, -1, action, null, false);

        internal static ActionResult Performed(int actorId, BattleAction action, IReadOnlyList<TargetOutcome> targets, bool escaped = false) =>
            new ActionResult(true, BattleRefusal.None, actorId, action, targets, escaped);

        public override string ToString() => Done ? $"{Action} done" : $"Refused: {Refusal}";
    }
}
