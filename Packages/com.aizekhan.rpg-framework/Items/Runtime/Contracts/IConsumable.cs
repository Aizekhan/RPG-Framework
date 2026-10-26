namespace RPGFramework.Items
{
    /// <summary>
    /// Consumable item that can be used for effects.
    /// Extends IItem with consumable-specific properties.
    /// Examples: potions, food, scrolls, elixirs.
    /// </summary>
    public interface IConsumable : IItem
    {
        /// <summary>
        /// Cooldown category for shared cooldowns.
        /// Items in same category share cooldown timer.
        /// Examples: "Potion", "Food", "Elixir"
        /// Empty string = no cooldown category (independent cooldown).
        /// </summary>
        string CooldownCategory { get; }

        /// <summary>
        /// Cooldown duration in seconds after use.
        /// 0 = no cooldown, can be used repeatedly.
        /// </summary>
        float CooldownDuration { get; }

        /// <summary>
        /// Whether this consumable can be used in combat.
        /// False = only usable out of combat (e.g., resurrection items).
        /// </summary>
        bool UsableInCombat { get; }

        /// <summary>
        /// Whether this consumable is consumed on use (single-use).
        /// True = item removed from inventory after use (typical).
        /// False = infinite uses (e.g., quest items that trigger events).
        /// </summary>
        bool IsConsumedOnUse { get; }

        /// <summary>
        /// Effect type identifier for game-specific effect application.
        /// Examples: "RestoreHP", "RestoreMana", "ApplyBuff", "RemoveDebuff"
        /// Framework stores the ID, game layer implements effect logic.
        /// </summary>
        string EffectId { get; }

        /// <summary>
        /// Effect magnitude (heal amount, buff strength, etc.).
        /// Interpretation depends on EffectId.
        /// Examples: 50 for "RestoreHP" = heal 50 HP, 0.2 for "ApplyBuff" = 20% boost.
        /// </summary>
        float EffectMagnitude { get; }
    }
}
