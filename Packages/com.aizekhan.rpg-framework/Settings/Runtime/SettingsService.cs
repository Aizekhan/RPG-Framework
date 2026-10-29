using System;
using System.Collections.Generic;
using System.Linq;
using RPGFramework.Events;

namespace RPGFramework.Settings
{
    /// <summary>
    /// Where settings are kept between starts (the device's settings, not a save: a new game keeps them).
    /// </summary>
    public interface ISettingsStore
    {
        bool TryLoad(string id, out string value);
        void Save(string id, string value);
        void Delete(string id);

        /// <summary>
        /// Writes what was saved (to disk), after a batch of changes.
        /// </summary>
        void Flush();
    }

    /// <summary>
    /// Settings kept only while the game runs (tests, servers).
    /// </summary>
    public sealed class MemorySettingsStore : ISettingsStore
    {
        public Dictionary<string, string> Values { get; } = new Dictionary<string, string>(StringComparer.Ordinal);
        public int Flushes { get; private set; }

        public bool TryLoad(string id, out string value) => Values.TryGetValue(id, out value);
        public void Save(string id, string value) => Values[id] = value;
        public void Delete(string id) => Values.Remove(id);
        public void Flush() => Flushes++;
    }

    public readonly struct SettingChangedEvent : IEvent
    {
        public SettingChangedEvent(string id, string value)
        {
            Id = id;
            Value = value;
        }

        public string Id { get; }

        /// <summary>
        /// The new value as the store keeps it.
        /// </summary>
        public string Value { get; }
    }

    /// <summary>
    /// The game's options for a settings page. Modules and the game add theirs (<see cref="Add{TSetting}"/>); each comes
    /// back with its kept value, and every change is kept and announced (<see cref="Changed"/>,
    /// <see cref="SettingChangedEvent"/>). Apply a value where it acts: in the setting's <c>Changed</c>, or by
    /// reading it when needed.
    /// </summary>
    public interface ISettingsService
    {
        IReadOnlyList<Setting> All { get; }

        /// <summary>
        /// The groups in the order their first setting came ("Display", "Audio", "Game").
        /// </summary>
        IReadOnlyList<string> Groups { get; }

        IEnumerable<Setting> InGroup(string group);

        /// <summary>
        /// Adds a setting and loads its kept value; a setting with the same id is replaced.
        /// </summary>
        TSetting Add<TSetting>(TSetting setting) where TSetting : Setting;

        bool Remove(string id);

        /// <returns>The setting, or null.</returns>
        Setting Find(string id);

        /// <returns>The setting of that type, or null.</returns>
        TSetting Find<TSetting>(string id) where TSetting : Setting;

        bool Toggle(string id, bool fallback = false);
        float Slider(string id, float fallback = 0f);

        /// <summary>
        /// The chosen option's text.
        /// </summary>
        string Choice(string id, string fallback = "");

        /// <summary>
        /// Puts every setting (or a group's) back to its default.
        /// </summary>
        void ResetToDefaults(string group = null);

        event Action<Setting> Changed;
    }

    public sealed class SettingsService : ISettingsService
    {
        private readonly List<Setting> _settings = new List<Setting>();
        private readonly List<string> _groups = new List<string>();
        private readonly ISettingsStore _store;
        private readonly IEventPublisher _events;
        private bool _batching;
        private bool _dirty;

        /// <param name="store">Null: kept only while the game runs.</param>
        public SettingsService(ISettingsStore store = null, IEventPublisher events = null)
        {
            _store = store ?? new MemorySettingsStore();
            _events = events;
        }

        public IReadOnlyList<Setting> All => _settings;
        public IReadOnlyList<string> Groups => _groups;

        public event Action<Setting> Changed;

        public IEnumerable<Setting> InGroup(string group) =>
            _settings.Where(s => string.Equals(s.Group, group ?? "", StringComparison.Ordinal));

        public TSetting Add<TSetting>(TSetting setting) where TSetting : Setting
        {
            if (setting == null)
                throw new ArgumentNullException(nameof(setting));
            int existing = _settings.FindIndex(s => s.Id == setting.Id);
            if (existing >= 0)
            {
                _settings[existing].Changed -= OnChanged;
                _settings[existing] = setting;
            }
            else
            {
                _settings.Add(setting);
            }
            if (!_groups.Contains(setting.Group))
                _groups.Add(setting.Group);
            // A bound setting's service keeps the value; the store would fight it
            if (!setting.IsBound && _store.TryLoad(setting.Id, out string kept))
                setting.TryLoad(kept);
            setting.Changed += OnChanged;
            return setting;
        }

        public bool Remove(string id)
        {
            int index = _settings.FindIndex(s => s.Id == id);
            if (index < 0)
                return false;
            var setting = _settings[index];
            setting.Changed -= OnChanged;
            _settings.RemoveAt(index);
            if (!_settings.Any(s => s.Group == setting.Group))
                _groups.Remove(setting.Group);
            return true;
        }

        public Setting Find(string id) => _settings.FirstOrDefault(s => s.Id == id);

        public TSetting Find<TSetting>(string id) where TSetting : Setting => Find(id) as TSetting;

        public bool Toggle(string id, bool fallback = false) => Find<ToggleSetting>(id)?.Value ?? fallback;

        public float Slider(string id, float fallback = 0f) => Find<SliderSetting>(id)?.Value ?? fallback;

        public string Choice(string id, string fallback = "") => Find<ChoiceSetting>(id)?.Value ?? fallback;

        public void ResetToDefaults(string group = null)
        {
            _batching = true;
            try
            {
                foreach (var setting in _settings.ToList())
                {
                    if (group == null || setting.Group == group)
                        setting.Reset();
                }
            }
            finally
            {
                _batching = false;
            }
            if (_dirty)
            {
                _dirty = false;
                _store.Flush();
            }
        }

        private void OnChanged(Setting setting)
        {
            if (!setting.IsBound)
            {
                // A default is not kept: a later version may choose a better one
                if (setting.IsDefault)
                    _store.Delete(setting.Id);
                else
                    _store.Save(setting.Id, setting.Serialized);
                if (_batching)
                    _dirty = true;
                else
                    _store.Flush();
            }
            Changed?.Invoke(setting);
            _events?.Publish(new SettingChangedEvent(setting.Id, setting.Serialized));
        }
    }
}
