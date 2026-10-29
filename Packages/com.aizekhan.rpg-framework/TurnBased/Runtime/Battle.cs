using System;
using System.Collections.Generic;
using System.Linq;
using RPGFramework.Abilities;
using RPGFramework.Items;
using RPGFramework.Logging;
using RPGFramework.StatusEffects;

namespace RPGFramework.TurnBased
{
    /// <summary>
    /// One fight between the party and an encounter's enemies.
    /// <para>Rounds: at the start of each round everyone standing is put in turn order by Speed, fastest first; ties
    /// go to the party, then to the earlier slot. Speed changes count from the next round. A combatant who goes down
    /// leaves the order; one brought back joins from the next round.</para>
    /// <para>Turns: the current combatant's turn starts (its Defend ends), its status effects tick (poison, regen),
    /// then a stunned combatant loses the turn. Otherwise it takes one action through <see cref="Perform"/>. At the
    /// end of its turn every status effect on it loses one turn and those at 0 expire. So effect durations count the
    /// affected combatant's own turns: poison for 3 ticks on its next 3 turns, a 1-turn stun skips one turn. An effect
    /// a combatant puts on itself counts the turn it was cast in.</para>
    /// <para>Abilities go through the Abilities module with the round number as its clock: costs are checked and
    /// paid, and a cooldown of 2 used in round 1 allows the ability again from round 3. Cooldowns end with the battle.
    /// </para>
    /// <para>An action that is not allowed is refused with the reason in plain words and changes nothing.</para>
    /// <para>Enemies act through the <see cref="IBattleAi"/>, on their own unless
    /// <see cref="TurnBasedSettings.EnemiesActAutomatically"/> is off.</para>
    /// </summary>
    public sealed class Battle
    {
        private const string Category = "TurnBased";
        private const int MaxLogLines = 100;
        private const int MaxSkippedTurns = 10000;

        private readonly TurnBasedWorld _world;
        private readonly List<Combatant> _party;
        private readonly List<Combatant> _enemies;
        private readonly List<Combatant> _all;
        private readonly List<Combatant> _order = new List<Combatant>();
        private readonly HashSet<int> _defeated = new HashSet<int>();
        private readonly List<string> _log = new List<string>();
        private int _turnIndex = -1;

        internal Battle(TurnBasedWorld world, EncounterData encounter, int encounterIndex, List<Combatant> party, List<Combatant> enemies)
        {
            _world = world;
            Encounter = encounter;
            EncounterIndex = encounterIndex;
            _party = party;
            _enemies = enemies;
            _all = party.Concat(enemies).ToList();
            foreach (var fallen in _all.Where(c => !c.IsAlive))
                _defeated.Add(fallen.Id);
        }

        public EncounterData Encounter { get; }

        /// <summary>
        /// Place of the encounter in the run (0 = the first fight).
        /// </summary>
        public int EncounterIndex { get; }

        /// <summary>
        /// Current round, from 1.
        /// </summary>
        public int Round { get; private set; }

        /// <summary>
        /// Whose turn it is; null once the battle is over.
        /// </summary>
        public Combatant Current { get; private set; }

        /// <summary>
        /// Everyone in the fight, party first, each side in slot order (the fallen included).
        /// </summary>
        public IReadOnlyList<Combatant> Combatants => _all;

        public IReadOnlyList<Combatant> Party => _party;
        public IReadOnlyList<Combatant> Enemies => _enemies;

        public BattleOutcome Outcome { get; private set; }
        public bool IsOver => Outcome != BattleOutcome.None;

        /// <summary>
        /// Who still acts this round, the current combatant first.
        /// </summary>
        public IReadOnlyList<Combatant> TurnOrder =>
            IsOver ? (IReadOnlyList<Combatant>)Array.Empty<Combatant>()
                : _order.Skip(Math.Max(0, _turnIndex)).Where(c => c.IsAlive).ToList();

        /// <summary>
        /// What happened, in words, oldest first.
        /// </summary>
        public IReadOnlyList<string> Log => _log;

        public Combatant Get(int combatantId) => _all.FirstOrDefault(c => c.Id == combatantId);

        public bool AreEnemies(int firstId, int secondId)
        {
            var first = Get(firstId);
            var second = Get(secondId);
            return first != null && second != null && first.Side != second.Side;
        }

        /// <summary>
        /// Living combatants on the same side (itself included).
        /// </summary>
        public IReadOnlyList<Combatant> LivingAllies(Combatant combatant) =>
            _all.Where(c => c.Side == combatant.Side && c.IsAlive).ToList();

        public IReadOnlyList<Combatant> LivingOpponents(Combatant combatant) =>
            _all.Where(c => c.Side != combatant.Side && c.IsAlive).ToList();

        /// <summary>
        /// What an ability does in battle; null when it only pays its costs.
        /// </summary>
        public AbilityEffectData EffectOf(string abilityId) => _world.EffectOf(abilityId);

        public IAbility AbilityOf(string abilityId) => _world.Abilities.GetAbility(abilityId);

        /// <summary>
        /// Rounds before the combatant may use the ability again; 0 when it may now.
        /// </summary>
        public int CooldownLeft(int combatantId, string abilityId) =>
            (int)Math.Ceiling(_world.Cooldowns.GetRemainingCooldown(combatantId, abilityId, Round));

        /// <summary>
        /// Why the action cannot be taken now, in plain words; null when it can.
        /// </summary>
        public string WhyNot(BattleAction action) => Prepare(action, out _);

        public bool CanPerform(BattleAction action) => Prepare(action, out _) == null;

        /// <summary>
        /// The current combatant takes the action: its turn ends and the battle moves on (enemies acting on their own
        /// take their turns before this returns). Refused, with the reason, when not allowed; then nothing changes.
        /// </summary>
        public ActionResult Perform(BattleAction action)
        {
            string refusal = Prepare(action, out var plan);
            if (refusal != null)
                return ActionResult.Refused(refusal, action);
            var result = Act(plan);
            RunAutomaticTurns();
            return result;
        }

        /// <summary>
        /// The current combatant acts as the AI chooses (an enemy's turn when enemies do not act on their own, or a
        /// party member on auto-battle).
        /// </summary>
        public ActionResult PlayAiTurn()
        {
            if (IsOver)
                return ActionResult.Refused("The battle is over.", default);
            var result = Act(ChooseFor(Current));
            RunAutomaticTurns();
            return result;
        }

        /// <summary>
        /// What the AI would do for the current combatant (always an allowed action).
        /// </summary>
        public BattleAction SuggestAction() => IsOver ? BattleAction.Defend() : ChooseFor(Current).Action;

        // ---- Flow ----

        internal void Start()
        {
            Write($"{Encounter.DisplayName}: {string.Join(", ", _enemies.Select(e => e.Name))}");
            _world.Bus.Publish(new BattleStartedEvent(Encounter.Id, EncounterIndex,
                _party.Select(c => c.Id).ToList(), _enemies.Select(c => c.Id).ToList()));
            if (!CheckOutcome())
                Advance();
            RunAutomaticTurns();
        }

        private void RunAutomaticTurns()
        {
            while (!IsOver && Current != null && Current.Side == BattleSide.Enemies && _world.Settings.EnemiesActAutomatically)
                Act(ChooseFor(Current));
        }

        private void StartRound()
        {
            Round++;
            _order.Clear();
            _order.AddRange(_all.Where(c => c.IsAlive)
                .OrderByDescending(c => c.Speed)
                .ThenBy(c => c.Side == BattleSide.Party ? 0 : 1)
                .ThenBy(c => c.Slot));
            _turnIndex = 0;
            _world.Bus.Publish(new RoundStartedEvent(Round, _order.Select(c => c.Id).ToList()));
        }

        // Moves to the next combatant who can act, running the turns of those who cannot (stunned, killed by poison)
        private void Advance()
        {
            for (int skipped = 0; skipped < MaxSkippedTurns; skipped++)
            {
                _turnIndex++;
                if (Round == 0 || _turnIndex >= _order.Count)
                    StartRound();
                if (_order.Count == 0)
                    return;

                var next = _order[_turnIndex];
                if (!next.IsAlive)
                    continue;

                Current = next;
                if (BeginTurn(next))
                    return;
                EndTurn(next);
                if (CheckOutcome())
                    return;
            }
            throw new InvalidOperationException("Nobody in the battle can act (is everyone stunned for good?)");
        }

        private bool BeginTurn(Combatant combatant)
        {
            combatant.TurnsTaken++;
            combatant.IsDefending = false;
            _world.Bus.Publish(new TurnStartedEvent(Round, combatant.Id, combatant.IsParty));

            _world.StatusEffects.ProcessTicks(combatant.Id, combatant.TurnsTaken, OnTick);
            if (!combatant.IsAlive)
            {
                Defeated(combatant, -1);
                return false;
            }
            if (combatant.IsStunned)
            {
                Write($"{combatant.Name} is stunned and loses the turn");
                _world.Bus.Publish(new TurnSkippedEvent(Round, combatant.Id, "stunned"));
                return false;
            }
            return true;
        }

        private void OnTick(int entityId, string effectId, float damage, float healing)
        {
            var combatant = Get(entityId);
            if (combatant == null || !combatant.IsAlive)
                return;
            int taken = combatant.TakeDamage(Combatant.Round(damage));
            int healed = combatant.Heal(Combatant.Round(healing));
            string name = _world.StatusEffects.GetEffect(effectId)?.Name ?? effectId;
            if (taken > 0)
                Write($"{combatant.Name} takes {taken} from {name}");
            if (healed > 0)
                Write($"{combatant.Name} recovers {healed} from {name}");
        }

        private void EndTurn(Combatant combatant)
        {
            if (combatant.IsAlive)
                _world.StatusEffects.UpdateEffects(combatant.Id, 1f);
        }

        private bool CheckOutcome()
        {
            if (IsOver)
                return true;
            if (_enemies.All(e => !e.IsAlive))
                Finish(BattleOutcome.Victory);
            else if (_party.All(p => !p.IsAlive))
                Finish(BattleOutcome.Defeat);
            return IsOver;
        }

        private void Finish(BattleOutcome outcome)
        {
            Outcome = outcome;
            Current = null;
            foreach (var combatant in _all)
            {
                _world.StatusEffects.RemoveAllEffects(combatant.Id);
                combatant.ClearEffects();
                combatant.IsDefending = false;
                combatant.TurnsTaken = 0;
                _world.Cooldowns.ClearAllCooldowns(combatant.Id);
            }
            switch (outcome)
            {
                case BattleOutcome.Victory: Write("Victory!"); break;
                case BattleOutcome.Defeat: Write("The party has fallen..."); break;
                case BattleOutcome.Fled: Write("The party got away"); break;
            }
            _world.OnBattleEnded(this);
        }

        /// <summary>
        /// A combatant reached 0 health (from a hit, poison or a resource drain): it leaves the turn order and its
        /// effects come off. Safe to call more than once.
        /// </summary>
        internal void Defeated(Combatant combatant, int byId)
        {
            if (combatant.IsAlive || !_defeated.Add(combatant.Id))
                return;
            combatant.IsDefending = false;
            Write($"{combatant.Name} is defeated");
            _world.Bus.Publish(new CombatantDefeatedEvent(combatant.Id, combatant.CharacterId, combatant.IsParty, byId));
            _world.StatusEffects.RemoveAllEffects(combatant.Id);
            combatant.ClearEffects();
        }

        // ---- Choosing ----

        private Plan ChooseFor(Combatant actor)
        {
            var ai = _world.Settings.EnemyAi ?? _world.DefaultAi;
            BattleAction choice;
            try
            {
                choice = ai.Choose(this, actor, _world.Random);
            }
            catch (Exception ex)
            {
                _world.Logger?.LogError($"The battle AI failed for {actor.Name}", Category, ex);
                choice = BattleAction.Defend();
            }

            string refusal = Prepare(choice, out var plan);
            if (refusal == null)
                return plan;

            _world.Logger?.LogWarning($"The battle AI chose something {actor.Name} cannot do ({refusal}); it attacks instead", Category);
            var foes = LivingOpponents(actor);
            if (foes.Count > 0 && Prepare(BattleAction.Attack(foes[0].Id), out plan) == null)
                return plan;
            Prepare(BattleAction.Defend(), out plan);
            return plan;
        }

        // ---- Checking an action ----

        private sealed class Plan
        {
            public BattleAction Action;
            public Combatant Actor;
            public List<Combatant> Targets = new List<Combatant>();
            public IAbility Ability;
            public AbilityEffectData Effect;
            public IItem Item;
            public string ItemEffect;
            public float Magnitude;
        }

        private string Prepare(BattleAction action, out Plan plan)
        {
            plan = null;
            if (IsOver)
                return "The battle is over.";
            var actor = Current;
            if (actor == null)
                return "The battle has not started.";
            if (action.ActorId >= 0 && action.ActorId != actor.Id)
                return $"It is not {Get(action.ActorId)?.Name ?? "that combatant"}'s turn; it is {actor.Name}'s.";

            var candidate = new Plan { Action = action, Actor = actor };
            string refusal;
            switch (action.Kind)
            {
                case BattleActionKind.Attack:
                    refusal = SingleTarget(candidate, action.TargetId, wantOpponent: true, wantFallen: false, "attack");
                    break;
                case BattleActionKind.Defend:
                    candidate.Targets.Add(actor);
                    refusal = null;
                    break;
                case BattleActionKind.Ability:
                    refusal = PrepareAbility(candidate);
                    break;
                case BattleActionKind.Item:
                    refusal = PrepareItem(candidate);
                    break;
                case BattleActionKind.Flee:
                    refusal = !actor.IsParty ? "Only the party can flee."
                        : !Encounter.CanFlee ? "There is no running from this fight." : null;
                    break;
                default:
                    refusal = $"Unknown action {action.Kind}.";
                    break;
            }

            if (refusal == null)
                plan = candidate;
            return refusal;
        }

        private string SingleTarget(Plan plan, int targetId, bool wantOpponent, bool wantFallen, string verb)
        {
            if (targetId < 0)
                return "Choose a target.";
            var target = Get(targetId);
            if (target == null)
                return "That target is not in this battle.";
            bool opponent = target.Side != plan.Actor.Side;
            if (wantOpponent && !opponent)
                return $"{plan.Actor.Name} cannot {verb} an ally.";
            if (!wantOpponent && opponent)
                return $"{plan.Actor.Name} cannot {verb} an enemy.";
            if (wantFallen && target.IsAlive)
                return $"{target.Name} is not down.";
            if (!wantFallen && !target.IsAlive)
                return $"{target.Name} is already down.";
            plan.Targets.Add(target);
            return null;
        }

        private string PrepareAbility(Plan plan)
        {
            var actor = plan.Actor;
            string id = plan.Action.AbilityId;
            if (string.IsNullOrEmpty(id))
                return "Choose an ability.";
            var ability = _world.Abilities.GetAbility(id);
            if (ability == null)
                return $"There is no ability '{id}'.";
            if (!actor.Knows(id))
                return $"{actor.Name} does not know {ability.Name}.";
            if (actor.IsSilenced)
                return $"{actor.Name} is silenced and cannot use abilities.";
            if (ability.Type != AbilityType.Active && ability.Type != AbilityType.Triggered)
                return $"{ability.Name} cannot be used in battle ({ability.Type}).";
            if (ability.CastInfo.CastTime > 0f || ability.CastInfo.ChannelDuration > 0f)
                return $"{ability.Name} takes time to cast, which turn-based battles do not support.";
            int cooldown = CooldownLeft(actor.Id, id);
            if (cooldown > 0)
                return $"{ability.Name} is not ready: {cooldown} more round{(cooldown == 1 ? "" : "s")}.";
            if (!_world.Executor.CanExecuteAbility(id, actor.Id, Round))
                return $"{ability.Name} has no uses left.";

            foreach (var cost in ability.ResourceCosts)
            {
                var resource = Resources.Parse(cost.ResourceType);
                if (resource == Resource.None)
                    return $"{ability.Name} costs {cost.ResourceType}, which combatants do not have.";
                int price = Price(actor, cost);
                if (resource == Resource.Mana && actor.Mana < price)
                    return $"Not enough mana: {ability.Name} costs {price} MP and {actor.Name} has {actor.Mana}.";
                if (resource == Resource.Health && actor.Health <= price)
                    return $"{ability.Name} costs {price} HP, more than {actor.Name} can give.";
            }

            plan.Ability = ability;
            plan.Effect = _world.EffectOf(id);
            var scope = plan.Effect?.Scope ?? TargetScopes.FromTargetType(ability.TargetType);
            bool revives = plan.Effect != null && plan.Effect.Revives;
            switch (scope)
            {
                case TargetScope.SingleEnemy:
                    return SingleTarget(plan, plan.Action.TargetId, wantOpponent: true, wantFallen: false, $"use {ability.Name} on");
                case TargetScope.SingleAlly:
                    return SingleTarget(plan, plan.Action.TargetId, wantOpponent: false, wantFallen: revives, $"use {ability.Name} on");
                case TargetScope.AllEnemies:
                    plan.Targets.AddRange(LivingOpponents(actor));
                    break;
                case TargetScope.AllAllies:
                    plan.Targets.AddRange(LivingAllies(actor));
                    break;
                default:
                    plan.Targets.Add(actor);
                    break;
            }
            return plan.Targets.Count == 0 ? $"{ability.Name} has nobody to reach." : null;
        }

        private string PrepareItem(Plan plan)
        {
            var actor = plan.Actor;
            string id = plan.Action.ItemId;
            if (!actor.IsParty)
                return "Only the party uses items.";
            if (string.IsNullOrEmpty(id))
                return "Choose an item.";
            var item = _world.Inventory.GetAllItems().FirstOrDefault(i => i != null && i.ItemId == id);
            if (item == null)
                return $"The party has no {_world.ItemName(id)}.";
            if (!(item is IConsumable consumable) || !consumable.UsableInCombat)
                return $"{item.Name} cannot be used in battle.";

            plan.Item = item;
            plan.ItemEffect = ItemEffects.Normalize(consumable.EffectId);
            plan.Magnitude = consumable.EffectMagnitude;
            switch (plan.ItemEffect)
            {
                case ItemEffects.RestoreHealth:
                case ItemEffects.RestoreMana:
                case ItemEffects.Cure:
                    return SingleTarget(plan, plan.Action.TargetId, wantOpponent: false, wantFallen: false, $"use {item.Name} on");
                case ItemEffects.Revive:
                    return SingleTarget(plan, plan.Action.TargetId, wantOpponent: false, wantFallen: true, $"use {item.Name} on");
                case ItemEffects.Damage:
                    return SingleTarget(plan, plan.Action.TargetId, wantOpponent: true, wantFallen: false, $"use {item.Name} on");
                default:
                    if (string.IsNullOrEmpty(plan.ItemEffect) || _world.StatusEffects.GetEffect(plan.ItemEffect) == null)
                        return $"{item.Name} cannot be used in battle.";
                    var target = Get(plan.Action.TargetId);
                    return SingleTarget(plan, plan.Action.TargetId, wantOpponent: target != null && target.Side != actor.Side,
                        wantFallen: false, $"use {item.Name} on");
            }
        }

        private int Price(Combatant actor, ResourceCost cost)
        {
            float amount = cost.Amount;
            if (cost.IsPercentage)
                amount *= Maximum(actor, Resources.Parse(cost.ResourceType));
            return (int)Math.Ceiling(Math.Max(0f, amount) - 1e-4f);
        }

        private static float Maximum(Combatant combatant, Resource resource) =>
            resource == Resource.Mana ? combatant.MaxMana : resource == Resource.Health ? combatant.MaxHealth : 0f;

        // ---- Doing an action ----

        private sealed class Resolution
        {
            public readonly List<TargetOutcome> Outcomes = new List<TargetOutcome>();
            public readonly List<(Combatant Target, string EffectId)> Apply = new List<(Combatant, string)>();
            public readonly List<(Combatant Target, string EffectId)> Remove = new List<(Combatant, string)>();
            public readonly HashSet<int> AliveBefore = new HashSet<int>();
            public bool Escaped;
        }

        private ActionResult Act(Plan plan)
        {
            var actor = plan.Actor;
            var action = plan.Action;
            var done = new Resolution();
            foreach (var combatant in _all.Where(c => c.IsAlive))
                done.AliveBefore.Add(combatant.Id);

            switch (action.Kind)
            {
                case BattleActionKind.Attack:
                    var target = plan.Targets[0];
                    var hit = _world.Formula.Hit(actor, target, 1f, CombatStat.Attack, _world.Random);
                    done.Outcomes.Add(Damage(target, hit));
                    Write(hit.Missed ? $"{actor.Name} attacks {target.Name} and misses"
                        : $"{actor.Name} attacks {target.Name} for {done.Outcomes[0].Damage}" + (hit.Critical ? " (critical)" : ""));
                    break;

                case BattleActionKind.Defend:
                    actor.IsDefending = true;
                    done.Outcomes.Add(new TargetOutcome(actor.Id));
                    Write($"{actor.Name} defends");
                    break;

                case BattleActionKind.Ability:
                    var used = _world.Executor.ExecuteAbility(plan.Ability.AbilityId, actor.Id, PrimaryTarget(plan), Round,
                        (entity, resource, amount) => CanPay(Get(entity), resource, amount),
                        (entity, resource, amount) => Pay(Get(entity), resource, amount),
                        (caster, primary, abilityId) => ResolveAbility(plan, done));
                    if (!used.Success)
                    {
                        // Checked above, so this is the Abilities module refusing for a reason of its own
                        _world.Logger?.LogWarning($"{plan.Ability.Name} was refused by the ability system: {used.FailureReason}", Category);
                        return ActionResult.Refused(used.FailureReason, action);
                    }
                    Write($"{actor.Name} uses {plan.Ability.Name}" + Describe(done.Outcomes));
                    break;

                case BattleActionKind.Item:
                    UseItem(plan, done);
                    Write($"{actor.Name} uses {plan.Item.Name}" + Describe(done.Outcomes));
                    break;

                case BattleActionKind.Flee:
                    done.Escaped = _world.Random.NextDouble() < _world.Settings.FleeChance;
                    Write(done.Escaped ? $"{actor.Name} leads the party away" : $"{actor.Name} tries to flee but cannot get away");
                    break;
            }

            // Who the action brought down
            for (int i = 0; i < done.Outcomes.Count; i++)
            {
                var outcome = done.Outcomes[i];
                var target = Get(outcome.TargetId);
                if (target != null && done.AliveBefore.Contains(target.Id) && !target.IsAlive)
                    done.Outcomes[i] = outcome.WithDefeated();
            }

            var targets = done.Outcomes.ToList();
            _world.Bus.Publish(new ActionPerformedEvent(Round, actor.Id, action.Kind, plan.Ability?.AbilityId,
                plan.Item?.ItemId, targets, done.Escaped));

            foreach (var outcome in targets.Where(o => o.Defeated))
                Defeated(Get(outcome.TargetId), actor.Id);

            foreach (var (target, effectId) in done.Remove)
            {
                if (target.IsAlive)
                    _world.StatusEffects.RemoveEffect(target.Id, effectId);
            }
            foreach (var (target, effectId) in done.Apply)
            {
                if (target.IsAlive)
                    _world.StatusEffects.ApplyEffect(target.Id, effectId, actor.Id.ToString(), target.TurnsTaken);
            }

            EndTurn(actor);
            if (done.Escaped)
                Finish(BattleOutcome.Fled);
            else if (!CheckOutcome())
                Advance();

            return ActionResult.Performed(actor.Id, action, targets, done.Escaped);
        }

        private static int PrimaryTarget(Plan plan)
        {
            if (plan.Targets.Count == 0)
                return -1;
            var scope = plan.Effect?.Scope ?? TargetScopes.FromTargetType(plan.Ability.TargetType);
            return scope == TargetScope.Self ? plan.Actor.Id : plan.Targets[0].Id;
        }

        private TargetOutcome Damage(Combatant target, HitResult hit, string element = null)
        {
            if (hit.Missed)
                return new TargetOutcome(target.Id, missed: true);
            int damage = hit.Damage;
            if (!string.IsNullOrEmpty(element))
                damage = Combatant.Round(_world.Resistances.Apply(target.Id, element, damage));
            return new TargetOutcome(target.Id, damage: target.TakeDamage(damage), critical: hit.Critical);
        }

        private void ResolveAbility(Plan plan, Resolution done)
        {
            var actor = plan.Actor;
            var effect = plan.Effect;
            if (effect == null)
            {
                foreach (var target in plan.Targets)
                    done.Outcomes.Add(new TargetOutcome(target.Id));
                return;
            }

            foreach (var target in plan.Targets)
            {
                TargetOutcome outcome;
                switch (effect.Kind)
                {
                    case AbilityKind.Damage:
                        outcome = Damage(target, _world.Formula.Hit(actor, target, effect.Power, effect.ScalesWith, _world.Random), effect.Element);
                        break;
                    case AbilityKind.Heal:
                        int amount = _world.Formula.Heal(actor, target, effect.Power, effect.ScalesWith, _world.Random);
                        if (effect.Revives && !target.IsAlive)
                        {
                            int back = target.Revive(amount);
                            _defeated.Remove(target.Id);
                            outcome = new TargetOutcome(target.Id, healing: back, revived: true);
                        }
                        else
                        {
                            outcome = new TargetOutcome(target.Id, healing: target.Heal(amount));
                        }
                        break;
                    default:
                        outcome = new TargetOutcome(target.Id);
                        break;
                }
                done.Outcomes.Add(outcome);

                if (outcome.Missed)
                    continue;
                foreach (var removed in effect.RemovesEffectIds ?? new List<string>())
                    done.Remove.Add((target, removed));
                foreach (var applied in effect.StatusEffectIds ?? new List<string>())
                {
                    if (effect.StatusEffectChance >= 1f || _world.Random.NextDouble() < effect.StatusEffectChance)
                        done.Apply.Add((target, applied));
                }
            }
        }

        private void UseItem(Plan plan, Resolution done)
        {
            var target = plan.Targets[0];
            int amount = Combatant.Round(plan.Magnitude);
            switch (plan.ItemEffect)
            {
                case ItemEffects.RestoreHealth:
                    done.Outcomes.Add(new TargetOutcome(target.Id, healing: target.Heal(amount)));
                    break;
                case ItemEffects.RestoreMana:
                    done.Outcomes.Add(new TargetOutcome(target.Id, manaRestored: target.RestoreMana(amount)));
                    break;
                case ItemEffects.Revive:
                    int health = plan.Magnitude <= 1f ? Combatant.Round(plan.Magnitude * target.MaxHealth) : amount;
                    int back = target.Revive(health);
                    _defeated.Remove(target.Id);
                    done.Outcomes.Add(new TargetOutcome(target.Id, healing: back, revived: true));
                    break;
                case ItemEffects.Cure:
                    foreach (var active in _world.StatusEffects.GetActiveEffects(target.Id))
                    {
                        var type = _world.StatusEffects.GetEffect(active.EffectId)?.Type;
                        if (type == EffectType.Debuff || type == EffectType.DamageOverTime || type == EffectType.StatusCondition)
                            done.Remove.Add((target, active.EffectId));
                    }
                    done.Outcomes.Add(new TargetOutcome(target.Id));
                    break;
                case ItemEffects.Damage:
                    done.Outcomes.Add(new TargetOutcome(target.Id, damage: target.TakeDamage(amount)));
                    break;
                default:
                    done.Apply.Add((target, plan.ItemEffect));
                    done.Outcomes.Add(new TargetOutcome(target.Id));
                    break;
            }

            if (!(plan.Item is IConsumable consumable) || consumable.IsConsumedOnUse)
                _world.Inventory.Remove(plan.Item);
        }

        private bool CanPay(Combatant combatant, string resourceType, float amount)
        {
            if (combatant == null)
                return false;
            int price = (int)Math.Ceiling(Math.Max(0f, amount) - 1e-4f);
            switch (Resources.Parse(resourceType))
            {
                case Resource.Mana: return combatant.Mana >= price;
                case Resource.Health: return combatant.Health > price;
                default: return false;
            }
        }

        private void Pay(Combatant combatant, string resourceType, float amount)
        {
            if (combatant == null)
                return;
            int price = (int)Math.Ceiling(Math.Max(0f, amount) - 1e-4f);
            switch (Resources.Parse(resourceType))
            {
                case Resource.Mana: combatant.SpendMana(price); break;
                case Resource.Health: combatant.SpendHealth(price); break;
            }
        }

        private string Describe(List<TargetOutcome> outcomes)
        {
            var parts = new List<string>();
            foreach (var outcome in outcomes)
            {
                string name = Get(outcome.TargetId)?.Name ?? "?";
                if (outcome.Missed)
                    parts.Add($"misses {name}");
                else if (outcome.Revived)
                    parts.Add($"{name} gets back up with {outcome.Healing}");
                else if (outcome.Damage > 0)
                    parts.Add($"{name} takes {outcome.Damage}" + (outcome.Critical ? " (critical)" : ""));
                else if (outcome.Healing > 0)
                    parts.Add($"{name} recovers {outcome.Healing}");
                else if (outcome.ManaRestored > 0)
                    parts.Add($"{name} recovers {outcome.ManaRestored} MP");
            }
            return parts.Count > 0 ? ": " + string.Join(", ", parts) : "";
        }

        private void Write(string line)
        {
            _log.Add(line);
            if (_log.Count > MaxLogLines)
                _log.RemoveAt(0);
            _world.Write(line);
        }
    }

    internal enum Resource
    {
        None,
        Health,
        Mana
    }

    internal static class Resources
    {
        /// <summary>
        /// Resource names abilities and effects use: "MP"/"Mana" and "HP"/"Health", in any case.
        /// </summary>
        public static Resource Parse(string name)
        {
            switch ((name ?? "").Trim().ToLowerInvariant())
            {
                case "mp":
                case "mana": return Resource.Mana;
                case "hp":
                case "health": return Resource.Health;
                default: return Resource.None;
            }
        }
    }
}
