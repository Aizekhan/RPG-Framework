namespace RPGFramework.Abilities
{
    /// <summary>
    /// One hit of one use of an ability (<see cref="IAbilityImpacts"/>): which use, which hit of how many, when.
    /// </summary>
    public readonly struct AbilityImpact
    {
        public AbilityImpact(int useId, string abilityId, int casterEntityId, int targetEntityId, int hit, int hits,
            float time, bool delayed)
        {
            UseId = useId;
            AbilityId = abilityId;
            CasterEntityId = casterEntityId;
            TargetEntityId = targetEntityId;
            Hit = hit;
            Hits = hits;
            Time = time;
            Delayed = delayed;
        }

        /// <summary>
        /// The use it belongs to (<see cref="AbilityExecutionResult.UseId"/>); 0 for none.
        /// </summary>
        public int UseId { get; }

        public string AbilityId { get; }
        public int CasterEntityId { get; }
        public int TargetEntityId { get; }

        /// <summary>
        /// Which hit, from 1 (0 for a channel's tick and for the use itself).
        /// </summary>
        public int Hit { get; }

        /// <summary>
        /// Hits the use makes.
        /// </summary>
        public int Hits { get; }

        /// <summary>
        /// When it lands (or went off, for the use itself).
        /// </summary>
        public float Time { get; }

        /// <summary>
        /// The use's hits come after it goes off (<see cref="CastInfo.HasDelayedImpacts"/>).
        /// </summary>
        public bool Delayed { get; }
    }
}
