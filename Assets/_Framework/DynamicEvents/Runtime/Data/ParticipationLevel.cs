namespace RPGFramework.DynamicEvents.Runtime
{
    /// <summary>
    /// Participation level based on contribution to an event.
    /// Determines reward tier.
    /// </summary>
    public enum ParticipationLevel
    {
        /// <summary>
        /// Minimal participation, no rewards.
        /// </summary>
        Observer,

        /// <summary>
        /// Small contribution, basic rewards.
        /// </summary>
        Helper,

        /// <summary>
        /// Moderate contribution, standard rewards.
        /// </summary>
        Contributor,

        /// <summary>
        /// Significant contribution, enhanced rewards.
        /// </summary>
        Hero,

        /// <summary>
        /// Exceptional contribution, maximum rewards.
        /// </summary>
        Legend
    }
}
