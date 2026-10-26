using System;
using System.Collections.Generic;

namespace RPGFramework.Core
{
    /// <summary>
    /// Argument guards that throw the standard .NET exceptions and return the checked value,
    /// so they can be used inline: <c>_bus = Ensure.NotNull(bus, nameof(bus));</c>
    /// </summary>
    public static class Ensure
    {
        public static T NotNull<T>(T value, string paramName) where T : class =>
            value ?? throw new ArgumentNullException(paramName);

        public static string NotNullOrEmpty(string value, string paramName) =>
            string.IsNullOrEmpty(value) ? throw new ArgumentException($"{paramName} cannot be null or empty", paramName) : value;

        public static string NotNullOrWhiteSpace(string value, string paramName) =>
            string.IsNullOrWhiteSpace(value) ? throw new ArgumentException($"{paramName} cannot be null or whitespace", paramName) : value;

        public static T NotEmpty<T>(T collection, string paramName) where T : class, System.Collections.ICollection
        {
            NotNull(collection, paramName);
            return collection.Count == 0 ? throw new ArgumentException($"{paramName} cannot be empty", paramName) : collection;
        }

        public static void IsTrue(bool condition, string message, string paramName = null)
        {
            if (!condition)
                throw new ArgumentException(message, paramName);
        }

        public static T InRange<T>(T value, T min, T max, string paramName) where T : IComparable<T>
        {
            if (value.CompareTo(min) < 0 || value.CompareTo(max) > 0)
                throw new ArgumentOutOfRangeException(paramName, value, $"{paramName} must be between {min} and {max}");
            return value;
        }

        public static int Positive(int value, string paramName) =>
            value > 0 ? value : throw new ArgumentOutOfRangeException(paramName, value, $"{paramName} must be positive");

        public static float Positive(float value, string paramName) =>
            value > 0f ? value : throw new ArgumentOutOfRangeException(paramName, value, $"{paramName} must be positive");

        public static int NotNegative(int value, string paramName) =>
            value >= 0 ? value : throw new ArgumentOutOfRangeException(paramName, value, $"{paramName} cannot be negative");

        public static float NotNegative(float value, string paramName) =>
            value >= 0f ? value : throw new ArgumentOutOfRangeException(paramName, value, $"{paramName} cannot be negative");

        public static TEnum Defined<TEnum>(TEnum value, string paramName) where TEnum : struct, Enum =>
            Enum.IsDefined(typeof(TEnum), value) ? value : throw new ArgumentOutOfRangeException(paramName, value, $"{value} is not a defined {typeof(TEnum).Name}");

        public static TKey ContainsKey<TKey, TValue>(IReadOnlyDictionary<TKey, TValue> dictionary, TKey key, string paramName) =>
            dictionary.ContainsKey(key) ? key : throw new KeyNotFoundException($"{paramName} '{key}' was not found");
    }
}
