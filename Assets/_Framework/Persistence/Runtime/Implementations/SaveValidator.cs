using System.Linq;
using System.Security.Cryptography;

namespace RPGFramework.Persistence
{
    /// <summary>
    /// Validates save file integrity and compatibility.
    /// </summary>
    public class SaveValidator : ISaveValidator
    {
        private readonly string _currentGameVersion;

        public SaveValidator(string currentGameVersion)
        {
            _currentGameVersion = currentGameVersion;
        }

        public bool ValidateIntegrity(ISaveFile saveFile)
        {
            if (saveFile == null)
                return false;

            if (saveFile.Data == null || saveFile.Data.Length == 0)
                return false;

            if (string.IsNullOrEmpty(saveFile.Checksum))
                return false;

            var computedChecksum = ComputeChecksum(saveFile.Data);
            return computedChecksum == saveFile.Checksum;
        }

        public bool IsCompatible(ISaveFile saveFile)
        {
            if (saveFile?.Metadata == null)
                return false;

            // Simple version comparison - in production, use semantic versioning
            return saveFile.Metadata.GameVersion == _currentGameVersion;
        }

        public bool TryRepair(ISaveFile saveFile, out ISaveFile repairedFile)
        {
            repairedFile = null;

            if (saveFile == null || saveFile.Data == null)
                return false;

            // Attempt to recompute checksum
            var newChecksum = ComputeChecksum(saveFile.Data);
            repairedFile = new SaveFile(saveFile.Metadata, saveFile.Data, newChecksum);

            return repairedFile.ValidateIntegrity();
        }

        public string ComputeChecksum(byte[] data)
        {
            if (data == null || data.Length == 0)
                return string.Empty;

            using (var sha256 = SHA256.Create())
            {
                var hash = sha256.ComputeHash(data);
                return string.Concat(hash.Select(b => b.ToString("x2")));
            }
        }
    }
}
