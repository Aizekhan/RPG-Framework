using System.Collections.Generic;

namespace RPGFramework.Companions.Runtime
{
    /// <summary>
    /// Manages all companions in the game - creation, lifecycle, and retrieval.
    /// </summary>
    public interface ICompanionManager
    {
        /// <summary>
        /// Gets all active companions.
        /// </summary>
        IReadOnlyList<ICompanion> ActiveCompanions { get; }

        /// <summary>
        /// Creates a new companion instance.
        /// </summary>
        ICompanion CreateCompanion(string companionId, int ownerId, CompanionType type, string name);

        /// <summary>
        /// Gets a companion by its unique ID.
        /// </summary>
        ICompanion GetCompanion(string companionId);

        /// <summary>
        /// Gets all companions owned by a specific entity.
        /// </summary>
        IReadOnlyList<ICompanion> GetCompanionsByOwner(int ownerId);

        /// <summary>
        /// Gets all companions of a specific type.
        /// </summary>
        IReadOnlyList<ICompanion> GetCompanionsByType(CompanionType type);

        /// <summary>
        /// Removes a companion from the game (permanent).
        /// </summary>
        bool RemoveCompanion(string companionId);

        /// <summary>
        /// Updates all active companions' AI and stats.
        /// </summary>
        void UpdateCompanions(float deltaTime);

        /// <summary>
        /// Feeds a companion with food item.
        /// </summary>
        bool FeedCompanion(string companionId, string foodItemId);

        /// <summary>
        /// Transfers companion ownership to a new owner.
        /// </summary>
        bool TransferOwnership(string companionId, int newOwnerId);
    }
}
