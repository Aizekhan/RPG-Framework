using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RPGFramework.Items
{
    /// <summary>
    /// An affix an item got, with the value it rolled.
    /// </summary>
    public readonly struct RolledAffix : IEquatable<RolledAffix>
    {
        public RolledAffix(string affixId, float value)
        {
            AffixId = affixId ?? "";
            Value = value;
        }

        public string AffixId { get; }
        public float Value { get; }

        public bool Equals(RolledAffix other) => AffixId == other.AffixId && Value.Equals(other.Value);
        public override bool Equals(object obj) => obj is RolledAffix other && Equals(other);
        public override int GetHashCode() => ((AffixId?.GetHashCode() ?? 0) * 397) ^ Value.GetHashCode();
        public override string ToString() => AffixId + "=" + Value.ToString("R", CultureInfo.InvariantCulture);

        /// <summary>
        /// The affixes as one line to save ("str_1=12;crit_2=0.05"), the same in any culture.
        /// </summary>
        public static string Encode(IEnumerable<RolledAffix> affixes)
        {
            var text = new StringBuilder();
            if (affixes == null)
                return "";
            foreach (var affix in affixes)
            {
                if (text.Length > 0)
                    text.Append(';');
                text.Append(affix.ToString());
            }
            return text.ToString();
        }

        /// <summary>
        /// The affixes back from <see cref="Encode"/>; parts that do not read are skipped.
        /// </summary>
        public static List<RolledAffix> Decode(string text)
        {
            var affixes = new List<RolledAffix>();
            if (string.IsNullOrEmpty(text))
                return affixes;
            foreach (var part in text.Split(';'))
            {
                int at = part.IndexOf('=');
                if (at <= 0)
                    continue;
                if (float.TryParse(part.Substring(at + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
                    affixes.Add(new RolledAffix(part.Substring(0, at), value));
            }
            return affixes;
        }
    }
}
