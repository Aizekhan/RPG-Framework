namespace RPGFramework.Companions.Runtime
{
    /// <summary>
    /// Current state/status of a companion.
    /// </summary>
    public enum CompanionState
    {
        /// <summary>
        /// Not doing anything, waiting for commands.
        /// </summary>
        Idle = 0,

        /// <summary>
        /// Following the owner.
        /// </summary>
        Following = 1,

        /// <summary>
        /// Engaged in combat.
        /// </summary>
        Fighting = 2,

        /// <summary>
        /// Dead/defeated and needs revival.
        /// </summary>
        Dead = 3,

        /// <summary>
        /// Not summoned/active, stored away.
        /// </summary>
        Dismissed = 4,

        /// <summary>
        /// Resting/recovering at a stable or camp.
        /// </summary>
        Resting = 5,

        /// <summary>
        /// Patrolling an area.
        /// </summary>
        Patrolling = 6,

        /// <summary>
        /// Defending a specific position or target.
        /// </summary>
        Defending = 7,

        /// <summary>
        /// Fleeing from danger (low health behavior).
        /// </summary>
        Fleeing = 8
    }
}
