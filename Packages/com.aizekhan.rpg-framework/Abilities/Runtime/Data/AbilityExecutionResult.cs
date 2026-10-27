namespace RPGFramework.Abilities
{
    /// <summary>
    /// Result of ability execution attempt.
    /// Contains success status, failure reason, and execution metadata.
    /// </summary>
    public struct AbilityExecutionResult
    {
        /// <summary>Whether execution succeeded</summary>
        public bool Success;

        /// <summary>Failure reason (if Success is false)</summary>
        public string FailureReason;

        /// <summary>Ability ID that was executed</summary>
        public string AbilityId;

        /// <summary>Caster entity ID</summary>
        public int CasterEntityId;

        /// <summary>Target entity ID (-1 for none)</summary>
        public int TargetEntityId;

        /// <summary>Timestamp of execution</summary>
        public float Timestamp;

        /// <summary>True when the ability began a cast: it pays and lands when the cast completes</summary>
        public bool CastStarted;

        /// <summary>True when this use switched a toggle ability off (nothing paid, nothing applied)</summary>
        public bool ToggledOff;

        public static AbilityExecutionResult CreateSuccess(string abilityId, int casterEntityId, int targetEntityId, float timestamp)
        {
            return new AbilityExecutionResult
            {
                Success = true,
                FailureReason = string.Empty,
                AbilityId = abilityId,
                CasterEntityId = casterEntityId,
                TargetEntityId = targetEntityId,
                Timestamp = timestamp
            };
        }

        public static AbilityExecutionResult CreateFailure(string abilityId, int casterEntityId, string reason, float timestamp)
        {
            return new AbilityExecutionResult
            {
                Success = false,
                FailureReason = reason,
                AbilityId = abilityId,
                CasterEntityId = casterEntityId,
                TargetEntityId = -1,
                Timestamp = timestamp
            };
        }
    }
}
