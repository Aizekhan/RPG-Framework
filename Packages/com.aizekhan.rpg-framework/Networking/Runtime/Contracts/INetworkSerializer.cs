using System;

namespace RPGFramework.Networking
{
    /// <summary>
    /// Serializes and deserializes network messages.
    /// </summary>
    public interface INetworkSerializer
    {
        /// <summary>
        /// Serialize a message to bytes.
        /// </summary>
        byte[] Serialize<T>(T message) where T : INetworkMessage;

        /// <summary>
        /// Deserialize bytes to a message.
        /// </summary>
        T Deserialize<T>(byte[] data) where T : INetworkMessage;

        /// <summary>
        /// Deserialize bytes to a message of unknown type.
        /// Returns the message type ID and the deserialized object.
        /// </summary>
        (int messageTypeId, object message) Deserialize(byte[] data);
    }
}
