using System;
using System.Collections.Generic;

namespace RPGFramework.BehaviorTree
{
    /// <summary>
    /// Blackboard - shared data storage for behavior tree nodes.
    /// Allows nodes to share state and communicate through named values.
    /// </summary>
    public class Blackboard
    {
        private readonly Dictionary<string, object> _data;

        public Blackboard()
        {
            _data = new Dictionary<string, object>();
        }

        /// <summary>
        /// Set a value in the blackboard.
        /// </summary>
        /// <typeparam name="T">Value type</typeparam>
        /// <param name="key">Value key</param>
        /// <param name="value">Value to store</param>
        public void SetValue<T>(string key, T value)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentNullException(nameof(key));
            }

            _data[key] = value;
        }

        /// <summary>
        /// Get a value from the blackboard.
        /// </summary>
        /// <typeparam name="T">Value type</typeparam>
        /// <param name="key">Value key</param>
        /// <returns>Stored value</returns>
        /// <exception cref="KeyNotFoundException">If key doesn't exist</exception>
        /// <exception cref="InvalidCastException">If value type doesn't match</exception>
        public T GetValue<T>(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (!_data.ContainsKey(key))
            {
                throw new KeyNotFoundException($"Blackboard key '{key}' not found");
            }

            return (T)_data[key];
        }

        /// <summary>
        /// Try to get a value from the blackboard.
        /// </summary>
        /// <typeparam name="T">Value type</typeparam>
        /// <param name="key">Value key</param>
        /// <param name="value">Output value</param>
        /// <returns>True if key exists and type matches</returns>
        public bool TryGetValue<T>(string key, out T value)
        {
            if (string.IsNullOrEmpty(key) || !_data.ContainsKey(key))
            {
                value = default;
                return false;
            }

            try
            {
                value = (T)_data[key];
                return true;
            }
            catch (InvalidCastException)
            {
                value = default;
                return false;
            }
        }

        /// <summary>
        /// Check if a key exists in the blackboard.
        /// </summary>
        /// <param name="key">Key to check</param>
        /// <returns>True if key exists</returns>
        public bool HasKey(string key)
        {
            return !string.IsNullOrEmpty(key) && _data.ContainsKey(key);
        }

        /// <summary>
        /// Remove a value from the blackboard.
        /// </summary>
        /// <param name="key">Key to remove</param>
        /// <returns>True if key was removed</returns>
        public bool Remove(string key)
        {
            return !string.IsNullOrEmpty(key) && _data.Remove(key);
        }

        /// <summary>
        /// Clear all values from the blackboard.
        /// </summary>
        public void Clear()
        {
            _data.Clear();
        }
    }
}
