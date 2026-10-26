using System;
using System.Collections.Generic;

namespace RPGFramework.UINavigation
{
    /// <summary>
    /// Named values passed to a view when it is opened or navigated back to.
    /// </summary>
    public sealed class NavigationParameters
    {
        private readonly Dictionary<string, object> _values = new Dictionary<string, object>(StringComparer.Ordinal);

        public int Count => _values.Count;

        public IEnumerable<string> Keys => _values.Keys;

        public static NavigationParameters With(string key, object value) => new NavigationParameters().Set(key, value);

        public NavigationParameters Set(string key, object value)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Parameter key is required", nameof(key));
            _values[key] = value;
            return this;
        }

        public bool Contains(string key) => key != null && _values.ContainsKey(key);

        public bool TryGet<T>(string key, out T value)
        {
            if (key != null && _values.TryGetValue(key, out var stored) && stored is T typed)
            {
                value = typed;
                return true;
            }
            value = default;
            return false;
        }

        /// <summary>
        /// Returns the value, or <paramref name="defaultValue"/> when it is missing or of another type.
        /// </summary>
        public T Get<T>(string key, T defaultValue = default) => TryGet<T>(key, out var value) ? value : defaultValue;

        public bool Remove(string key) => key != null && _values.Remove(key);

        public NavigationParameters Clone()
        {
            var clone = new NavigationParameters();
            foreach (var pair in _values)
                clone._values[pair.Key] = pair.Value;
            return clone;
        }
    }
}
