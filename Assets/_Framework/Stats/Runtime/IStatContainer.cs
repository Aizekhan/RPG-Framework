// Assets/_Framework/Stats/Runtime/IStatContainer.cs

using System;
using System.Collections.Generic;

namespace RPGFramework.Stats
{
    /// <summary>
    /// Generic stat storage container with modifier support
    /// </summary>
    /// <typeparam name="TStatType">Stat identifier type (enum or struct)</typeparam>
    /// <typeparam name="TValue">Stat value type (float, int, etc.)</typeparam>
    public interface IStatContainer<TStatType, TValue>
        where TStatType : struct, IConvertible
        where TValue : struct, IComparable<TValue>
    {
        /// <summary>
        /// Get base stat value (before modifiers)
        /// </summary>
        /// <param name="statType">Stat type to query</param>
        /// <returns>Base stat value, or default if stat doesn't exist</returns>
        TValue GetBaseStat(TStatType statType);

        /// <summary>
        /// Set base stat value
        /// </summary>
        /// <param name="statType">Stat type to set</param>
        /// <param name="value">Base value to set</param>
        void SetBaseStat(TStatType statType, TValue value);

        /// <summary>
        /// Get final stat value (after all modifiers applied)
        /// Calculation: (base + additives) * multiplicatives, or override
        /// </summary>
        /// <param name="statType">Stat type to query</param>
        /// <returns>Final calculated stat value</returns>
        TValue GetFinalStat(TStatType statType);

        /// <summary>
        /// Check if stat exists in container
        /// </summary>
        /// <param name="statType">Stat type to check</param>
        /// <returns>True if stat has been set (even if value is zero)</returns>
        bool HasStat(TStatType statType);

        /// <summary>
        /// Get all stat types currently in container
        /// </summary>
        /// <returns>Enumerable of all stat types with values</returns>
        IEnumerable<TStatType> GetAllStatTypes();

        /// <summary>
        /// Add a modifier to a stat
        /// </summary>
        /// <param name="modifier">Modifier to add</param>
        void AddModifier(IStatModifier<TStatType, TValue> modifier);

        /// <summary>
        /// Remove a modifier by its unique ID
        /// </summary>
        /// <param name="modifierId">Unique modifier ID</param>
        /// <returns>True if modifier was found and removed</returns>
        bool RemoveModifier(string modifierId);

        /// <summary>
        /// Remove all modifiers affecting a specific stat
        /// </summary>
        /// <param name="statType">Stat type to clear modifiers for</param>
        void RemoveAllModifiers(TStatType statType);

        /// <summary>
        /// Clear all modifiers from all stats
        /// </summary>
        void ClearAllModifiers();
    }
}
