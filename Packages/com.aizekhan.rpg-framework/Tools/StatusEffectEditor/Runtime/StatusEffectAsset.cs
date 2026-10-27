using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using RPGFramework.StatusEffects;

namespace RPGFramework.Tools.StatusEffectEditor
{
    /// <summary>
    /// A status effect (poison, haste, stun...) made in the Unity inspector. Converts to the
    /// <see cref="StatusEffectData"/> the status effect manager runs on.
    /// </summary>
    [CreateAssetMenu(fileName = "NewStatusEffect", menuName = "RPG Framework/Status Effect", order = 105)]
    public class StatusEffectAsset : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Unique id the game uses to apply this effect, e.g. 'poison'")]
        public string effectId = "effect_new";

        [Tooltip("Name shown to players")]
        public string effectName = "New Effect";

        [Tooltip("Text shown in tooltips")]
        [TextArea(2, 5)]
        public string description = "";

        [Tooltip("What kind of effect this is: a buff, a debuff, damage over time, healing over time, a stat change or a condition like stun")]
        public EffectType effectType = EffectType.Buff;

        [Header("Duration")]
        [Tooltip("Timed: lasts for Duration seconds. Instant: gone on the next update. Permanent: stays until removed")]
        public StatusEffectLength length = StatusEffectLength.Timed;

        [Tooltip("How many seconds the effect lasts (for a timed effect). Applying it again starts the time over")]
        [Min(0f)]
        public float duration = 10f;

        [Tooltip("Seconds between ticks of damage or healing; 0 means the effect never ticks")]
        [Min(0f)]
        public float tickInterval = 0f;

        [Header("Stacking")]
        [Tooltip("Whether applying the effect again adds a stack (damage and healing per tick grow with stacks). Otherwise applying it again only starts the time over")]
        public bool isStackable = false;

        [Tooltip("Most stacks the effect can have; keep it at 1 for an effect that does not stack")]
        [Min(1)]
        public int maxStacks = 1;

        [Header("Modifiers")]
        [Tooltip("What the effect does: damage or healing per tick, or changes to stats, resources, movement or control")]
        public List<StatusEffectModifierEntry> modifiers = new List<StatusEffectModifierEntry>();

        /// <summary>
        /// Converts this asset to the runtime definition. A permanent effect gets duration -1, an instant one 0.
        /// Empty modifier slots are left out.
        /// </summary>
        public StatusEffectData ToData()
        {
            return new StatusEffectData
            {
                Id = effectId,
                Name = effectName,
                Description = description ?? string.Empty,
                Type = effectType,
                Duration = RuntimeDuration,
                IsStackable = isStackable,
                MaxStacks = maxStacks,
                TickInterval = tickInterval,
                Modifiers = (modifiers ?? new List<StatusEffectModifierEntry>())
                    .Where(m => m != null)
                    .Select(m => m.ToModifierData())
                    .ToList()
            };
        }

        /// <summary>
        /// The duration the runtime uses: -1 for permanent, 0 for instant, the seconds for timed.
        /// </summary>
        public float RuntimeDuration
        {
            get
            {
                switch (length)
                {
                    case StatusEffectLength.Permanent: return -1f;
                    case StatusEffectLength.Instant: return 0f;
                    default: return duration;
                }
            }
        }

        /// <summary>
        /// Whether the effect has damage or healing, the only things the runtime deals on each tick.
        /// </summary>
        public bool HasTickModifiers =>
            modifiers != null && modifiers.Any(m => m != null && IsTickType(m.type));

        /// <summary>
        /// Lists every problem that would make the effect behave differently than it reads. Empty means ready.
        /// </summary>
        public List<string> Validate()
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(effectId))
                errors.Add("Effect ID cannot be empty");

            if (string.IsNullOrWhiteSpace(effectName))
                errors.Add("Effect Name cannot be empty");

            if (length == StatusEffectLength.Timed && duration <= 0f)
                errors.Add("Duration must be above 0 for a timed effect (choose Instant or Permanent otherwise)");

            bool ticks = HasTickModifiers;

            if (tickInterval < 0f)
                errors.Add("Tick Interval cannot be negative");

            if (ticks && tickInterval <= 0f && length != StatusEffectLength.Instant)
                errors.Add("The effect has damage or healing but Tick Interval is 0, so it never ticks");

            if (!ticks && tickInterval > 0f)
                errors.Add("Tick Interval is set but nothing ticks: add a Damage or Healing modifier or set it to 0");

            if (length == StatusEffectLength.Timed && duration > 0f && tickInterval > duration)
                errors.Add("Tick Interval is longer than Duration, so the effect ends before its first tick");

            if (length == StatusEffectLength.Instant && (ticks || tickInterval > 0f))
                errors.Add("An instant effect is gone on the next update and never ticks: make it timed or remove the ticking");

            if (maxStacks < 1)
                errors.Add("Max Stacks must be at least 1");
            else if (isStackable && maxStacks == 1)
                errors.Add("The effect is stackable but Max Stacks is 1, so it can never stack");
            else if (!isStackable && maxStacks > 1)
                errors.Add("Max Stacks is above 1 but the effect is not stackable, so the extra stacks are never used");

            if (effectType == EffectType.DamageOverTime && !HasModifier(ModifierType.Damage))
                errors.Add("A damage over time effect needs a Damage modifier");

            if (effectType == EffectType.HealOverTime && !HasModifier(ModifierType.Healing))
                errors.Add("A healing over time effect needs a Healing modifier");

            if (modifiers != null)
            {
                for (int i = 0; i < modifiers.Count; i++)
                {
                    var modifier = modifiers[i];
                    string label = $"Modifier {i + 1}";

                    if (modifier == null)
                    {
                        errors.Add($"{label} is an empty slot");
                        continue;
                    }

                    if (IsTickType(modifier.type))
                    {
                        if (modifier.value <= 0f)
                            errors.Add($"{label}: {modifier.type} per tick must be above 0");

                        if (modifier.operation != ModifierOperation.Add)
                            errors.Add($"{label}: {modifier.type} is always dealt as an amount per tick, so its operation must be Add");
                    }
                    else if (string.IsNullOrWhiteSpace(modifier.targetId))
                    {
                        errors.Add($"{label}: {modifier.type} needs a target (e.g. a stat name like 'MoveSpeed')");
                    }
                }
            }

            return errors;
        }

        /// <summary>
        /// One line describing the effect in plain words, e.g. "Poison: 5 damage every 1 s for 10 s (stacks to 3)".
        /// </summary>
        public string Describe()
        {
            var parts = new List<string>();
            var valid = (modifiers ?? new List<StatusEffectModifierEntry>()).Where(m => m != null).ToList();

            var tickParts = valid
                .Where(m => IsTickType(m.type))
                .Select(m => $"{Number(m.value)} {(m.type == ModifierType.Damage ? "damage" : "healing")}")
                .ToList();
            if (tickParts.Count > 0)
            {
                string tick = string.Join(" and ", tickParts);
                if (tickInterval > 0f)
                    tick += $" every {Number(tickInterval)} s";
                parts.Add(tick);
            }

            parts.AddRange(valid.Where(m => !IsTickType(m.type)).Select(DescribeChange));

            string what = parts.Count > 0 ? string.Join(", ", parts) : effectType.ToString();

            string when;
            switch (length)
            {
                case StatusEffectLength.Permanent: when = " until removed"; break;
                case StatusEffectLength.Instant: when = " instantly"; break;
                default: when = $" for {Number(duration)} s"; break;
            }

            string stacks = isStackable && maxStacks > 1 ? $" (stacks to {maxStacks})" : string.Empty;

            string title = string.IsNullOrWhiteSpace(effectName) ? effectId : effectName;
            return $"{title}: {what}{when}{stacks}";
        }

        private static string DescribeChange(StatusEffectModifierEntry modifier)
        {
            string target = string.IsNullOrWhiteSpace(modifier.targetId) ? modifier.type.ToString() : modifier.targetId;
            switch (modifier.operation)
            {
                case ModifierOperation.Multiply:
                    return $"{target} x{Number(modifier.value)}";
                case ModifierOperation.Override:
                    return $"{target} set to {Number(modifier.value)}";
                default:
                    return $"{target} {(modifier.value >= 0f ? "+" : "")}{Number(modifier.value)}";
            }
        }

        private bool HasModifier(ModifierType type)
        {
            return modifiers != null && modifiers.Any(m => m != null && m.type == type);
        }

        private static bool IsTickType(ModifierType type)
        {
            return type == ModifierType.Damage || type == ModifierType.Healing;
        }

        private static string Number(float value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private void OnValidate()
        {
            if (!string.IsNullOrWhiteSpace(effectId))
                effectId = effectId.Trim().ToLower().Replace(" ", "_");
        }
    }

    /// <summary>
    /// How long a status effect lasts.
    /// </summary>
    public enum StatusEffectLength
    {
        /// <summary>
        /// Lasts for the set number of seconds.
        /// </summary>
        Timed,

        /// <summary>
        /// Gone on the next update.
        /// </summary>
        Instant,

        /// <summary>
        /// Stays until something removes it.
        /// </summary>
        Permanent
    }

    /// <summary>
    /// One thing a status effect does, made in the Unity inspector.
    /// </summary>
    [System.Serializable]
    public class StatusEffectModifierEntry
    {
        [Tooltip("Damage or Healing are dealt on every tick; Stat, Resource, Movement and Control change something while the effect lasts")]
        public ModifierType type = ModifierType.Stat;

        [Tooltip("What is changed, e.g. 'AttackPower', 'MoveSpeed' or 'Health'")]
        public string targetId = "Health";

        [Tooltip("How much: the amount per tick for Damage and Healing, otherwise the change")]
        public float value = 5f;

        [Tooltip("Add: value + amount. Multiply: value x amount (1.5 = 50% more). Override: value becomes the amount. Damage and Healing always use Add")]
        public ModifierOperation operation = ModifierOperation.Add;

        /// <summary>
        /// Converts this entry to the runtime modifier.
        /// </summary>
        public EffectModifierData ToModifierData()
        {
            return EffectModifierData.Create(type, targetId, value, operation);
        }
    }
}
