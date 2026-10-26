using System;
using System.Collections.Generic;

namespace RPGFramework.Persistence
{
    /// <summary>
    /// Saved-game payload written by <see cref="SaveGameCoordinator"/>: one serialised section per participant.
    /// </summary>
    public sealed class SaveGameState
    {
        public const int CurrentVersion = 1;

        public int Version { get; set; } = CurrentVersion;

        /// <summary>
        /// Participant key → Base64 of the section bytes (independent of the outer save serializer).
        /// </summary>
        public Dictionary<string, string> Sections { get; set; } = new Dictionary<string, string>();
    }

    /// <summary>
    /// Saves and loads the whole game through registered <see cref="ISaveParticipant"/>s, so no single class has to
    /// know every system. Loading is all-or-nothing up to the restore step: every section is deserialised before any
    /// participant is touched. Participants are restored in registration order; one with no section in the save
    /// keeps its current state and is listed in <see cref="LastMissingSections"/>.
    /// </summary>
    public sealed class SaveGameCoordinator
    {
        public const string QuickSaveSlot = "quicksave";

        private readonly ISaveSystem _saveSystem;
        private readonly ISaveSerializer _sectionSerializer;
        private readonly List<ISaveParticipant> _participants = new List<ISaveParticipant>();
        private readonly List<string> _missingSections = new List<string>();

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
            return _saveSystem.Save(slotId, state, metadata);
        }

        public LoadResult Load(string slotId)
        {
            var result = _saveSystem.Load<SaveGameState>(slotId);
            if (!result.Success)
                return result;
            if (!(result.GameState is SaveGameState state) || state.Sections == null)
                return LoadResult.CreateFailure(slotId, SaveError.CorruptedData, "Save does not contain a coordinated game state");
            if (state.Version > SaveGameState.CurrentVersion)
                return LoadResult.CreateFailure(slotId, SaveError.IncompatibleVersion,
                    $"Save format version {state.Version} is newer than supported version {SaveGameState.CurrentVersion}");

            var restores = new List<KeyValuePair<ISaveParticipant, object>>(_participants.Count);
            var missing = new List<string>();
            foreach (var participant in _participants)
            {
                if (!state.Sections.TryGetValue(participant.SaveKey, out var encoded) || encoded == null)
                {
                    missing.Add(participant.SaveKey);
                    continue;
                }
                try
                {
                    var section = participant.Read(Convert.FromBase64String(encoded), _sectionSerializer);
                    restores.Add(new KeyValuePair<ISaveParticipant, object>(participant, section));
                }
                catch (Exception ex)
                {
                    return LoadResult.CreateFailure(slotId, SaveError.DeserializationFailed,
                        $"Section '{participant.SaveKey}' could not be read: {ex.Message}");
                }
            }

            var failures = new List<string>();
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
