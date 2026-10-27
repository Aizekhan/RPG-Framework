using System.IO;
using RPGFramework.Networking;

namespace RPGFramework.ActionRpg.Coop
{
    /// <summary>
    /// Host → joined machines: the game is now in this scene (a hub, a run, the next floor); load it too. Sent when
    /// the host changes scene and to each player who joins. The party, the world and the link stay as they are.
    /// </summary>
    public sealed class CoopSceneMessage : NetworkMessage, INetworkPayload
    {
        public override int MessageTypeId => CoopProtocol.SceneId;
        public string Scene = "";

        public void Write(BinaryWriter writer) => writer.Write(Scene ?? "");

        public void Read(BinaryReader reader) => Scene = reader.ReadString();
    }
}
