namespace RPGFramework.Companions.Runtime
{
    /// <summary>
    /// Manages companion AI behavior and decision-making.
    /// </summary>
    public interface ICompanionBehavior
    {
        /// <summary>
        /// Current command the companion is executing.
        /// </summary>
        CompanionCommand CurrentCommand { get; }

        /// <summary>
        /// Whether the companion is in combat.
        /// </summary>
        bool IsInCombat { get; }

        /// <summary>
        /// Distance the companion maintains from its owner when following.
        /// </summary>
        float FollowDistance { get; set; }

        /// <summary>
        /// Whether the companion will auto-attack hostile targets.
        /// </summary>
        bool AutoAttackEnabled { get; set; }

        /// <summary>
        /// Updates the companion's AI behavior for the current frame.
        /// </summary>
        void Update(float deltaTime);

        /// <summary>
        /// Executes a specific command.
        /// </summary>
        void ExecuteCommand(CompanionCommand command);

        /// <summary>
        /// Makes the companion attack a specific target.
        /// </summary>
        void AttackTarget(int targetEntityId);

        /// <summary>
        /// Makes the companion move to a specific position.
        /// </summary>
        void MoveTo(float x, float y, float z);

        /// <summary>
        /// Resets the companion to idle state.
        /// </summary>
        void ResetToIdle();
    }
}
