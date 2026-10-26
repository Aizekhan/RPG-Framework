using System;
using System.Collections.Generic;
using System.Linq;

namespace RPGFramework.Persistence
{
    /// <summary>
    /// Core save system implementation.
    /// </summary>
    public class SaveSystem : ISaveSystem
    {
        private readonly SaveSlotManager _slotManager;
        private readonly ISaveSerializer _serializer;
        private readonly ISaveValidator _validator;

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

                // Write to disk
                var filePath = _slotManager.GetFilePath(slotId);
                try
                {
                    _slotManager.WriteSaveFile(filePath, data);
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
                OnSaveCreated?.Invoke(new SaveCreatedEvent(slotId, metadata));

                return SaveResult.CreateSuccess(slotId, metadata);
            }
            catch (Exception ex)
            {
                var errorEvent = new SaveErrorEvent(slotId, SaveError.IOError, ex.Message);
                OnSaveError?.Invoke(errorEvent);
                return SaveResult.CreateFailure(slotId, SaveError.IOError, ex.Message);
            }
        }

        public LoadResult Load(string slotId) => LoadCore(slotId, null);

        public LoadResult Load<TState>(string slotId) => LoadCore(slotId, data => _serializer.Deserialize<TState>(data));

        private LoadResult LoadCore(string slotId, Func<byte[], object> deserialize)
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
                if (!_slotManager.IsSlotOccupied(slotId))
                {
                    return LoadResult.CreateFailure(slotId, SaveError.FileNotFound,
                        $"Save slot '{slotId}' is empty.");
                }

                // Read save file
                var filePath = _slotManager.GetFilePath(slotId);
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

                // Get metadata
                var slot = _slotManager.GetSlot(slotId);
                var metadata = slot.Metadata;

                // Update active slot
                _activeSlot = slot;

                // Raise event
                OnSaveLoaded?.Invoke(new SaveLoadedEvent(slotId, metadata));

                return LoadResult.CreateSuccess(slotId, gameState, metadata);
            }
            catch (Exception ex)
            {
                var errorEvent = new SaveErrorEvent(slotId, SaveError.IOError, ex.Message);
                OnSaveError?.Invoke(errorEvent);
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

                if (success)
                {
                    _slotManager.MarkSlotEmpty(slotId);
                    OnSaveDeleted?.Invoke(new SaveDeletedEvent(slotId));

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
            return slot?.Metadata;
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

            OnAutoSaveTriggered?.Invoke(new AutoSaveTriggeredEvent("autosave", _timeSinceLastAutoSave));
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
