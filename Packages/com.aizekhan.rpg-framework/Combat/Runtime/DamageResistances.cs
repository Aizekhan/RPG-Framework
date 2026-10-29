using System;
using System.Collections.Generic;

namespace RPGFramework.Combat
{
    /// <summary>
    /// A character's resistance to one element, as content gives it: 0.3 takes 30% less fire damage, -0.5 (a weakness)
    /// takes 50% more; immune takes none.
    /// </summary>
    [Serializable]
    public sealed class ElementResistance
    {
        public string Element = "";
        public float Value;
        public bool Immune;

        public ElementResistance()
        {
        }

        public ElementResistance(string element, float value, bool immune = false)
        {
            Element = element ?? "";
            Value = value;
            Immune = immune;
        }
    }

    /// <summary>
    /// Resistances to damage elements ("fire", "ice", "poison") per entity, from several sources that come and go on
    /// their own (the character itself, each piece of gear, a buff): they add up, then are held between
    /// <see cref="MaxWeakness"/> and <see cref="MaxResistance"/>. An immunity from any source takes all of that element.
    /// Engine-free; the game decides which hits carry which elements.
    /// </summary>
    public sealed class DamageResistances
    {
        /// <summary>
        /// The source of what an entity is by itself (its content).
        /// </summary>
        public const string BaseSource = "";

        private readonly Dictionary<int, Dictionary<string, Dictionary<string, float>>> _values =
            new Dictionary<int, Dictionary<string, Dictionary<string, float>>>();
        private readonly Dictionary<int, Dictionary<string, HashSet<string>>> _immune =
            new Dictionary<int, Dictionary<string, HashSet<string>>>();

        /// <summary>
        /// The most an entity can resist (0.75: at least a quarter gets through).
        /// </summary>
        public float MaxResistance { get; set; } = 0.75f;

        /// <summary>
        /// The deepest weakness, as a negative resistance (-1: at most double damage).
        /// </summary>
        public float MaxWeakness { get; set; } = -1f;

        /// <summary>
        /// Sets what one source gives an entity against an element; 0 takes it away.
        /// </summary>
        public void Set(int entityId, string element, float value, string source = BaseSource)
        {
            if (string.IsNullOrEmpty(element))
                throw new ArgumentException("An element is needed.", nameof(element));
            if (float.IsNaN(value) || float.IsInfinity(value))
                throw new ArgumentOutOfRangeException(nameof(value), value, "A resistance is a finite number.");
            source = source ?? BaseSource;
            if (value == 0f)
            {
                if (_values.TryGetValue(entityId, out var byElement) && byElement.TryGetValue(element, out var bySource))
                    bySource.Remove(source);
                return;
            }
            if (!_values.TryGetValue(entityId, out var elements))
                _values[entityId] = elements = new Dictionary<string, Dictionary<string, float>>(StringComparer.Ordinal);
            if (!elements.TryGetValue(element, out var sources))
                elements[element] = sources = new Dictionary<string, float>(StringComparer.Ordinal);
            sources[source] = value;
        }

        public void SetImmune(int entityId, string element, bool immune, string source = BaseSource)
        {
            if (string.IsNullOrEmpty(element))
                throw new ArgumentException("An element is needed.", nameof(element));
            source = source ?? BaseSource;
            if (!_immune.TryGetValue(entityId, out var elements))
            {
                if (!immune)
                    return;
                _immune[entityId] = elements = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            }
            if (!elements.TryGetValue(element, out var sources))
            {
                if (!immune)
                    return;
                elements[element] = sources = new HashSet<string>(StringComparer.Ordinal);
            }
            if (immune)
                sources.Add(source);
            else
                sources.Remove(source);
        }

        /// <summary>
        /// Sets an entity's own resistances from its content (replacing the ones it had by itself).
        /// </summary>
        public void SetBase(int entityId, IEnumerable<ElementResistance> resistances)
        {
            RemoveSource(entityId, BaseSource);
            if (resistances == null)
                return;
            foreach (var resistance in resistances)
            {
                if (resistance == null || string.IsNullOrEmpty(resistance.Element))
                    continue;
                if (resistance.Value != 0f)
                    Set(entityId, resistance.Element, resistance.Value + Of(entityId, resistance.Element, BaseSource));
                if (resistance.Immune)
                    SetImmune(entityId, resistance.Element, true);
            }
        }

        /// <summary>
        /// Takes away everything one source gave an entity (gear taken off, a buff ended).
        /// </summary>
        /// <returns>False when the source gave it nothing.</returns>
        public bool RemoveSource(int entityId, string source)
        {
            source = source ?? BaseSource;
            bool removed = false;
            if (_values.TryGetValue(entityId, out var elements))
            {
                foreach (var sources in elements.Values)
                    removed |= sources.Remove(source);
            }
            if (_immune.TryGetValue(entityId, out var immunities))
            {
                foreach (var sources in immunities.Values)
                    removed |= sources.Remove(source);
            }
            return removed;
        }

        /// <summary>
        /// Forgets an entity (it left the game).
        /// </summary>
        public void Forget(int entityId)
        {
            _values.Remove(entityId);
            _immune.Remove(entityId);
        }

        /// <summary>
        /// An entity's resistance to an element: every source added up, held between the limits.
        /// </summary>
        public float Of(int entityId, string element)
        {
            if (element == null || !_values.TryGetValue(entityId, out var elements) || !elements.TryGetValue(element, out var sources))
                return 0f;
            float total = 0f;
            foreach (var value in sources.Values)
                total += value;
            return Math.Max(MaxWeakness, Math.Min(MaxResistance, total));
        }

        private float Of(int entityId, string element, string source) =>
            element != null && _values.TryGetValue(entityId, out var elements) && elements.TryGetValue(element, out var sources) &&
            sources.TryGetValue(source, out float value)
                ? value
                : 0f;

        public bool IsImmune(int entityId, string element) =>
            element != null && _immune.TryGetValue(entityId, out var elements) && elements.TryGetValue(element, out var sources) &&
            sources.Count > 0;

        /// <summary>
        /// The elements an entity has anything against (a resistance, a weakness or an immunity).
        /// </summary>
        public IReadOnlyList<string> ElementsOf(int entityId)
        {
            var elements = new List<string>();
            if (_values.TryGetValue(entityId, out var values))
            {
                foreach (var pair in values)
                {
                    if (pair.Value.Count > 0)
                        elements.Add(pair.Key);
                }
            }
            if (_immune.TryGetValue(entityId, out var immunities))
            {
                foreach (var pair in immunities)
                {
                    if (pair.Value.Count > 0 && !elements.Contains(pair.Key))
                        elements.Add(pair.Key);
                }
            }
            elements.Sort(StringComparer.Ordinal);
            return elements;
        }

        /// <summary>
        /// Damage of one element after the entity's resistance (none for an immunity).
        /// </summary>
        public float Apply(int entityId, string element, float damage)
        {
            if (damage <= 0f || string.IsNullOrEmpty(element))
                return Math.Max(0f, damage);
            return IsImmune(entityId, element) ? 0f : damage * (1f - Of(entityId, element));
        }

        /// <summary>
        /// Damage carrying several elements (a fire-and-ice blade), split evenly between them; none: as it is.
        /// </summary>
        public float Apply(int entityId, IReadOnlyList<string> elements, float damage)
        {
            if (elements == null || elements.Count == 0 || damage <= 0f)
                return Math.Max(0f, damage);
            float share = damage / elements.Count;
            float total = 0f;
            foreach (var element in elements)
                total += Apply(entityId, element, share);
            return total;
        }
    }
}
