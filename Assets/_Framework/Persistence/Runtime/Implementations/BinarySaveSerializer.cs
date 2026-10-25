using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace RPGFramework.Persistence
{
    /// <summary>
    /// Binary save serializer using BinaryFormatter.
    /// Note: For production use, consider more modern serializers like MessagePack or Protobuf.
    /// </summary>
    public class BinarySaveSerializer : ISaveSerializer
    {
        public SerializationFormat Format => SerializationFormat.Binary;

#pragma warning disable SYSLIB0011 // BinaryFormatter is obsolete
        private readonly BinaryFormatter _formatter;

        public BinarySaveSerializer()
        {
            _formatter = new BinaryFormatter();
        }

        public byte[] Serialize<T>(T data)
        {
            try
            {
                using (var memoryStream = new MemoryStream())
                {
                    _formatter.Serialize(memoryStream, data);
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
                using (var memoryStream = new MemoryStream(data))
                {
                    var obj = _formatter.Deserialize(memoryStream);
                    return (T)obj;
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to deserialize data: {ex.Message}", ex);
            }
        }
#pragma warning restore SYSLIB0011
    }
}
