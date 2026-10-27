using System;
using System.Collections.Generic;

namespace RPGFramework.Persistence
{
    /// <summary>
    /// Saved-game payload written by <see cref="SaveGameCoordinator"/>: one serialised section per participant.
    /// </summary>
    public sealed class SaveGameState
    {
        /// <summary>
        /// 2 added <see cref="SectionVersions"/>; version 1 saves still load (every section counts as version 1).
        /// </summary>
        public const int CurrentVersion = 2;

        public int Version { get; set; } = CurrentVersion;

        /// <summary>
        /// Participant key → Base64 of the section bytes (independent of the outer save serializer).
        /// </summary>
        public Dictionary<string, string> Sections { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Participant key → <see cref="ISaveParticipant.Version"/> it was saved with.
        /// </summary>
        public Dictionary<string, int> SectionVersions { get; set; } = new Dictionary<string, int>();
    }

    /// <summary>
    /// Saves and loads the whole game through registered <see cref="ISaveParticipant"/>s, so no single class has to
    /// know every system. Loading is all-or-nothing up to the restore step: every section is deserialised before any
    /// participant is touched. Participants are restored in registration order; one with no section in the save
    /// keeps its current state and is listed in <see cref="LastMissingSections"/>.
    /// Each section carries its participant's version: older ones are upgraded by the participant
    /// (<see cref="LastUpgradedSections"/>), newer ones (a save from a newer build) fail the load before anything
    /// changes. When the latest save cannot be read, the previous one is loaded (<see cref="LoadResult.FromPrevious"/>).
    /// </summary>
    public sealed class SaveGameCoordinator
    {
        public const string QuickSaveSlot = "quicksave";

        private readonly ISaveSystem _saveSystem;
        private readonly ISaveSerializer _sectionSerializer;
        private readonly List<ISaveParticipant> _participants = new List<ISaveParticipant>();
        private readonly List<string> _missingSections = new List<string>();
        private readonly List<string> _upgradedSections = new List<string>();

        /// <param name="sectionSerializer">Serialises each participant's state; JSON by default.</param>
        public SaveGameCoordinator(ISaveSystem saveSystem, ISaveSerializer sectionSerializer = null)
        {
            _saveSystem = saveSystem ?? throw new ArgumentNullException(nameof(saveSystem));
            _sectionSerializer = sectionSerializer ?? new JsonSaveSerializer();
        }

        public IReadOnlyList<string> ParticipantKeys
        {
            get
            {
                var keys = new List<string>(_participants.Count);
                foreach (var participant in _participants)
                    keys.Add(participant.SaveKey);
                return keys;
            }
        }

        /// <summary>
        /// Participants that had no section in the last successful load (e.g. a system added after the save was made).
        /// </summary>
        public IReadOnlyList<string> LastMissingSections => _missingSections;

        /// <summary>
        /// Participants whose section was saved with an older version and upgraded in the last successful load.
        /// </summary>
        public IReadOnlyList<string> LastUpgradedSections => _upgradedSections;

        /// <summary>
        /// Sections in the save on disk that no registered participant owns are carried over when saving, instead of
        /// being dropped: a mode of the game that runs only some of its systems (a co-op guest saving just their
        /// progress) does not erase the rest. Off by default.
        /// </summary>
        public bool KeepUnknownSections { get; set; }

        public SaveGameCoordinator Register(ISaveParticipant participant)
        {
            if (participant == null)
                throw new ArgumentNullException(nameof(participant));
            if (string.IsNullOrEmpty(participant.SaveKey))
                throw new ArgumentException("Save participant needs a key", nameof(participant));
            if (Find(participant.SaveKey) != null)
                throw new InvalidOperationException($"A save participant with key '{participant.SaveKey}' is already registered");
            _participants.Add(participant);
            return this;
        }

        public bool Unregister(string saveKey)
        {
            var participant = Find(saveKey);
            return participant != null && _participants.Remove(participant);
        }

        public SaveResult Save(string slotId, ISaveMetadata metadata)
        {
            if (!TryCapture(out var state, out var error))
                return SaveResult.CreateFailure(slotId, SaveError.SerializationFailed, error);
            if (KeepUnknownSections)
                CarryOverUnknownSections(slotId, state);
            return _saveSystem.Save(slotId, state, metadata);
        }

        private void CarryOverUnknownSections(string slotId, SaveGameState state)
        {
            if (!_saveSystem.HasSave(slotId))
                return;
            var existing = _saveSystem.Load<SaveGameState>(slotId);
            if (!existing.Success || !(existing.GameState is SaveGameState saved) || saved.Sections == null)
                return;
            foreach (var section in saved.Sections)
            {
                if (state.Sections.ContainsKey(section.Key))
                    continue;
                state.Sections[section.Key] = section.Value;
                if (saved.SectionVersions != null && saved.SectionVersions.TryGetValue(section.Key, out int version))
                    state.SectionVersions[section.Key] = version;
            }
        }

        public LoadResult Load(string slotId)
        {
            var result = _saveSystem.Load<SaveGameState>(slotId);
            if (!result.Success)
                return result;
            var outcome = Restore(slotId, result, out bool touched);
            if (outcome.Success || touched || result.FromPrevious || outcome.Error == SaveError.IncompatibleVersion)
                return outcome;

            // The latest save is intact on disk but its contents cannot be used: try the one before it
            var previous = _saveSystem.LoadPrevious<SaveGameState>(slotId);
            if (!previous.Success)
                return outcome;
            var fallback = Restore(slotId, previous, out _);
            if (!fallback.Success)
                return outcome;
            fallback.FromPrevious = true;
            fallback.ErrorMessage = outcome.ErrorMessage;
            return fallback;
        }

        /// <param name="touched">Whether any participant was restored (a failure after that cannot be undone).</param>
        private LoadResult Restore(string slotId, LoadResult result, out bool touched)
        {
            touched = false;
            if (!(result.GameState is SaveGameState state) || state.Sections == null)
                return LoadResult.CreateFailure(slotId, SaveError.CorruptedData, "Save does not contain a coordinated game state");
            if (state.Version > SaveGameState.CurrentVersion)
                return LoadResult.CreateFailure(slotId, SaveError.IncompatibleVersion,
                    $"Save format version {state.Version} is newer than supported version {SaveGameState.CurrentVersion}");

            var restores = new List<KeyValuePair<ISaveParticipant, object>>(_participants.Count);
            var missing = new List<string>();
            var upgraded = new List<string>();
            foreach (var participant in _participants)
            {
                if (!state.Sections.TryGetValue(participant.SaveKey, out var encoded) || encoded == null)
                {
                    missing.Add(participant.SaveKey);
                    continue;
                }
                int savedVersion = state.SectionVersions != null && state.SectionVersions.TryGetValue(participant.SaveKey, out int v) ? v : 1;
                if (savedVersion > participant.Version)
                    return LoadResult.CreateFailure(slotId, SaveError.IncompatibleVersion,
                        $"Section '{participant.SaveKey}' was saved by a newer version of the game (section version {savedVersion}, this build reads up to {participant.Version})");
                if (savedVersion < participant.Version)
                    upgraded.Add(participant.SaveKey);
                try
                {
                    var section = participant.Read(Convert.FromBase64String(encoded), savedVersion, _sectionSerializer);
                    restores.Add(new KeyValuePair<ISaveParticipant, object>(participant, section));
                }
                catch (Exception ex)
                {
                    return LoadResult.CreateFailure(slotId, SaveError.DeserializationFailed,
                        $"Section '{participant.SaveKey}' could not be read: {ex.Message}");
                }
            }

            var failures = new List<string>();
            touched = restores.Count > 0;
            foreach (var pair in restores)
            {
                try
                {
                    pair.Key.Restore(pair.Value);
                }
                catch (Exception ex)
                {
                    failures.Add($"'{pair.Key.SaveKey}': {ex.Message}");
                }
            }

            _missingSections.Clear();
            _missingSections.AddRange(missing);
            _upgradedSections.Clear();
            _upgradedSections.AddRange(upgraded);
            if (failures.Count > 0)
                return LoadResult.CreateFailure(slotId, SaveError.CorruptedData,
                    $"Game state partially restored; failed sections: {string.Join("; ", failures)}");
            return result;
        }

        public SaveResult QuickSave(ISaveMetadata metadata) => Save(QuickSaveSlot, metadata);

        public LoadResult QuickLoad() => Load(QuickSaveSlot);

        /// <summary>
        /// Captures the game and hands it to <see cref="ISaveSystem.TriggerAutoSave"/> (which honours AutoSaveEnabled).
        /// Returns false if a participant failed to capture.
        /// </summary>
        public bool AutoSave(ISaveMetadata metadata)
        {
            if (!TryCapture(out var state, out _))
                return false;
            _saveSystem.TriggerAutoSave(state, metadata);
            return true;
        }

        private bool TryCapture(out SaveGameState state, out string error)
        {
            state = new SaveGameState();
            foreach (var participant in _participants)
            {
                try
                {
                    state.Sections[participant.SaveKey] = Convert.ToBase64String(participant.Capture(_sectionSerializer));
                    state.SectionVersions[participant.SaveKey] = participant.Version;
                }
                catch (Exception ex)
                {
                    error = $"Section '{participant.SaveKey}' could not be captured: {ex.Message}";
                    state = null;
                    return false;
                }
            }
            error = null;
            return true;
        }

        private ISaveParticipant Find(string saveKey)
        {
            foreach (var participant in _participants)
            {
                if (string.Equals(participant.SaveKey, saveKey, StringComparison.Ordinal))
                    return participant;
            }
            return null;
        }
    }
}
