using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace RPGFramework.Persistence
{
    /// <summary>
    /// Concrete implementation of a save file.
    /// </summary>
    public class SaveFile : ISaveFile
    {
        public ISaveMetadata Metadata { get; private set; }
        public byte[] Data { get; private set; }
        public string Checksum { get; private set; }

        public SaveFile(ISaveMetadata metadata, byte[] data, string checksum = null)
        {
            Metadata = metadata;
            Data = data;
            Checksum = checksum ?? ComputeChecksum(data);
        }

        public bool ValidateIntegrity()
        {
            if (Data == null || Data.Length == 0)
                return false;

            if (string.IsNullOrEmpty(Checksum))
                return false;

            var computedChecksum = ComputeChecksum(Data);
            return computedChecksum == Checksum;
        }

        private static string ComputeChecksum(byte[] data)
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
