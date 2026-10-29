using System;
using System.Collections.Generic;
using System.Linq;

namespace RPGFramework.Localization
{
    /// <summary>
    /// Text to show a player, before it is translated: a key and named values ("hit" with damage 12). Rules made of
    /// plain C# give it out; views translate it with <see cref="ILocalization.Text(LocalizedText)"/>. A value that is
    /// itself a <see cref="LocalizedText"/> is translated too.
    /// </summary>
    public readonly struct LocalizedText : IEquatable<LocalizedText>
    {
        private static readonly KeyValuePair<string, object>[] NoArgs = Array.Empty<KeyValuePair<string, object>>();
        private readonly KeyValuePair<string, object>[] _args;

        public LocalizedText(string key, params (string Name, object Value)[] args)
        {
            Key = key ?? "";
            _args = args == null || args.Length == 0
                ? NoArgs
                : args.Select(a => new KeyValuePair<string, object>(a.Name ?? "", a.Value)).ToArray();
        }

        public string Key { get; }

        public IReadOnlyList<KeyValuePair<string, object>> Args => _args ?? NoArgs;

        public bool IsEmpty => string.IsNullOrEmpty(Key);

        public static LocalizedText Of(string key, params (string Name, object Value)[] args) => new LocalizedText(key, args);

        /// <returns>The named value, or null.</returns>
        public object Arg(string name)
        {
            foreach (var arg in Args)
            {
                if (arg.Key == name)
                    return arg.Value;
            }
            return null;
        }

        public static implicit operator LocalizedText(string key) => new LocalizedText(key);

        public bool Equals(LocalizedText other) =>
            Key == other.Key && Args.Count == other.Args.Count
            && Args.Zip(other.Args, (a, b) => a.Key == b.Key && Equals(a.Value, b.Value)).All(same => same);

        public override bool Equals(object obj) => obj is LocalizedText other && Equals(other);

        public override int GetHashCode() => Key.GetHashCode();

        public override string ToString() =>
            Args.Count == 0 ? Key : $"{Key}({string.Join(", ", Args.Select(a => a.Key + "=" + a.Value))})";
    }
}
