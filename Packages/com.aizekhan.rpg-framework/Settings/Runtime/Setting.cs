using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace RPGFramework.Settings
{
    public enum SettingKind
    {
        Toggle,
        Choice,
        Slider
    }

    /// <summary>
    /// One option of the settings page: an id ("display.vsync"), an English label, a group ("Display") and a value
    /// kept by <see cref="ISettingsService"/>. A setting made with a getter and a setter is bound to a service that keeps
    /// the value itself (volumes, the language): the settings page shows and changes it, the service stores it.
    /// </summary>
    public abstract class Setting
    {
        private Action<Setting> _changed;

        protected Setting(string id, string label, string group)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("A setting needs an id", nameof(id));
            Id = id.Trim();
            Label = string.IsNullOrEmpty(label) ? Id : label;
            Group = group ?? "";
        }

        public string Id { get; }

        /// <summary>
        /// The English label; a localized page reads the key <c>settings.&lt;id&gt;</c> first.
        /// </summary>
        public string Label { get; }

        public string Group { get; }
        public abstract SettingKind Kind { get; }

        /// <summary>
        /// Kept by a service of its own, not by the settings store.
        /// </summary>
        public abstract bool IsBound { get; }

        /// <summary>
        /// The value as text for the page ("On", "1920 x 1080", "80%").
        /// </summary>
        public abstract string Display { get; }

        /// <summary>
        /// The value as the store keeps it (invariant).
        /// </summary>
        public abstract string Serialized { get; }

        public abstract bool IsDefault { get; }

        /// <summary>
        /// The value changed (by the page, the game or a load).
        /// </summary>
        public event Action<Setting> Changed
        {
            add => _changed += value;
            remove => _changed -= value;
        }

        public abstract void Reset();

        /// <summary>
        /// One step forward (+1) or back (-1): the next choice, a slider step, a toggle flipped.
        /// </summary>
        public abstract void Step(int direction);

        /// <returns>False when the text is not a value of this setting (the value stays).</returns>
        public abstract bool TryLoad(string serialized);

        protected void RaiseChanged() => _changed?.Invoke(this);

        public override string ToString() => $"{Id} = {Serialized}";
    }

    public sealed class ToggleSetting : Setting
    {
        private readonly Func<bool> _get;
        private readonly Action<bool> _set;
        private bool _value;

        public ToggleSetting(string id, string label, bool defaultValue, string group = "")
            : base(id, label, group)
        {
            Default = _value = defaultValue;
        }

        /// <summary>
        /// Bound: reads and writes another service's value.
        /// </summary>
        public ToggleSetting(string id, string label, Func<bool> get, Action<bool> set, bool defaultValue = false, string group = "")
            : base(id, label, group)
        {
            _get = get ?? throw new ArgumentNullException(nameof(get));
            _set = set ?? throw new ArgumentNullException(nameof(set));
            Default = defaultValue;
        }

        public bool Default { get; }
        public override SettingKind Kind => SettingKind.Toggle;
        public override bool IsBound => _get != null;

        public bool Value
        {
            get => _get != null ? _get() : _value;
            set
            {
                if (value == Value)
                    return;
                if (_set != null)
                    _set(value);
                else
                    _value = value;
                RaiseChanged();
            }
        }

        public override string Display => Value ? "On" : "Off";
        public override string Serialized => Value ? "1" : "0";
        public override bool IsDefault => Value == Default;
        public override void Reset() => Value = Default;
        public override void Step(int direction) => Value = !Value;

        public override bool TryLoad(string serialized)
        {
            switch ((serialized ?? "").Trim().ToLowerInvariant())
            {
                case "1":
                case "true":
                case "on":
                    Value = true;
                    return true;
                case "0":
                case "false":
                case "off":
                    Value = false;
                    return true;
                default:
                    return false;
            }
        }
    }

    /// <summary>
    /// One of a list ("Windowed", "Borderless", "Full screen"); kept by the option's text, so a list that grows or
    /// changes order (screen sizes of another monitor) still finds the kept one.
    /// </summary>
    public sealed class ChoiceSetting : Setting
    {
        private readonly List<string> _options;
        private readonly Func<int> _get;
        private readonly Action<int> _set;
        private int _index;

        public ChoiceSetting(string id, string label, IEnumerable<string> options, int defaultIndex = 0, string group = "")
            : base(id, label, group)
        {
            _options = Options(options);
            Default = Clamp(defaultIndex);
            _index = Default;
        }

        /// <summary>
        /// Bound: reads and writes another service's choice by index.
        /// </summary>
        public ChoiceSetting(string id, string label, IEnumerable<string> options, Func<int> get, Action<int> set, int defaultIndex = 0,
            string group = "")
            : base(id, label, group)
        {
            _options = Options(options);
            _get = get ?? throw new ArgumentNullException(nameof(get));
            _set = set ?? throw new ArgumentNullException(nameof(set));
            Default = Clamp(defaultIndex);
        }

        public IReadOnlyList<string> Choices => _options;
        public int Default { get; }
        public override SettingKind Kind => SettingKind.Choice;
        public override bool IsBound => _get != null;

        public int Index
        {
            get => Clamp(_get != null ? _get() : _index);
            set
            {
                value = Clamp(value);
                if (value == Index)
                    return;
                if (_set != null)
                    _set(value);
                else
                    _index = value;
                RaiseChanged();
            }
        }

        public string Value => _options.Count == 0 ? "" : _options[Index];

        public override string Display => Value;
        public override string Serialized => Value;
        public override bool IsDefault => Index == Default;
        public override void Reset() => Index = Default;

        // Wraps around: the last choice steps on to the first
        public override void Step(int direction)
        {
            if (_options.Count > 0)
                Index = ((Index + Math.Sign(direction)) % _options.Count + _options.Count) % _options.Count;
        }

        public bool Select(string option)
        {
            int index = _options.FindIndex(o => string.Equals(o, option, StringComparison.OrdinalIgnoreCase));
            if (index < 0)
                return false;
            Index = index;
            return true;
        }

        public override bool TryLoad(string serialized) => Select((serialized ?? "").Trim());

        private int Clamp(int index) => _options.Count == 0 ? 0 : Math.Max(0, Math.Min(_options.Count - 1, index));

        private static List<string> Options(IEnumerable<string> options)
        {
            var list = (options ?? Enumerable.Empty<string>()).Where(o => o != null).ToList();
            if (list.Count == 0)
                throw new ArgumentException("A choice needs at least one option", nameof(options));
            return list;
        }
    }

    /// <summary>
    /// A number between two ends in steps (volume 0..1 by 0.1, camera shake 0..2 by 0.25, field of view 60..110 by 5).
    /// </summary>
    public sealed class SliderSetting : Setting
    {
        private readonly Func<float> _get;
        private readonly Action<float> _set;
        private float _value;

        public SliderSetting(string id, string label, float min, float max, float step, float defaultValue, string group = "")
            : base(id, label, group)
        {
            Check(min, max, step);
            Min = min;
            Max = max;
            StepSize = step;
            Default = _value = Clamp(defaultValue);
        }

        /// <summary>
        /// Bound: reads and writes another service's number.
        /// </summary>
        public SliderSetting(string id, string label, float min, float max, float step, Func<float> get, Action<float> set,
            float defaultValue, string group = "")
            : base(id, label, group)
        {
            Check(min, max, step);
            Min = min;
            Max = max;
            StepSize = step;
            _get = get ?? throw new ArgumentNullException(nameof(get));
            _set = set ?? throw new ArgumentNullException(nameof(set));
            Default = Clamp(defaultValue);
        }

        public float Min { get; }
        public float Max { get; }
        public float StepSize { get; }
        public float Default { get; }

        /// <summary>
        /// How <see cref="Display"/> shows the value: "{0:0%}" for 0..1 volumes (the default when Max is 1), "{0:0.##}".
        /// </summary>
        public string Format { get; set; }

        public override SettingKind Kind => SettingKind.Slider;
        public override bool IsBound => _get != null;

        /// <summary>
        /// Where the value is between the ends, 0..1 (for a slider control).
        /// </summary>
        public float Fraction
        {
            get => Max > Min ? (Value - Min) / (Max - Min) : 0f;
            set => Value = Min + (Max - Min) * Math.Max(0f, Math.Min(1f, value));
        }

        public float Value
        {
            get => _get != null ? _get() : _value;
            set
            {
                value = Clamp(value);
                if (Math.Abs(value - Value) < 1e-5f)
                    return;
                if (_set != null)
                    _set(value);
                else
                    _value = value;
                RaiseChanged();
            }
        }

        public override string Display =>
            string.Format(CultureInfo.InvariantCulture, Format ?? (Min >= 0f && Max <= 1f ? "{0:0%}" : "{0:0.##}"), Value);

        public override string Serialized => Value.ToString("R", CultureInfo.InvariantCulture);
        public override bool IsDefault => Math.Abs(Value - Default) < 1e-5f;
        public override void Reset() => Value = Default;
        public override void Step(int direction) => Value += StepSize * Math.Sign(direction);

        public override bool TryLoad(string serialized)
        {
            if (!float.TryParse((serialized ?? "").Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float value)
                || float.IsNaN(value) || float.IsInfinity(value))
                return false;
            Value = value;
            return true;
        }

        // Onto the nearest step from Min, inside the ends
        private float Clamp(float value)
        {
            if (float.IsNaN(value))
                value = Min;
            value = Math.Max(Min, Math.Min(Max, value));
            if (StepSize > 0f)
                value = Math.Min(Max, Min + (float)Math.Round((value - Min) / StepSize) * StepSize);
            return value;
        }

        private static void Check(float min, float max, float step)
        {
            if (!(max >= min))
                throw new ArgumentException("Max must not be below Min");
            if (step < 0f)
                throw new ArgumentException("The step must not be negative");
        }
    }
}
