using System.Collections.Generic;
using System.Linq;

namespace RPGFramework.Progression
{
    /// <summary>
    /// Default implementation of IStatGrowthFormula.
    /// Manages stat growth calculations for multiple stats.
    /// </summary>
    public class StatGrowthFormula : IStatGrowthFormula
    {
        private readonly Dictionary<string, StatGrowthData> _statGrowth = new Dictionary<string, StatGrowthData>();

        public void RegisterStatGrowth(StatGrowthData growthData)
        {
            _statGrowth[growthData.StatId] = growthData;
        }

        public bool HasStatGrowth(string statId)
        {
            return _statGrowth.ContainsKey(statId);
        }

        public float CalculateStatAtLevel(string statId, int level)
        {
            if (!_statGrowth.TryGetValue(statId, out var growth))
                return 0f;

            return growth.CalculateAtLevel(level);
        }

        public Dictionary<string, float> GetStatChangesForLevelUp(int fromLevel, int toLevel)
        {
            var changes = new Dictionary<string, float>();

            foreach (var kvp in _statGrowth)
            {
                string statId = kvp.Key;
                StatGrowthData growth = kvp.Value;

                float oldValue = growth.CalculateAtLevel(fromLevel);
                float newValue = growth.CalculateAtLevel(toLevel);

                changes[statId] = newValue;
            }

            return changes;
        }
    }
}
