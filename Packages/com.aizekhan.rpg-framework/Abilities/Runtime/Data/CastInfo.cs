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

        /// <summary>
        /// Seconds between the ticks of a channel (a beam that burns every 0.5 s); 0: the channel lands once, when
        /// it starts. Each tick calls the ability's effect again and raises <see cref="AbilityChannelTickEvent"/>.
        /// </summary>
        public float ChannelTickInterval;

        /// <summary>
        /// Seconds from the moment it goes off (paid, charge used, cooldown started) to its first hit: the swing's
        /// contact, the arrow's flight, the meteor's fall. 0 (the default): it hits at once, as before.
        /// <see cref="IAbilityImpacts.ImpactSpeedFor"/> can make it faster or slower for each use (attack speed).
        /// </summary>
        public float ImpactDelay;

        /// <summary>
        /// Hits one use makes (a flurry of three stabs, a volley); 0 or 1: one. Each calls the effect.
        /// </summary>
        public int ImpactCount;

        /// <summary>
        /// Seconds between the hits of one use (with <see cref="ImpactCount"/> above 1).
        /// </summary>
        public float ImpactInterval;

        /// <summary>
        /// The hits still to come land even when the caster is interrupted or dies (an arrow already loosed, a
        /// meteor already called). False (the default): an interrupt stops them, as it stops a swing before contact.
        /// </summary>
        public bool ImpactsSurviveInterrupt;

        /// <summary>
        /// Each hit checks the target again (still there, still valid, in range) and misses when it is not: the
        /// target rolled away from the swing. False: the effect is called with the target and decides.
        /// </summary>
        public bool RecheckTargetOnImpact;

        /// <summary>
        /// Whether its hits come after it goes off (a delay or more than one hit) instead of at once.
        /// </summary>
        public bool HasDelayedImpacts => ImpactDelay > 0f || ImpactCount > 1;

        public CastInfo(float castTime, float channelDuration = 0f, bool isInterruptible = true, bool canMoveWhileCasting = false)
        {
            CastTime = castTime;
            ChannelDuration = channelDuration;
            IsInterruptible = isInterruptible;
            CanMoveWhileCasting = canMoveWhileCasting;
            ChannelTickInterval = 0f;
            ImpactDelay = 0f;
            ImpactCount = 0;
            ImpactInterval = 0f;
            ImpactsSurviveInterrupt = false;
            RecheckTargetOnImpact = false;
        }

        /// <summary>Instant cast (no cast time)</summary>
        public static CastInfo Instant => new CastInfo(0f);
    }
}
