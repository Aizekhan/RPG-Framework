namespace RPGFramework.Companions.Runtime
{
    /// <summary>
    /// Represents a special ability or skill that a companion can use.
    /// Examples: Special attacks, buffs, utility abilities, passive bonuses.
    /// </summary>
    public interface ICompanionAbility
    {
        /// <summary>
        /// Unique identifier for this ability.
        /// </summary>
        string AbilityId { get; }

        /// <summary>
        /// Display name of the ability.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Description of what the ability does.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Cooldown time in seconds.
        /// </summary>
        float Cooldown { get; }

        /// <summary>
        /// Time remaining until ability can be used again.
        /// </summary>
        float RemainingCooldown { get; }

        /// <summary>
        /// Whether the ability is currently on cooldown.
        /// </summary>
        bool IsOnCooldown { get; }

        /// <summary>
        /// Minimum companion level required to use this ability.
        /// </summary>
        int RequiredLevel { get; }

        /// <summary>
        /// Whether the ability is passive (always active) or active (must be triggered).
        /// </summary>
        bool IsPassive { get; }

        /// <summary>
        /// Checks if the ability can be used right now.
        /// </summary>
        bool CanUse();

        /// <summary>
        /// Uses the ability.
        /// </summary>
        /// <returns>True if ability was successfully used.</returns>
        bool Use(ICompanion companion, int targetEntityId = -1);

        /// <summary>
        /// Updates the ability cooldown timer.
        /// </summary>
        void UpdateCooldown(float deltaTime);
    }
}
