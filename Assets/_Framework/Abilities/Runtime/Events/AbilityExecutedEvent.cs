namespace RPGFramework.Abilities
{
    /// <summary>
    /// Event raised when an ability is executed.
    /// </summary>
    public struct AbilityExecutedEvent
    {
        /// <summary>Ability ID that was executed</summary>
        public string AbilityId;

        /// <summary>Entity that cast the ability</summary>
        public int CasterEntityId;

        /// <summary>Target entity (-1 for no target)</summary>
        public int TargetEntityId;

        /// <summary>Timestamp of execution</summary>
        public float Timestamp;

        /// <summary>Whether execution succeeded</summary>
        public bool Success;
    }
}
