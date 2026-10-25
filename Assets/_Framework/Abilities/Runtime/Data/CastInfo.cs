namespace RPGFramework.Abilities
{
    /// <summary>
    /// Information about ability casting mechanics.
    /// Defines cast time, channel duration, and casting rules.
    /// </summary>
    public struct CastInfo
    {
        /// <summary>Time required to cast ability (in seconds)</summary>
        public float CastTime;

        /// <summary>Duration of channeling (in seconds, 0 for non-channeled)</summary>
        public float ChannelDuration;

        /// <summary>Whether casting can be interrupted</summary>
        public bool IsInterruptible;

        /// <summary>Whether caster can move while casting</summary>
        public bool CanMoveWhileCasting;

        public CastInfo(float castTime, float channelDuration = 0f, bool isInterruptible = true, bool canMoveWhileCasting = false)
        {
            CastTime = castTime;
            ChannelDuration = channelDuration;
            IsInterruptible = isInterruptible;
            CanMoveWhileCasting = canMoveWhileCasting;
        }

        /// <summary>Instant cast (no cast time)</summary>
        public static CastInfo Instant => new CastInfo(0f);
    }
}
