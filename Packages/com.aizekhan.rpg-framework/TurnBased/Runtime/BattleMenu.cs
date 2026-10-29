using System;
using System.Collections.Generic;
using System.Linq;
using RPGFramework.Items;

namespace RPGFramework.TurnBased
{
    /// <summary>
    /// One choice of the battle menu for the combatant whose turn it is: what it says, the action, and either the
    /// targets it can be aimed at, nothing to aim (all enemies, self, defend) or why it cannot be taken now.
    /// </summary>
    public sealed class BattleCommand
    {
        internal BattleCommand(string label, BattleAction action, IReadOnlyList<Combatant> targets, bool needsTarget, string refusal)
        {
            Label = label ?? "";
            Action = action;
            Targets = targets ?? Array.Empty<Combatant>();
            NeedsTarget = needsTarget;
            Refusal = refusal;
        }

        public string Label { get; }

        /// <summary>
        /// The action without a target; <see cref="With"/> aims it.
        /// </summary>
        public BattleAction Action { get; }

        public BattleActionKind Kind => Action.Kind;

        /// <summary>
        /// Who it can be aimed at now (when <see cref="NeedsTarget"/>).
        /// </summary>
        public IReadOnlyList<Combatant> Targets { get; }

        public bool NeedsTarget { get; }

        /// <summary>
        /// Why it cannot be taken now ("Not enough mana."); null when it can.
        /// </summary>
        public string Refusal { get; }

        public bool IsAvailable => Refusal == null;

        /// <summary>
        /// The action aimed at a target (the target is ignored by actions that need none).
        /// </summary>
        public BattleAction With(Combatant target) => With(target?.Id ?? -1);

        public BattleAction With(int targetId)
        {
            if (!NeedsTarget)
                return Action;
            switch (Action.Kind)
            {
                case BattleActionKind.Attack:
                    return BattleAction.Attack(targetId);
                case BattleActionKind.Ability:
                    return BattleAction.Ability(Action.AbilityId, targetId);
                case BattleActionKind.Item:
                    return BattleAction.Item(Action.ItemId, targetId);
                default:
                    return Action;
            }
        }

        public override string ToString() => IsAvailable ? Label : Label + " (" + Refusal + ")";
    }

    /// <summary>
    /// The battle menu for any screen: Attack, each ability the current combatant knows, Defend, each item the party
    /// can use in battle, Flee; each with its targets or the reason it is closed, as the battle's own rules
    /// (<see cref="Battle.WhyNot"/>) say. Empty when the battle is over or it is not a party member's turn.
    /// </summary>
    public static class BattleMenu
    {
        public static IReadOnlyList<BattleCommand> For(Battle battle, IEnumerable<IItem> inventory = null)
        {
            var commands = new List<BattleCommand>();
            if (battle == null || battle.IsOver || battle.Current == null || !battle.Current.IsParty)
                return commands;
            var actor = battle.Current;

            commands.Add(Make(battle, "Attack", BattleAction.Attack(-1)));
            foreach (var abilityId in actor.AbilityIds)
            {
                string name = battle.AbilityOf(abilityId)?.Name ?? abilityId;
                int wait = battle.CooldownLeft(actor.Id, abilityId);
                commands.Add(Make(battle, wait > 0 ? name + " (" + wait + ")" : name, BattleAction.Ability(abilityId, -1)));
            }
            commands.Add(Make(battle, "Defend", BattleAction.Defend()));

            if (inventory != null)
            {
                foreach (var group in inventory.Where(i => i is IConsumable c && c.UsableInCombat).GroupBy(i => i.ItemId))
                {
                    var item = group.First();
                    int count = group.Count();
                    commands.Add(Make(battle, count > 1 ? item.Name + " x" + count : item.Name, BattleAction.Item(item.ItemId, -1)));
                }
            }

            if (battle.Encounter == null || battle.Encounter.CanFlee)
                commands.Add(Make(battle, "Flee", BattleAction.Flee()));
            return commands;
        }

        private static BattleCommand Make(Battle battle, string label, BattleAction action)
        {
            string asIs = battle.WhyNot(action);
            if (asIs == null)
                return new BattleCommand(label, action, null, needsTarget: false, refusal: null);

            var targets = new List<Combatant>();
            string firstRefusal = null;
            foreach (var combatant in battle.Combatants)
            {
                string why = battle.WhyNot(Aim(action, combatant.Id));
                if (why == null)
                    targets.Add(combatant);
                else if (firstRefusal == null && (combatant.IsAlive || action.Kind == BattleActionKind.Item))
                    firstRefusal = why;
            }
            return targets.Count > 0
                ? new BattleCommand(label, action, targets, needsTarget: true, refusal: null)
                : new BattleCommand(label, action, null, needsTarget: false, refusal: firstRefusal ?? asIs);
        }

        private static BattleAction Aim(BattleAction action, int targetId)
        {
            switch (action.Kind)
            {
                case BattleActionKind.Attack:
                    return BattleAction.Attack(targetId);
                case BattleActionKind.Ability:
                    return BattleAction.Ability(action.AbilityId, targetId);
                case BattleActionKind.Item:
                    return BattleAction.Item(action.ItemId, targetId);
                default:
                    return action;
            }
        }
    }
}
