using System;
using System.Collections.Generic;
using RPGFramework.Abilities;

namespace RPGFramework.TurnBased
{
    /// <summary>
    /// What an ability does when it lands in battle. The Abilities module owns the rest of the ability (name, costs,
    /// cooldown, target type) and calls the game back with the ability id; this is the game's answer for that id.
    /// An ability without one does nothing but pay its costs.
    /// </summary>
    [Serializable]
    public sealed class AbilityEffectData
    {
        /// <summary>
        /// The ability (registered in the ability registry) this describes.
        /// </summary>
        public string AbilityId = "";

        /// <summary>
        /// Who it hits. <see cref="TargetScopes.FromTargetType"/> gives the scope matching the ability's target type.
        /// </summary>
        public TargetScope Scope = TargetScope.SingleEnemy;

        public AbilityKind Kind = AbilityKind.Damage;

        /// <summary>
        /// Multiplier of the scaling stat (1 = as strong as a plain attack with the same stat).
        /// </summary>
        public float Power = 1f;

        /// <summary>
        /// The caster's stat the damage or healing grows with: Attack for weapon skills (they can miss), Magic for
        /// spells (they never miss).
        /// </summary>
        public CombatStat ScalesWith = CombatStat.Magic;

        /// <summary>
        /// Status effects put on every target that is hit and still standing (poison, attack up, stun).
        /// </summary>
        public List<string> StatusEffectIds = new List<string>();

        /// <summary>
        /// Chance (0..1) each status effect lands on each target.
        /// </summary>
        public float StatusEffectChance = 1f;

        /// <summary>
        /// Status effects taken off every target (a cure).
        /// </summary>
        public List<string> RemovesEffectIds = new List<string>();

        /// <summary>
        /// Targets fallen allies (single ally scope) and brings them back with the healing amount.
        /// </summary>
        public bool Revives;

        /// <summary>
        /// The damage's element ("fire"): the target's resistances change it (<see cref="CombatantData.Resistances"/>,
        /// <see cref="TurnBasedWorld.Resistances"/>); empty for none.
        /// </summary>
        public string Element = "";

        public bool TargetsAllies => Scope == TargetScope.SingleAlly || Scope == TargetScope.AllAllies || Scope == TargetScope.Self;
    }

    /// <summary>
    /// Who an action reaches.
    /// </summary>
    public enum TargetScope
    {
        SingleEnemy,
        AllEnemies,
        SingleAlly,
        AllAllies,
        Self
    }

    public enum AbilityKind
    {
        /// <summary>
        /// Deals damage (and may apply effects).
        /// </summary>
        Damage,

        /// <summary>
        /// Restores health (and may apply effects, cure or revive).
        /// </summary>
        Heal,

        /// <summary>
        /// Only applies or removes status effects.
        /// </summary>
        Support
    }

    public static class TargetScopes
    {
        /// <summary>
        /// The scope an ability's target type means in a turn-based battle: Enemy → one enemy, Ally → one ally,
        /// Area/AllEnemies → every enemy, AllAllies → every ally, Self/None → the caster, Any → one enemy.
        /// </summary>
        public static TargetScope FromTargetType(TargetType type)
        {
            switch (type)
            {
                case TargetType.Ally: return TargetScope.SingleAlly;
                case TargetType.Area:
                case TargetType.AllEnemies: return TargetScope.AllEnemies;
                case TargetType.AllAllies: return TargetScope.AllAllies;
                case TargetType.Self:
                case TargetType.None: return TargetScope.Self;
                default: return TargetScope.SingleEnemy;
            }
        }

        public static bool IsSingle(this TargetScope scope) => scope == TargetScope.SingleEnemy || scope == TargetScope.SingleAlly;
    }
}
