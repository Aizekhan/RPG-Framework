namespace RPGFramework.Abilities
{
    /// <summary>
    /// Event raised when an ability cooldown expires.
    /// </summary>
    public struct CooldownExpiredEvent
    {
        /// <summary>Ability ID that is now ready</summary>
        public string AbilityId;

        /// <summary>Entity with the expired cooldown</summary>
        public int EntityId;

        /// <summary>Timestamp when cooldown expired</summary>
        public float Timestamp;
    }
}
