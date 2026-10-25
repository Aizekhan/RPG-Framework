namespace RPGFramework.Abilities
{
    /// <summary>
    /// Metadata and configuration for an ability.
    /// Contains display information, type, and gameplay parameters.
    /// </summary>
    public struct AbilityMetadata
    {
        /// <summary>Unique identifier</summary>
        public string AbilityId;

        /// <summary>Display name</summary>
        public string Name;

        /// <summary>Description text</summary>
        public string Description;

        /// <summary>Icon identifier (for UI)</summary>
        public string IconId;

        /// <summary>Ability type</summary>
        public AbilityType Type;

        /// <summary>Cooldown duration in seconds</summary>
        public float Cooldown;

        /// <summary>Maximum range (0 for melee/self)</summary>
        public float Range;

        /// <summary>Area of effect radius (0 for single target)</summary>
        public float AoeRadius;

        /// <summary>Minimum level required to use</summary>
        public int MinLevel;

        /// <summary>Maximum number of charges (0 for unlimited)</summary>
        public int MaxCharges;

        public AbilityMetadata(
            string abilityId,
            string name,
            AbilityType type = AbilityType.Active,
            float cooldown = 0f,
            float range = 0f,
            float aoeRadius = 0f,
            int minLevel = 0,
            int maxCharges = 0)
        {
            AbilityId = abilityId;
            Name = name;
            Description = string.Empty;
            IconId = string.Empty;
            Type = type;
            Cooldown = cooldown;
            Range = range;
            AoeRadius = aoeRadius;
            MinLevel = minLevel;
            MaxCharges = maxCharges;
        }
    }
}
