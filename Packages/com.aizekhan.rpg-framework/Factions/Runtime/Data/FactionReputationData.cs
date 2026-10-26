using System;
using System.Collections.Generic;
using RPGFramework.ECS;

namespace RPGFramework.Factions
{
    /// <summary>
    /// ECS Component storing all faction reputations for an entity.
    /// Attached to entities that can have reputation (players, NPCs).
    /// </summary>
    [Serializable]
    public struct FactionReputationData : IComponent
    {
        /// <summary>
        /// Dictionary of faction ID → reputation value.
        /// Reputation ranges from -100 (hated) to +100 (revered).
        /// </summary>
        public Dictionary<string, int> Reputations;

        /// <summary>
        /// Create new faction reputation data with empty reputation dictionary.
        /// </summary>
        public static FactionReputationData Create()
        {
            return new FactionReputationData
            {
                Reputations = new Dictionary<string, int>()
            };
        }

        /// <summary>
        /// Get reputation with a specific faction.
        /// Returns 0 if no reputation established yet.
        /// </summary>
        public int GetReputation(string factionId)
        {
            if (Reputations == null)
                Reputations = new Dictionary<string, int>();

            return Reputations.TryGetValue(factionId, out int rep) ? rep : 0;
        }

        /// <summary>
        /// Set reputation with a specific faction.
        /// Automatically clamps to [-100, +100] range.
        /// </summary>
        public void SetReputation(string factionId, int reputation)
        {
            if (Reputations == null)
                Reputations = new Dictionary<string, int>();

            Reputations[factionId] = Math.Clamp(reputation, -100, 100);
        }

        /// <summary>
        /// Modify reputation by a delta amount.
        /// Automatically clamps to [-100, +100] range.
        /// </summary>
        public void ModifyReputation(string factionId, int delta)
        {
            int current = GetReputation(factionId);
            SetReputation(factionId, current + delta);
        }

        /// <summary>
        /// Check if entity has reputation established with a faction.
        /// </summary>
        public bool HasReputation(string factionId)
        {
            return Reputations != null && Reputations.ContainsKey(factionId);
        }
    }
}
