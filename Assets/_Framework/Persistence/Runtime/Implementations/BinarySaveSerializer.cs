using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace RPGFramework.Persistence
{
    /// <summary>
    /// Compact binary save serializer: a "RPGB" header and format version followed by
    /// a GZip-compressed JSON payload.
    /// Unlike BinaryFormatter, the payload never carries type information, so loading a
    /// tampered save cannot instantiate arbitrary types; only members of T are populated.
    /// </summary>
    public class BinarySaveSerializer : ISaveSerializer
    {
        private static readonly byte[] Magic = { (byte)'R', (byte)'P', (byte)'G', (byte)'B' };
        private const byte FormatVersion = 1;

        public SerializationFormat Format => SerializationFormat.Binary;

        public byte[] Serialize<T>(T data)
        {
            try
            {
                var payload = Encoding.UTF8.GetBytes(SimpleJsonSerializer.Serialize(data));

                using (var memoryStream = new MemoryStream())
                {
                    memoryStream.Write(Magic, 0, Magic.Length);
                    memoryStream.WriteByte(FormatVersion);

                    using (var gzip = new GZipStream(memoryStream, CompressionMode.Compress, leaveOpen: true))
                    {
                        gzip.Write(payload, 0, payload.Length);
                    }

                    return memoryStream.ToArray();
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to serialize data: {ex.Message}", ex);
            }
        }

        public T Deserialize<T>(byte[] data)
        {
            try
            {
                if (data == null || data.Length < Magic.Length + 1)
                    throw new FormatException("Data is too short to be a binary save");

                for (int i = 0; i < Magic.Length; i++)
                {
                    if (data[i] != Magic[i])
                        throw new FormatException("Missing binary save header");
                }

                var version = data[Magic.Length];
                if (version != FormatVersion)
                    throw new FormatException($"Unsupported binary save version {version}");

                using (var input = new MemoryStream(data, Magic.Length + 1, data.Length - Magic.Length - 1))
                using (var gzip = new GZipStream(input, CompressionMode.Decompress))
                using (var output = new MemoryStream())
                {
                    gzip.CopyTo(output);
                    return SimpleJsonSerializer.Deserialize<T>(Encoding.UTF8.GetString(output.ToArray()));
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to deserialize data: {ex.Message}", ex);
            }
        }
    }
}
