using System.IO;

namespace RPGFramework.Networking
{
    /// <summary>
    /// A game's own message that writes and reads its fields. Derive from <see cref="NetworkMessage"/>, implement this,
    /// give it an id from <see cref="MessageType.GameSpecificStart"/> up and register it with
    /// <see cref="NetworkSerializer.RegisterMessageType{T}"/> on both sides. It needs a parameterless constructor.
    /// </summary>
    public interface INetworkPayload
    {
        void Write(BinaryWriter writer);

        void Read(BinaryReader reader);
    }
}
