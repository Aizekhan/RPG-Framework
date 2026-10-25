using System;
using System.Collections.Generic;

namespace RPGFramework.Guilds.Runtime
{
    /// <summary>
    /// Represents a guild-specific quest that requires collective effort.
    /// Guild quests typically have shared progress and rewards for all contributors.
    /// </summary>
    public interface IGuildQuest
    {
        /// <summary>
        /// Unique quest identifier.
        /// </summary>
        string QuestId { get; }

        /// <summary>
        /// Guild this quest belongs to.
        /// </summary>
        string GuildId { get; }

        /// <summary>
        /// Quest name.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Quest description.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Current progress (0 to MaxProgress).
        /// </summary>
        int Progress { get; }

        /// <summary>
        /// Progress required to complete the quest.
        /// </summary>
        int MaxProgress { get; }

        /// <summary>
        /// Whether the quest is completed.
        /// </summary>
        bool IsCompleted { get; }

        /// <summary>
        /// Quest start time.
        /// </summary>
        DateTime StartedAt { get; }

        /// <summary>
        /// Quest deadline (optional, null if no deadline).
        /// </summary>
        DateTime? ExpiresAt { get; }

        /// <summary>
        /// Minimum guild rank required to contribute to this quest.
        /// </summary>
        GuildRankType MinimumRank { get; }

        /// <summary>
        /// Check if the quest has expired.
        /// </summary>
        /// <returns>True if quest has passed its deadline.</returns>
        bool IsExpired();

        /// <summary>
        /// Add progress to the quest.
        /// </summary>
        /// <param name="amount">Amount of progress to add.</param>
        /// <param name="contributorId">Entity contributing to the quest.</param>
        void AddProgress(int amount, int contributorId);

        /// <summary>
        /// Get all contributors and their individual contributions.
        /// </summary>
        /// <returns>Dictionary of entity ID → contribution amount.</returns>
        IReadOnlyDictionary<int, int> GetContributions();

        /// <summary>
        /// Get the contribution amount for a specific entity.
        /// </summary>
        /// <param name="entityId">Entity to check.</param>
        /// <returns>Contribution amount (0 if no contribution).</returns>
        int GetContribution(int entityId);

        /// <summary>
        /// Complete the quest and distribute rewards.
        /// </summary>
        void Complete();
    }
}
