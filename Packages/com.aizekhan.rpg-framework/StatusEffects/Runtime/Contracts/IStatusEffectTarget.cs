namespace RPGFramework.StatusEffects
{
    /// <summary>
    /// Where status effects land in the game. The manager works out what each active effect contributes and
    /// hands it here; the game (or an Integration bridge) applies it to its own stats, resources, movement and input.
    /// Pass one to <see cref="StatusEffectManager"/>; without one the manager only tracks effects and answers queries.
    /// </summary>
    public interface IStatusEffectTarget
    {
        /// <summary>
        /// Put a stat modifier on an entity. Called when an effect with a Stat modifier is applied, and again
        /// (after <see cref="RemoveStatModifier"/>) when its stack count changes.
        /// </summary>
        /// <param name="entityId">Entity the effect is on</param>
        /// <param name="modifierId">Unique per entity, effect and modifier; use it to remove the modifier later</param>
        /// <param name="statId">The modifier's TargetId, e.g. "AttackPower"</param>
        /// <param name="operation">Add: stat + value. Multiply: stat * value. Override: stat = value</param>
        /// <param name="value">Value with stacks already folded in (Add: value x stacks, Multiply: value ^ stacks)</param>
        void ApplyStatModifier(int entityId, string modifierId, string statId, ModifierOperation operation, float value);

        /// <summary>
        /// Take off a stat modifier put on by <see cref="ApplyStatModifier"/>. Unknown ids should be ignored.
        /// </summary>
        void RemoveStatModifier(int entityId, string modifierId);

        /// <summary>
        /// Current value of a resource (e.g. "Mana", "Health"). Needed for Multiply and Override resource changes,
        /// and for Multiply / Override damage and healing. Return false if the entity has no such resource.
        /// </summary>
        bool TryGetResource(int entityId, string resourceId, out float value);

        /// <summary>
        /// Change a resource by an amount (negative drains). Called for Resource modifiers on every tick, or once
        /// per application for effects without a tick interval.
        /// </summary>
        void ChangeResource(int entityId, string resourceId, float amount);

        /// <summary>
        /// The entity's combined movement multiplier changed (1 = normal speed, 0 = rooted).
        /// See <see cref="StatusEffectManager.GetMovementMultiplier"/>.
        /// </summary>
        void SetMovementMultiplier(int entityId, float multiplier);

        /// <summary>
        /// A control (the TargetId of a Control modifier, e.g. "Stun", "Silence") started or stopped on an entity.
        /// Called once when the first effect gives it and once when the last effect giving it ends.
        /// </summary>
        void SetControl(int entityId, string controlId, bool active);
    }
}
