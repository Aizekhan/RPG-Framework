using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Unity.Networking.Transport.Relay;

namespace RPGFramework.Networking.UnityRelay
{
    /// <summary>
    /// The cloud side of a relayed game: rooms with a short code and relay allocations. The real one is
    /// <see cref="MultiplayerRelayService"/> (Unity Multiplayer Services); tests use a fake. Failures are thrown as
    /// <see cref="NetworkSessionException"/> with a message for the player.
    /// </summary>
    public interface IRelayService
    {
        /// <summary>
        /// What is missing before rooms can be opened (the project is not linked to Unity Gaming Services), or null.
        /// </summary>
        string SetupProblem { get; }

        /// <summary>
        /// Opens a room. While opening, the service hands over the host's relay data through
        /// <paramref name="startNetwork"/>, which opens the game's transport.
        /// </summary>
        /// <returns>The code friends type to join.</returns>
        UniTask<string> CreateAsync(int maxPlayers, string name, Func<RelayServerData, UniTask> startNetwork,
            CancellationToken cancellation);

        /// <summary>
        /// Joins the room with this code, handing this player's relay data to <paramref name="startNetwork"/>.
        /// </summary>
        UniTask JoinAsync(string code, Func<RelayServerData, UniTask> startNetwork, CancellationToken cancellation);

        /// <summary>
        /// Leaves the room (a host closes it for everyone).
        /// </summary>
        UniTask LeaveAsync();
    }
}
