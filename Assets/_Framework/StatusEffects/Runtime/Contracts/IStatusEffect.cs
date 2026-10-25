using System.Collections.Generic;

namespace RPGFramework.StatusEffects
{
    /// <summary>
    /// Core interface for status effect definitions (buffs, debuffs, DoT, HoT, etc.)
    /// </summary>
    public interface IStatusEffect
    {
        /// <summary>
        /// Unique identifier for this effect.
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Display name of the effect.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Description of what this effect does.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Type of effect (Buff, Debuff, DoT, HoT, etc.)
        /// </summary>
        EffectType Type { get; }

        /// <summary>
        /// Duration of the effect in seconds.
        /// -1 = permanent, 0 = instant, >0 = timed
        /// </summary>
        float Duration { get; }

        /// <summary>
        /// Can this effect stack multiple times?
        /// </summary>
        bool IsStackable { get; }

        /// <summary>
        /// Maximum number of stacks allowed.
        /// </summary>
        int MaxStacks { get; }

        /// <summary>
        /// Interval between ticks for DoT/HoT effects (seconds).
        /// </summary>
        float TickInterval { get; }

        /// <summary>
        /// List of modifiers this effect applies.
        /// </summary>
        IReadOnlyList<IEffectModifier> Modifiers { get; }
    }
}
