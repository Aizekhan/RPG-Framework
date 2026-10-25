using System.Collections.Generic;

namespace RPGFramework.Progression
{
    /// <summary>
    /// Manages unlock conditions and tracks unlocked content.
    /// Handles level-based unlocks for abilities, features, etc.
    /// </summary>
    public interface IUnlockManager
    {
        /// <summary>Register unlock</summary>
        /// <param name="unlock">Unlock configuration</param>
        void RegisterUnlock(UnlockData unlock);

        /// <summary>Check if unlock is available at level</summary>
        /// <param name="unlockId">Unlock identifier</param>
        /// <param name="level">Level to check</param>
        /// <returns>True if unlock is available</returns>
        bool IsUnlockAvailable(string unlockId, int level);

        /// <summary>Get all unlocks that become available at specific level</summary>
        /// <param name="level">Level to check</param>
        /// <returns>List of unlocks</returns>
        List<UnlockData> GetUnlocksForLevel(int level);

        /// <summary>Get all unlocks available up to and including level</summary>
        /// <param name="level">Maximum level</param>
        /// <returns>List of all available unlocks</returns>
        List<UnlockData> GetAllUnlocksUpToLevel(int level);

        /// <summary>Check if entity has unlocked specific content</summary>
        /// <param name="entityId">Entity to check</param>
        /// <param name="unlockId">Unlock identifier</param>
        /// <returns>True if unlocked</returns>
        bool HasUnlocked(int entityId, string unlockId);

        /// <summary>Mark unlock as achieved for entity</summary>
        /// <param name="entityId">Entity achieving unlock</param>
        /// <param name="unlockId">Unlock identifier</param>
        void AchieveUnlock(int entityId, string unlockId);

        /// <summary>Get all unlocked content for entity</summary>
        /// <param name="entityId">Entity to query</param>
        /// <returns>List of unlocked IDs</returns>
        List<string> GetUnlockedContent(int entityId);
    }
}
