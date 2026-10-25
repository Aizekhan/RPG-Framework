namespace RPGFramework.Trading
{
    /// <summary>
    /// Current state of a trade offer.
    /// </summary>
    public enum TradeState
    {
        /// <summary>
        /// Trade has been proposed but not yet responded to.
        /// Player has not accepted or declined.
        /// </summary>
        Proposed = 0,

        /// <summary>
        /// Trade has been accepted and is ready to complete.
        /// Items have been transferred, gold exchanged.
        /// </summary>
        Accepted = 1,

        /// <summary>
        /// Player declined the trade offer.
        /// Trade is no longer valid and cannot be completed.
        /// </summary>
        Declined = 2,

        /// <summary>
        /// Trade has been completed successfully.
        /// Final state - no further actions possible.
        /// </summary>
        Completed = 3
    }
}
