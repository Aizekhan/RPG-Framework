using System.Collections.Generic;

namespace RPGFramework.Abilities
{
    /// <summary>
    /// Base interface for all abilities.
    /// Abilities are reusable definitions that can be instantiated per entity.
    /// </summary>
    public interface IAbility
    {
        /// <summary>Unique ability identifier</summary>
        string AbilityId { get; }

        /// <summary>Display name</summary>
        string Name { get; }

        /// <summary>Ability metadata</summary>
        AbilityMetadata Metadata { get; }

        /// <summary>Ability type (Active, Passive, Triggered, etc.)</summary>
        AbilityType Type { get; }

        /// <summary>Cooldown duration in seconds</summary>
        float Cooldown { get; }

        /// <summary>Resource costs for execution</summary>
        IReadOnlyList<ResourceCost> ResourceCosts { get; }

        /// <summary>Casting information</summary>
        CastInfo CastInfo { get; }

        /// <summary>Target type requirement</summary>
        TargetType TargetType { get; }

        /// <summary>Maximum range (0 for melee/self)</summary>
        float Range { get; }
    }
}
