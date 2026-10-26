// Packages/com.aizekhan.rpg-framework/Stats/Runtime/StatContainer.cs

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RPGFramework.ECS;
using RPGFramework.Serialization;

namespace RPGFramework.Stats
{
    /// <summary>
    /// Generic stat container component with modifier support
    /// Supports any stat type (enum, struct) and value type (float, int, etc.)
    /// Final values are cached until a base value or modifier changes; recalculating allocates nothing (modifiers are
    /// kept in priority order as they are added). Whole-number stats stop at their type's limits instead of overflowing.
    /// </summary>
    /// <typeparam name="TStatType">Stat identifier type (must be value type with IConvertible)</typeparam>
    /// <typeparam name="TValue">Stat value type (must be value type with IComparable)</typeparam>
    [SerializableType("RPGFramework.Stats.StatContainer")] // Stable type ID for serialization
    public struct StatContainer<TStatType, TValue> : IComponent, ISerializable, IStatContainer<TStatType, TValue>
        where TStatType : struct, IConvertible
        where TValue : struct, IComparable<TValue>
    {
        // Internal storage
        private Dictionary<TStatType, TValue> _baseStats;
        private Dictionary<TStatType, List<IStatModifier<TStatType, TValue>>> _modifiers;

        // Cached final values (invalidated when modifiers change)
        private Dictionary<TStatType, TValue> _cachedFinalStats;
        private HashSet<TStatType> _dirtyStats; // Stats that need recalculation

        /// <summary>
        /// Initialize stat container
        /// </summary>
        public void Initialize()
        {
            _baseStats = new Dictionary<TStatType, TValue>();
            _modifiers = new Dictionary<TStatType, List<IStatModifier<TStatType, TValue>>>();
            _cachedFinalStats = new Dictionary<TStatType, TValue>();
            _dirtyStats = new HashSet<TStatType>();
        }

        /// <summary>
        /// Get base stat value (before modifiers)
        /// </summary>
        public TValue GetBaseStat(TStatType statType)
        {
            EnsureInitialized();
            return _baseStats.TryGetValue(statType, out var value) ? value : default(TValue);
        }

        /// <summary>
        /// Set base stat value
        /// </summary>
        public void SetBaseStat(TStatType statType, TValue value)
        {
            EnsureInitialized();
            _baseStats[statType] = value;
            MarkDirty(statType);
        }

        /// <summary>
        /// Get final stat value (after all modifiers applied)
        /// </summary>
        public TValue GetFinalStat(TStatType statType)
        {
            EnsureInitialized();

            // Return cached value if not dirty
            if (!_dirtyStats.Contains(statType) && _cachedFinalStats.TryGetValue(statType, out var cached))
            {
                return cached;
            }

            // Calculate final value
            TValue finalValue = CalculateFinalStat(statType);
            _cachedFinalStats[statType] = finalValue;
            _dirtyStats.Remove(statType);

            return finalValue;
        }

        /// <summary>
        /// Check if stat exists in container
        /// </summary>
        public bool HasStat(TStatType statType)
        {
            EnsureInitialized();
            return _baseStats.ContainsKey(statType);
        }

        /// <summary>
        /// Get all stat types currently in container
        /// </summary>
        public IEnumerable<TStatType> GetAllStatTypes()
        {
            EnsureInitialized();
            return _baseStats.Keys;
        }

        /// <summary>
        /// Add a modifier to a stat
        /// </summary>
        public void AddModifier(IStatModifier<TStatType, TValue> modifier)
        {
            if (modifier == null)
                throw new ArgumentNullException(nameof(modifier));

            EnsureInitialized();

            if (!_modifiers.TryGetValue(modifier.StatType, out var list))
            {
                list = new List<IStatModifier<TStatType, TValue>>();
                _modifiers[modifier.StatType] = list;
            }

            // Kept in priority order (equal priorities in the order added), so recalculating never sorts
            int index = list.Count;
            while (index > 0 && list[index - 1].Priority > modifier.Priority)
                index--;
            list.Insert(index, modifier);
            MarkDirty(modifier.StatType);
        }

        /// <summary>
        /// Remove a modifier by its unique ID
        /// </summary>
        public bool RemoveModifier(string modifierId)
        {
            if (string.IsNullOrEmpty(modifierId))
                return false;

            EnsureInitialized();

            bool removed = false;
            foreach (var statType in _modifiers.Keys.ToList())
            {
                var modifierList = _modifiers[statType];
                int removedCount = modifierList.RemoveAll(m => m.ModifierId == modifierId);

                if (removedCount > 0)
                {
                    removed = true;
                    MarkDirty(statType);

                    // Clean up empty modifier lists
                    if (modifierList.Count == 0)
                    {
                        _modifiers.Remove(statType);
                    }
                }
            }

            return removed;
        }

        /// <summary>
        /// Remove all modifiers affecting a specific stat
        /// </summary>
        public void RemoveAllModifiers(TStatType statType)
        {
            EnsureInitialized();

            if (_modifiers.ContainsKey(statType))
            {
                _modifiers.Remove(statType);
                MarkDirty(statType);
            }
        }

        /// <summary>
        /// Clear all modifiers from all stats
        /// </summary>
        public void ClearAllModifiers()
        {
            EnsureInitialized();

            foreach (var statType in _modifiers.Keys)
            {
                MarkDirty(statType);
            }

            _modifiers.Clear();
        }

        // Private helper methods

        private void EnsureInitialized()
        {
            if (_baseStats == null)
            {
                Initialize();
            }
        }

        private void MarkDirty(TStatType statType)
        {
            _dirtyStats.Add(statType);
        }

        private TValue CalculateFinalStat(TStatType statType)
        {
            // Start with base value
            TValue baseValue = GetBaseStat(statType);

            // No modifiers? Return base value
            if (!_modifiers.TryGetValue(statType, out var modifierList) || modifierList.Count == 0)
            {
                return baseValue;
            }

            // One pass over the priority-ordered list: an Override wins outright (the highest priority, the first
            // added among equals); otherwise (base + additives) * (1 + each multiplier), multipliers in priority order
            IStatModifier<TStatType, TValue> winningOverride = null;
            double additive = 0d;
            for (int i = 0; i < modifierList.Count; i++)
            {
                var modifier = modifierList[i];
                if (modifier.ModifierType == StatModifierType.Override)
                {
                    if (winningOverride == null || modifier.Priority > winningOverride.Priority)
                        winningOverride = modifier;
                }
                else if (modifier.ModifierType == StatModifierType.Additive)
                {
                    additive += StatValues<TValue>.ToDouble(modifier.Value);
                }
            }
            if (winningOverride != null)
            {
                return winningOverride.Value;
            }

            double currentValue = StatValues<TValue>.ToDouble(baseValue) + additive;
            for (int i = 0; i < modifierList.Count; i++)
            {
                var modifier = modifierList[i];
                // Multiplicative formula: value * (1 + modifier)
                // Example: 100 * (1 + 0.1) = 110 (10% increase)
                if (modifier.ModifierType == StatModifierType.Multiplicative)
                    currentValue *= 1d + StatValues<TValue>.ToDouble(modifier.Value);
            }

            return StatValues<TValue>.FromDouble(currentValue);
        }

        // Serialization

        /// <summary>
        /// Serialize stat container to byte array
        /// Version 1: Stores base stats only (modifiers are runtime-only)
        /// </summary>
        public byte[] Serialize()
        {
            EnsureInitialized();

            using (var memoryStream = new MemoryStream())
            using (var writer = new BinaryWriter(memoryStream))
            {
                // Write version
                writer.Write((ushort)1);

                // Write base stats count
                writer.Write(_baseStats.Count);

                // Write base stats (stat type ID + value)
                foreach (var pair in _baseStats)
                {
                    // Write stat type as int32
                    writer.Write(pair.Key.ToInt32(null));

                    // Write value (assumes TValue is float or int for now)
                    if (typeof(TValue) == typeof(float))
                    {
                        writer.Write((float)(object)pair.Value);
                    }
                    else if (typeof(TValue) == typeof(int))
                    {
                        writer.Write((int)(object)pair.Value);
                    }
                    else if (typeof(TValue) == typeof(double))
                    {
                        writer.Write((double)(object)pair.Value);
                    }
                    else
                    {
                        throw new NotSupportedException($"Value type {typeof(TValue)} not supported for serialization");
                    }
                }

                return memoryStream.ToArray();
            }
        }

        /// <summary>
        /// Deserialize stat container from byte array
        /// </summary>
        public void Deserialize(byte[] data)
        {
            if (data == null || data.Length == 0)
                throw new ArgumentException("Cannot deserialize null or empty data", nameof(data));

            Initialize();

            using (var memoryStream = new MemoryStream(data))
            using (var reader = new BinaryReader(memoryStream))
            {
                // Read version
                ushort version = reader.ReadUInt16();

                if (version == 1)
                {
                    DeserializeV1(reader);
                }
                else
                {
                    throw new NotSupportedException($"Serialization version {version} not supported");
                }
            }
        }

        private void DeserializeV1(BinaryReader reader)
        {
            // Read base stats count
            int count = reader.ReadInt32();

            // Read base stats
            for (int i = 0; i < count; i++)
            {
                // Read stat type ID
                int statTypeId = reader.ReadInt32();
                TStatType statType = (TStatType)(object)statTypeId;

                // Read value
                TValue value;
                if (typeof(TValue) == typeof(float))
                {
                    value = (TValue)(object)reader.ReadSingle();
                }
                else if (typeof(TValue) == typeof(int))
                {
                    value = (TValue)(object)reader.ReadInt32();
                }
                else if (typeof(TValue) == typeof(double))
                {
                    value = (TValue)(object)reader.ReadDouble();
                }
                else
                {
                    throw new NotSupportedException($"Value type {typeof(TValue)} not supported for deserialization");
                }

                _baseStats[statType] = value;
            }

            // Mark all stats as dirty (need recalculation)
            foreach (var statType in _baseStats.Keys)
            {
                MarkDirty(statType);
            }
        }
    }

    /// <summary>
    /// Converts stat values to and from double without boxing (one delegate per value type, made once).
    /// Whole numbers are rounded half away from zero and kept within the type's range.
    /// </summary>
    internal static class StatValues<TValue> where TValue : struct
    {
        public static readonly Func<TValue, double> ToDouble;
        public static readonly Func<double, TValue> FromDouble;

        static StatValues()
        {
            object to, from;
            var type = typeof(TValue);
            if (type == typeof(float))
            {
                to = (Func<float, double>)(v => v);
                from = (Func<double, float>)(d => (float)d);
            }
            else if (type == typeof(double))
            {
                to = (Func<double, double>)(v => v);
                from = (Func<double, double>)(d => d);
            }
            else if (type == typeof(int))
            {
                to = (Func<int, double>)(v => v);
                from = (Func<double, int>)(d => (int)Whole(d, int.MinValue, int.MaxValue));
            }
            else if (type == typeof(long))
            {
                to = (Func<long, double>)(v => v);
                from = (Func<double, long>)(d => d >= 9.2233720368547758E+18 ? long.MaxValue : d <= -9.2233720368547758E+18 ? long.MinValue : (long)Whole(d, long.MinValue, long.MaxValue));
            }
            else if (type == typeof(short))
            {
                to = (Func<short, double>)(v => v);
                from = (Func<double, short>)(d => (short)Whole(d, short.MinValue, short.MaxValue));
            }
            else if (type == typeof(byte))
            {
                to = (Func<byte, double>)(v => v);
                from = (Func<double, byte>)(d => (byte)Whole(d, byte.MinValue, byte.MaxValue));
            }
            else
            {
                // Anything else convertible: correct, though it boxes
                to = (Func<TValue, double>)(v => System.Convert.ToDouble(v, System.Globalization.CultureInfo.InvariantCulture));
                from = (Func<double, TValue>)(d => (TValue)System.Convert.ChangeType(d, typeof(TValue), System.Globalization.CultureInfo.InvariantCulture));
            }
            ToDouble = (Func<TValue, double>)to;
            FromDouble = (Func<double, TValue>)from;
        }

        private static double Whole(double value, double min, double max)
        {
            if (double.IsNaN(value))
                return 0d;
            value = Math.Round(value, MidpointRounding.AwayFromZero);
            return value < min ? min : value > max ? max : value;
        }
    }
}
