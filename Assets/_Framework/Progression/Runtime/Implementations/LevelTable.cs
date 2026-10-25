using System;
using System.Collections.Generic;

namespace RPGFramework.Progression
{
    /// <summary>
    /// Default implementation of ILevelTable.
    /// Supports multiple XP curve types (Linear, Exponential, Polynomial, Custom).
    /// </summary>
    public class LevelTable : ILevelTable
    {
        private readonly LevelCurveType _curveType;
        private readonly long _baseXP;
        private readonly float _multiplier;
        private readonly float _power;
        private readonly Dictionary<int, long> _customLevelXP;
        private readonly int _maxLevel;

        // Cache for calculated XP values
        private readonly Dictionary<int, LevelInfo> _levelCache = new Dictionary<int, LevelInfo>();

        /// <summary>
        /// Create linear level table: XP = baseXP * level
        /// </summary>
        public static LevelTable CreateLinear(int maxLevel, long baseXP)
        {
            return new LevelTable(maxLevel, LevelCurveType.Linear, baseXP, 0f, 0f, null);
        }

        /// <summary>
        /// Create exponential level table: XP = baseXP * (multiplier ^ level)
        /// </summary>
        public static LevelTable CreateExponential(int maxLevel, long baseXP, float multiplier)
        {
            return new LevelTable(maxLevel, LevelCurveType.Exponential, baseXP, multiplier, 0f, null);
        }

        /// <summary>
        /// Create polynomial level table: XP = baseXP * (level ^ power)
        /// </summary>
        public static LevelTable CreatePolynomial(int maxLevel, long baseXP, float power)
        {
            return new LevelTable(maxLevel, LevelCurveType.Polynomial, baseXP, 0f, power, null);
        }

        /// <summary>
        /// Create custom level table with predefined XP values
        /// </summary>
        public static LevelTable CreateCustom(Dictionary<int, long> customXP)
        {
            int maxLevel = 0;
            foreach (var level in customXP.Keys)
            {
                if (level > maxLevel)
                    maxLevel = level;
            }

            return new LevelTable(maxLevel, LevelCurveType.Custom, 0, 0f, 0f, customXP);
        }

        private LevelTable(int maxLevel, LevelCurveType curveType, long baseXP, float multiplier, float power,
            Dictionary<int, long> customLevelXP)
        {
            _maxLevel = maxLevel;
            _curveType = curveType;
            _baseXP = baseXP;
            _multiplier = multiplier;
            _power = power;
            _customLevelXP = customLevelXP;

            // Precalculate all levels for performance
            for (int i = 1; i <= maxLevel; i++)
            {
                _levelCache[i] = CalculateLevelInfoInternal(i);
            }
        }

        public int MaxLevel => _maxLevel;

        public LevelInfo GetLevelInfo(int level)
        {
            if (level < 1) level = 1;
            if (level > _maxLevel) level = _maxLevel;

            return _levelCache.TryGetValue(level, out var info)
                ? info
                : CalculateLevelInfoInternal(level);
        }

        public int CalculateLevelFromXP(long totalXP)
        {
            if (totalXP <= 0) return 1;

            // Binary search for level (since XP is monotonically increasing)
            int low = 1;
            int high = _maxLevel;

            while (low < high)
            {
                int mid = (low + high + 1) / 2;
                long requiredXP = GetTotalXPForLevel(mid);

                if (totalXP >= requiredXP)
                    low = mid;
                else
                    high = mid - 1;
            }

            return low;
        }

        public long GetXPForNextLevel(int currentLevel)
        {
            if (currentLevel >= _maxLevel)
                return 0;

            long currentTotalXP = GetTotalXPForLevel(currentLevel);
            long nextTotalXP = GetTotalXPForLevel(currentLevel + 1);

            return nextTotalXP - currentTotalXP;
        }

        public long GetTotalXPForLevel(int level)
        {
            if (level <= 1) return 0;
            if (level > _maxLevel) level = _maxLevel;

            return GetLevelInfo(level).TotalXPRequired;
        }

        private LevelInfo CalculateLevelInfoInternal(int level)
        {
            if (level <= 1)
                return new LevelInfo(1, 0, 0);

            long totalXP = 0;

            switch (_curveType)
            {
                case LevelCurveType.Linear:
                    // Sum: baseXP * (1 + 2 + 3 + ... + (level-1))
                    // = baseXP * ((level-1) * level / 2)
                    totalXP = _baseXP * (level - 1) * level / 2;
                    break;

                case LevelCurveType.Exponential:
                    // Sum of geometric series
                    for (int i = 1; i < level; i++)
                    {
                        totalXP += (long)(_baseXP * Math.Pow(_multiplier, i));
                    }
                    break;

                case LevelCurveType.Polynomial:
                    // Sum: baseXP * (1^power + 2^power + ... + (level-1)^power)
                    for (int i = 1; i < level; i++)
                    {
                        totalXP += (long)(_baseXP * Math.Pow(i, _power));
                    }
                    break;

                case LevelCurveType.Custom:
                    if (_customLevelXP != null && _customLevelXP.TryGetValue(level, out var customXP))
                        totalXP = customXP;
                    break;
            }

            long previousLevelXP = level > 1 ? GetTotalXPForLevel(level - 1) : 0;
            long xpFromPrevious = totalXP - previousLevelXP;

            return new LevelInfo(level, totalXP, xpFromPrevious);
        }
    }
}
