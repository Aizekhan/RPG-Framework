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

        public ResourceCost(string resourceType, float amount, bool isPercentage = false)
        {
            ResourceType = resourceType;
            Amount = amount;
            IsPercentage = isPercentage;
        }
    }
}
