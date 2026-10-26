using System.Collections.Generic;

namespace RPGFramework.Abilities
{
    /// <summary>
    /// Default implementation of IAbility.
    /// Represents a reusable ability definition.
    /// </summary>
    public class Ability : IAbility
    {
        public string AbilityId { get; }
        public string Name { get; }
        public AbilityMetadata Metadata { get; }
        public AbilityType Type => Metadata.Type;
        public float Cooldown => Metadata.Cooldown;
        public IReadOnlyList<ResourceCost> ResourceCosts { get; }
        public CastInfo CastInfo { get; }
        public TargetType TargetType { get; }
        public float Range => Metadata.Range;

        public Ability(
            AbilityMetadata metadata,
            List<ResourceCost> resourceCosts = null,
            CastInfo? castInfo = null,
            TargetType targetType = TargetType.None)
        {
            AbilityId = metadata.AbilityId;
            Name = metadata.Name;
            Metadata = metadata;
            ResourceCosts = resourceCosts ?? new List<ResourceCost>();
            CastInfo = castInfo ?? CastInfo.Instant;
            TargetType = targetType;
        }
    }
}
