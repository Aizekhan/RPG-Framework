using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RPGFramework.Persistence
{
    /// <summary>
    /// Manages save slots and file system operations.
    /// </summary>
    public class SaveSlotManager
    {
        private readonly string _saveDirectory;
        private readonly int _maxSlots;
        private readonly Dictionary<string, SaveSlotInfo> _slots;

        public SaveSlotManager(string saveDirectory, int maxSlots)
        {
            _saveDirectory = saveDirectory;
            _maxSlots = maxSlots;
            _slots = new Dictionary<string, SaveSlotInfo>();

            InitializeSlots();
        }

        private void InitializeSlots()
        {
            // Ensure save directory exists
            if (!Directory.Exists(_saveDirectory))
            {
                Directory.CreateDirectory(_saveDirectory);
            }

            // Initialize slot info
            for (int i = 0; i < _maxSlots; i++)
            {
                var slotId = $"slot_{i}";
                var filePath = Path.Combine(_saveDirectory, $"{slotId}.sav");
                var slotInfo = new SaveSlotInfo(slotId, filePath);

                // Check if save file exists
                if (File.Exists(filePath))
                {
                    slotInfo.IsOccupied = true;
                    // Metadata will be loaded on demand
                }

                _slots[slotId] = slotInfo;
            }

            // Add quick save slot
            var quickSaveId = "quicksave";
            var quickSavePath = Path.Combine(_saveDirectory, $"{quickSaveId}.sav");
            var quickSlot = new SaveSlotInfo(quickSaveId, quickSavePath);
            if (File.Exists(quickSavePath))
            {
                quickSlot.IsOccupied = true;
            }
            _slots[quickSaveId] = quickSlot;

            // Add auto-save slot
            var autoSaveId = "autosave";
            var autoSavePath = Path.Combine(_saveDirectory, $"{autoSaveId}.sav");
            var autoSlot = new SaveSlotInfo(autoSaveId, autoSavePath);
            if (File.Exists(autoSavePath))
            {
                autoSlot.IsOccupied = true;
            }
            _slots[autoSaveId] = autoSlot;
        }

        public IReadOnlyList<ISaveSlot> GetAllSlots()
        {
            return _slots.Values.Cast<ISaveSlot>().ToList();
        }

        public ISaveSlot GetSlot(string slotId)
        {
            return _slots.TryGetValue(slotId, out var slot) ? slot : null;
        }

        public bool HasSlot(string slotId)
        {
            return _slots.ContainsKey(slotId);
        }

        public bool IsSlotOccupied(string slotId)
        {
            return _slots.TryGetValue(slotId, out var slot) && slot.IsOccupied;
        }

        public void MarkSlotOccupied(string slotId, ISaveMetadata metadata)
        {
            if (_slots.TryGetValue(slotId, out var slot))
            {
                slot.IsOccupied = true;
                slot.Metadata = metadata;
            }
        }

        public void MarkSlotEmpty(string slotId)
        {
            if (_slots.TryGetValue(slotId, out var slot))
            {
                slot.IsOccupied = false;
                slot.Metadata = null;
            }
        }

        public string GetFilePath(string slotId)
        {
            return _slots.TryGetValue(slotId, out var slot) ? slot.FilePath : null;
        }

        /// <summary>
        /// Path of the sidecar <see cref="SaveFileInfo"/> for a save file.
        /// </summary>
        public static string GetInfoPath(string filePath)
        {
            return Path.ChangeExtension(filePath, ".info");
        }

        /// <summary>
        /// Writes through a temporary file, so a crash mid-write leaves the previous save intact.
        /// </summary>
        public void WriteSaveFile(string filePath, byte[] data)
        {
            var tempPath = filePath + ".tmp";
            File.WriteAllBytes(tempPath, data);
            if (File.Exists(filePath))
                File.Delete(filePath);
            File.Move(tempPath, filePath);
        }

        public byte[] ReadSaveFile(string filePath)
        {
            if (!File.Exists(filePath))
                return null;

            return File.ReadAllBytes(filePath);
        }

        public bool DeleteSaveFile(string filePath)
        {
            if (!File.Exists(filePath))
                return false;

            File.Delete(filePath);
            var infoPath = GetInfoPath(filePath);
            if (File.Exists(infoPath))
                File.Delete(infoPath);
            return true;
        }

        public bool FileExists(string filePath)
        {
            return File.Exists(filePath);
        }
    }
}
