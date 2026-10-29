using System;
using System.Collections.Generic;

namespace RPGFramework.Items
{
    /// <summary>
    /// The affixes an item instance rolled, kept in its <see cref="IItemInstance.CustomProperties"/> as one string
    /// (<see cref="Key"/>), so they save and travel with the instance like its other properties.
    /// </summary>
    public static class ItemAffixes
    {
        public const string Key = "affixes";

        public static List<RolledAffix> Get(IItemInstance instance)
        {
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));
            return instance.CustomProperties != null && instance.CustomProperties.TryGetValue(Key, out var value)
                ? RolledAffix.Decode(value as string)
                : new List<RolledAffix>();
        }

        /// <summary>
        /// Keeps the affixes on the instance; none removes the property.
        /// </summary>
        public static void Set(IItemInstance instance, IEnumerable<RolledAffix> affixes)
        {
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));
            string text = RolledAffix.Encode(affixes);
            if (text.Length == 0)
                instance.CustomProperties.Remove(Key);
            else
                instance.CustomProperties[Key] = text;
        }

        public static bool Has(IItemInstance instance) =>
            instance?.CustomProperties != null && instance.CustomProperties.ContainsKey(Key);
    }
}
