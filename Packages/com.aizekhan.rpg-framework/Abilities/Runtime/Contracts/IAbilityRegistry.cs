using System.Collections.Generic;

namespace RPGFramework.Abilities
{
    /// <summary>
    /// Registry for ability definitions.
    /// Provides lookup and management of all abilities in the game.
    /// </summary>
    public interface IAbilityRegistry
    {
        /// <summary>Register new ability definition</summary>
        void RegisterAbility(IAbility ability);

        /// <summary>Unregister ability</summary>
        bool UnregisterAbility(string abilityId);

        /// <summary>Get ability by ID</summary>
        IAbility GetAbility(string abilityId);

        /// <summary>Check if ability exists</summary>
        bool HasAbility(string abilityId);

        /// <summary>Get all registered abilities</summary>
        IReadOnlyList<IAbility> GetAllAbilities();

        /// <summary>Get abilities by type</summary>
        IReadOnlyList<IAbility> GetAbilitiesByType(AbilityType type);

        /// <summary>Clear all registered abilities</summary>
        void Clear();
    }
}
