using System;
using System.Collections.Generic;
using System.Linq;

namespace RPGFramework.TurnBased
{
    /// <summary>
    /// Chooses what a combatant does on its turn (enemies, or a party member on auto-battle). Use only the random
    /// given, so a fixed seed replays the same battle. An action the battle refuses is replaced by a plain attack.
    /// </summary>
    public interface IBattleAi
    {
        BattleAction Choose(Battle battle, Combatant actor, Random random);
    }

    /// <summary>
    /// A sensible simple fighter:
    /// <list type="number">
    /// <item>an ally (itself included) under <see cref="HealBelow"/> of its health is healed with the strongest heal
    /// it can use now, the most hurt ally first;</item>
    /// <item>otherwise it uses the strongest damage it can afford now: the plain attack or a damage ability
    /// (expected strength = scaling stat x power, times the number of enemies for abilities hitting them all), on the
    /// opponent with the least health (<see cref="TargetWeakest"/>) or a random one.</item>
    /// </list>
    /// Support-only abilities (buffs, debuffs) are left to a game's own AI.
    /// </summary>
    public sealed class SimpleBattleAi : IBattleAi
    {
        /// <summary>
        /// Share of maximum health under which an ally counts as hurt.
        /// </summary>
        public float HealBelow { get; set; } = 0.5f;

        /// <summary>
        /// Attack the opponent with the least health; false picks one at random.
        /// </summary>
        public bool TargetWeakest { get; set; } = true;

        public BattleAction Choose(Battle battle, Combatant actor, Random random)
        {
            var heal = ChooseHeal(battle, actor);
            if (heal.HasValue)
                return heal.Value;

            var foes = battle.LivingOpponents(actor);
            if (foes.Count == 0)
                return BattleAction.Defend();
            var target = TargetWeakest
                ? foes.OrderBy(f => f.Health).ThenBy(f => f.Slot).First()
                : foes[random.Next(foes.Count)];

            var best = BattleAction.Attack(target.Id);
            float bestStrength = actor.Attack;
            foreach (var abilityId in actor.AbilityIds)
            {
                var effect = battle.EffectOf(abilityId);
                if (effect == null || effect.Kind != AbilityKind.Damage)
                    continue;
                float strength = actor.Stat(effect.ScalesWith) * effect.Power;
                if (effect.Scope == TargetScope.AllEnemies)
                    strength *= foes.Count;
                else if (effect.Scope != TargetScope.SingleEnemy)
                    continue;

                var action = BattleAction.Ability(abilityId, target.Id);
                if (strength > bestStrength && battle.CanPerform(action))
                {
                    best = action;
                    bestStrength = strength;
                }
            }
            return best;
        }

        private BattleAction? ChooseHeal(Battle battle, Combatant actor)
        {
            var hurt = battle.LivingAllies(actor)
                .Where(a => a.HealthFraction < HealBelow)
                .OrderBy(a => a.HealthFraction).ThenBy(a => a.Slot)
                .ToList();
            if (hurt.Count == 0)
                return null;

            BattleAction? best = null;
            float bestStrength = 0f;
            foreach (var abilityId in actor.AbilityIds)
            {
                var effect = battle.EffectOf(abilityId);
                if (effect == null || effect.Kind != AbilityKind.Heal || effect.Revives)
                    continue;

                Combatant patient;
                switch (effect.Scope)
                {
                    case TargetScope.SingleAlly:
                    case TargetScope.AllAllies:
                        patient = hurt[0];
                        break;
                    case TargetScope.Self:
                        patient = hurt.Contains(actor) ? actor : null;
                        break;
                    default:
                        patient = null;
                        break;
                }
                if (patient == null)
                    continue;

                float strength = actor.Stat(effect.ScalesWith) * effect.Power * (effect.Scope == TargetScope.AllAllies ? hurt.Count : 1);
                var action = BattleAction.Ability(abilityId, patient.Id);
                if (strength > bestStrength && battle.CanPerform(action))
                {
                    best = action;
                    bestStrength = strength;
                }
            }
            return best;
        }
    }
}
