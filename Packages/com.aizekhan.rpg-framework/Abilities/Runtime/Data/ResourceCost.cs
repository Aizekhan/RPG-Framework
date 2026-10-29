namespace RPGFramework.Abilities
{
    /// <summary>
    /// Represents resource costs for ability execution.
    /// Supports HP, MP, Stamina, or custom resources.
    /// </summary>
    public struct ResourceCost
    {
        /// <summary>Resource type identifier (e.g., "HP", "MP", "Stamina")</summary>
        public string ResourceType;

        /// <summary>Amount of resource required</summary>
        public float Amount;

        /// <summary>Whether cost is percentage of max resource (true) or flat amount (false)</summary>
        public bool IsPercentage;

        /// <summary>
        /// An upkeep: <see cref="Amount"/> is taken every second while a toggle is on or a channel lasts, not when used
        /// (using it needs one second's worth). A toggle the caster can no longer pay for switches off, a channel ends.
        /// </summary>
        public bool PerSecond;

        public ResourceCost(string resourceType, float amount, bool isPercentage = false, bool perSecond = false)
        {
            ResourceType = resourceType;
            Amount = amount;
            IsPercentage = isPercentage;
            PerSecond = perSecond;
        }
    }
}
