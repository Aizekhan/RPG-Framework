using RPGFramework.Events;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RPGFramework.Persistence
{
    /// <summary>
    /// Saves into slots on disk: the data, a sidecar with metadata and checksum, and the previous save kept beside it
    /// (<c>slot.previous.sav</c>) on every save while it was intact. A latest save that fails its checksum or cannot be
    /// read falls back to the previous one (<see cref="LoadResult.FromPrevious"/>).
    /// </summary>
    public class SaveSystem : ISaveSystem
    {
        private readonly SaveSlotManager _slotManager;
        private readonly ISaveSerializer _serializer;
        private readonly ISaveValidator _validator;
        private readonly JsonSaveSerializer _infoSerializer = new JsonSaveSerializer();

        private ISaveSlot _activeSlot;
        private float _timeSinceLastAutoSave;

        public IReadOnlyList<ISaveSlot> Slots => _slotManager.GetAllSlots();
        public ISaveSlot ActiveSlot => _activeSlot;
        public int MaxSlots { get; }
        public bool AutoSaveEnabled { get; set; }
        public float AutoSaveIntervalSeconds { get; set; }

        // Events - subscribe to these to be notified of save/load operations
        public event Action<SaveCreatedEvent> OnSaveCreated;
        public event Action<SaveLoadedEvent> OnSaveLoaded;
        public event Action<SaveDeletedEvent> OnSaveDeleted;
        public event Action<SaveErrorEvent> OnSaveError;
        public event Action<AutoSaveTriggeredEvent> OnAutoSaveTriggered;

        /// <summary>
        /// Where the save events also go, besides the C# events (the game's bus); null: only the C# events.
        /// </summary>
        public IEventPublisher Events { get; set; }

        public SaveSystem(
            string saveDirectory,
            int maxSlots,
            ISaveSerializer serializer,
            ISaveValidator validator)
        {
            MaxSlots = maxSlots;
            _slotManager = new SaveSlotManager(saveDirectory, maxSlots);
            _serializer = serializer;
            _validator = validator;

            AutoSaveEnabled = true;
            AutoSaveIntervalSeconds = 300f; // 5 minutes default
            _timeSinceLastAutoSave = 0f;
        }

        public SaveResult Save(string slotId, object gameState, ISaveMetadata metadata)
        {
            try
            {
                // Validate slot exists
                if (!_slotManager.HasSlot(slotId))
                {
                    return SaveResult.CreateFailure(slotId, SaveError.SlotNotFound,
                        $"Save slot '{slotId}' does not exist.");
                }

                // Serialize game state
                byte[] data;
                try
                {
                    data = _serializer.Serialize(gameState);
                }
                catch (Exception ex)
                {
                    return SaveResult.CreateFailure(slotId, SaveError.SerializationFailed,
                        $"Failed to serialize game state: {ex.Message}");
                }

                // Create save file with checksum
                var checksum = _validator.ComputeChecksum(data);
                var saveFile = new SaveFile(metadata, data, checksum);

                // Validate integrity
                if (!_validator.ValidateIntegrity(saveFile))
                {
                    return SaveResult.CreateFailure(slotId, SaveError.InvalidChecksum,
                        "Save file failed integrity check.");
                }

                // Write to disk: the data, then the sidecar with metadata and checksum
                var filePath = _slotManager.GetFilePath(slotId);
                try
                {
                    KeepPrevious(filePath);
                    _slotManager.WriteSaveFile(filePath, data);
                    var info = new SaveFileInfo
                    {
                        Metadata = ToSaveMetadata(metadata),
                        Checksum = checksum,
                        DataLength = data.Length
                    };
                    _slotManager.WriteSaveFile(SaveSlotManager.GetInfoPath(filePath), _infoSerializer.Serialize(info));
                }
                catch (Exception ex)
                {
                    return SaveResult.CreateFailure(slotId, SaveError.IOError,
                        $"Failed to write save file: {ex.Message}");
                }

                // Update slot status
                _slotManager.MarkSlotOccupied(slotId, metadata);
                _activeSlot = _slotManager.GetSlot(slotId);

                // Raise event
                var raised0 = new SaveCreatedEvent(slotId, metadata);
                OnSaveCreated?.Invoke(raised0);
                Events?.Publish(raised0);

                return SaveResult.CreateSuccess(slotId, metadata);
            }
            catch (Exception ex)
            {
                var errorEvent = new SaveErrorEvent(slotId, SaveError.IOError, ex.Message);
                OnSaveError?.Invoke(errorEvent);
                Events?.Publish(errorEvent);
                return SaveResult.CreateFailure(slotId, SaveError.IOError, ex.Message);
            }
        }

        public LoadResult Load(string slotId) => LoadCore(slotId, null, previous: false);

        public LoadResult Load<TState>(string slotId)
        {
            var result = LoadCore(slotId, Deserializer<TState>(), previous: false);
            if (result.Success || (result.Error != SaveError.InvalidChecksum && result.Error != SaveError.DeserializationFailed))
                return result;
            var previous = LoadCore(slotId, Deserializer<TState>(), previous: true);
            if (!previous.Success)
                return result;
            previous.FromPrevious = true;
            previous.ErrorMessage = result.ErrorMessage;
            return previous;
        }

        public LoadResult LoadPrevious<TState>(string slotId) => LoadCore(slotId, Deserializer<TState>(), previous: true);

        /// <summary>
        /// Moves an unreadable save out of the slot (data and sidecar renamed with "unreadable" and the time), so
        /// the next save does not overwrite what someone may still recover. Returns the new data path, or null.
        /// </summary>
        public string SetAside(string slotId)
        {
            var filePath = _slotManager.GetFilePath(slotId);
            if (filePath == null || !_slotManager.FileExists(filePath))
                return null;
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture);
            string target = SiblingPath(filePath, ".unreadable-" + stamp);
            System.IO.File.Move(filePath, target);
            var info = SaveSlotManager.GetInfoPath(filePath);
            if (_slotManager.FileExists(info))
                System.IO.File.Move(info, SaveSlotManager.GetInfoPath(target));
            _slotManager.MarkSlotEmpty(slotId);
            return target;
        }

        /// <summary>
        /// Where the save that the latest one replaced is kept.
        /// </summary>
        public static string GetPreviousPath(string filePath) => SiblingPath(filePath, ".previous");

        private static string SiblingPath(string filePath, string tag) =>
            System.IO.Path.Combine(System.IO.Path.GetDirectoryName(filePath) ?? "",
                System.IO.Path.GetFileNameWithoutExtension(filePath) + tag + System.IO.Path.GetExtension(filePath));

        private Func<byte[], object> Deserializer<TState>() => data => _serializer.Deserialize<TState>(data);

        /// <summary>
        /// Copies the current save aside before it is replaced, unless it is damaged (a damaged one must not push out
        /// a good previous save).
        /// </summary>
        private void KeepPrevious(string filePath)
        {
            if (!_slotManager.FileExists(filePath))
                return;
            var data = _slotManager.ReadSaveFile(filePath);
            var info = ReadInfo(filePath);
            if (data == null || (info != null && !string.IsNullOrEmpty(info.Checksum) && _validator.ComputeChecksum(data) != info.Checksum))
                return;
            var previous = GetPreviousPath(filePath);
            _slotManager.WriteSaveFile(previous, data);
            var infoPath = SaveSlotManager.GetInfoPath(filePath);
            var previousInfo = SaveSlotManager.GetInfoPath(previous);
            if (_slotManager.FileExists(infoPath))
                _slotManager.WriteSaveFile(previousInfo, _slotManager.ReadSaveFile(infoPath));
            else if (_slotManager.FileExists(previousInfo))
                System.IO.File.Delete(previousInfo);
        }

        private LoadResult LoadCore(string slotId, Func<byte[], object> deserialize, bool previous)
        {
            try
            {
                // Validate slot exists
                if (!_slotManager.HasSlot(slotId))
                {
                    return LoadResult.CreateFailure(slotId, SaveError.SlotNotFound,
                        $"Save slot '{slotId}' does not exist.");
                }

                // Check if slot is occupied
                var filePath = _slotManager.GetFilePath(slotId);
                if (previous)
                {
                    filePath = GetPreviousPath(filePath);
                    if (!_slotManager.FileExists(filePath))
                        return LoadResult.CreateFailure(slotId, SaveError.FileNotFound, $"Save slot '{slotId}' has no previous save.");
                }
                else if (!_slotManager.IsSlotOccupied(slotId))
                {
                    return LoadResult.CreateFailure(slotId, SaveError.FileNotFound,
                        $"Save slot '{slotId}' is empty.");
                }

                // Read save file
                byte[] data;
                try
                {
                    data = _slotManager.ReadSaveFile(filePath);
                    if (data == null)
                    {
                        return LoadResult.CreateFailure(slotId, SaveError.FileNotFound,
                            "Save file could not be read.");
                    }
                }
                catch (Exception ex)
                {
                    return LoadResult.CreateFailure(slotId, SaveError.IOError,
                        $"Failed to read save file: {ex.Message}");
                }

                // Verify the data against the checksum recorded at save time (saves without a sidecar predate it)
                var info = ReadInfo(filePath);
                if (info != null && !string.IsNullOrEmpty(info.Checksum) &&
                    _validator.ComputeChecksum(data) != info.Checksum)
                {
                    var raised2 = new SaveErrorEvent(slotId, SaveError.InvalidChecksum, "Save data does not match its checksum.");
                    OnSaveError?.Invoke(raised2);
                    Events?.Publish(raised2);
                    return LoadResult.CreateFailure(slotId, SaveError.InvalidChecksum,
                        "Save data does not match its checksum (corrupted or modified).");
                }

                // Deserialize game state. The serializers rebuild objects by reflection, so they need the concrete
                // type: deserializing as object used to return an empty object and silently lose the state.
                object gameState = null;
                try
                {
                    if (deserialize != null)
                        gameState = deserialize(data);
                }
                catch (Exception ex)
                {
                    return LoadResult.CreateFailure(slotId, SaveError.DeserializationFailed,
                        $"Failed to deserialize game state: {ex.Message}");
                }

                // Get metadata (known after a save in this session, otherwise from the sidecar)
                var slot = _slotManager.GetSlot(slotId);
                var metadata = previous ? info?.Metadata : slot.Metadata ?? info?.Metadata;
                if (slot.Metadata == null && metadata != null)
                    _slotManager.MarkSlotOccupied(slotId, metadata);

                // Update active slot
                _activeSlot = slot;

                // Raise event
                var raised3 = new SaveLoadedEvent(slotId, metadata);
                OnSaveLoaded?.Invoke(raised3);
                Events?.Publish(raised3);

                return LoadResult.CreateSuccess(slotId, gameState, metadata);
            }
            catch (Exception ex)
            {
                var errorEvent = new SaveErrorEvent(slotId, SaveError.IOError, ex.Message);
                OnSaveError?.Invoke(errorEvent);
                Events?.Publish(errorEvent);
                return LoadResult.CreateFailure(slotId, SaveError.IOError, ex.Message);
            }
        }

        public bool DeleteSave(string slotId)
        {
            try
            {
                if (!_slotManager.HasSlot(slotId))
                    return false;

                var filePath = _slotManager.GetFilePath(slotId);
                var success = _slotManager.DeleteSaveFile(filePath);
                _slotManager.DeleteSaveFile(GetPreviousPath(filePath));

                if (success)
                {
                    _slotManager.MarkSlotEmpty(slotId);
                    var raised5 = new SaveDeletedEvent(slotId);
                    OnSaveDeleted?.Invoke(raised5);
                    Events?.Publish(raised5);

                    if (_activeSlot?.SlotId == slotId)
                    {
                        _activeSlot = null;
                    }
                }

                return success;
            }
            catch
            {
                return false;
            }
        }

        public ISaveMetadata GetMetadata(string slotId)
        {
            var slot = _slotManager.GetSlot(slotId);
            if (slot == null || slot.Metadata != null || !slot.IsOccupied)
                return slot?.Metadata;

            // Saves from an earlier session: read the sidecar on demand
            var metadata = ReadInfo(_slotManager.GetFilePath(slotId))?.Metadata;
            if (metadata != null)
                _slotManager.MarkSlotOccupied(slotId, metadata);
            return metadata;
        }

        private SaveFileInfo ReadInfo(string filePath)
        {
            try
            {
                var bytes = _slotManager.ReadSaveFile(SaveSlotManager.GetInfoPath(filePath));
                return bytes == null ? null : _infoSerializer.Deserialize<SaveFileInfo>(bytes);
            }
            catch
            {
                // An unreadable sidecar must not make the save itself unloadable
                return null;
            }
        }

        private static SaveMetadata ToSaveMetadata(ISaveMetadata metadata)
        {
            if (metadata == null)
                return null;
            if (metadata is SaveMetadata concrete)
                return concrete;
            return new SaveMetadata
            {
                Timestamp = metadata.Timestamp,
                GameVersion = metadata.GameVersion,
                CharacterName = metadata.CharacterName,
                PlaytimeSeconds = metadata.PlaytimeSeconds,
                LocationName = metadata.LocationName,
                CharacterLevel = metadata.CharacterLevel,
                CustomData = metadata.CustomData != null
                    ? new System.Collections.Generic.Dictionary<string, string>(metadata.CustomData)
                    : new System.Collections.Generic.Dictionary<string, string>()
            };
        }

        public bool HasSave(string slotId)
        {
            return _slotManager.IsSlotOccupied(slotId);
        }

        public SaveResult QuickSave(object gameState, ISaveMetadata metadata)
        {
            return Save("quicksave", gameState, metadata);
        }

        public LoadResult QuickLoad()
        {
            return Load("quicksave");
        }

        public LoadResult QuickLoad<TState>()
        {
            return Load<TState>("quicksave");
        }

        public void TriggerAutoSave(object gameState, ISaveMetadata metadata)
        {
            if (!AutoSaveEnabled)
                return;

            var raised6 = new AutoSaveTriggeredEvent("autosave", _timeSinceLastAutoSave);

            OnAutoSaveTriggered?.Invoke(raised6);

            Events?.Publish(raised6);
            Save("autosave", gameState, metadata);
            _timeSinceLastAutoSave = 0f;
        }

        public void Update(float deltaTime)
        {
            if (AutoSaveEnabled)
            {
                _timeSinceLastAutoSave += deltaTime;
            }
        }

        public bool ShouldAutoSave()
        {
            return AutoSaveEnabled && _timeSinceLastAutoSave >= AutoSaveIntervalSeconds;
        }
    }
}
