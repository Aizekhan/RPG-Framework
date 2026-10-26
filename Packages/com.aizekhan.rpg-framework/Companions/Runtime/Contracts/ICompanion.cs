namespace RPGFramework.Companions.Runtime
{
    /// <summary>
    /// Represents a companion entity - a pet, mount, summon, or familiar that can accompany the player.
    /// Examples: Combat pets, mounts for travel, non-combat pets for bonuses, temporary summons, magical familiars.
    /// </summary>
    public interface ICompanion
    {
        /// <summary>
        /// Unique identifier for this companion instance.
        /// </summary>
        string CompanionId { get; }

        /// <summary>
        /// Entity ID of the companion in the ECS system.
        /// </summary>
        int EntityId { get; }

        /// <summary>
        /// Display name of the companion.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Type/category of this companion (e.g., CombatPet, Mount, NonCombatPet).
        /// </summary>
        CompanionType CompanionType { get; }

        /// <summary>
        /// Current state of the companion (e.g., Idle, Following, Fighting).
        /// </summary>
        CompanionState CurrentState { get; }

        /// <summary>
        /// Entity ID of the companion's owner/master.
        /// </summary>
        int OwnerId { get; }

        /// <summary>
        /// Current loyalty/affection level toward the owner.
        /// </summary>
        LoyaltyLevel Loyalty { get; }

        /// <summary>
        /// Current level of the companion.
        /// </summary>
        int Level { get; }

        /// <summary>
        /// Whether the companion is currently active/summoned.
        /// </summary>
        bool IsActive { get; }

        /// <summary>
        /// Whether the companion is alive (not dead/defeated).
        /// </summary>
        bool IsAlive { get; }

        /// <summary>
        /// Summons/activates the companion.
        /// </summary>
        void Summon();

        /// <summary>
        /// Dismisses/deactivates the companion.
        /// </summary>
        void Dismiss();

        /// <summary>
        /// Issues a command to the companion.
        /// </summary>
        void IssueCommand(CompanionCommand command);

        /// <summary>
        /// Modifies the companion's loyalty level.
        /// </summary>
        void ModifyLoyalty(int amount);
    }
}
