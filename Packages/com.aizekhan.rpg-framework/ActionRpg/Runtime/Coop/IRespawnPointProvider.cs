namespace RPGFramework.ActionRpg.Coop
{
    /// <summary>
    /// Why a player is being put back into the world.
    /// </summary>
    public enum RespawnReason
    {
        /// <summary>
        /// They came back after their link was lost.
        /// </summary>
        Rejoin = 1,

        /// <summary>
        /// They asked to be revived after a fall.
        /// </summary>
        Revive = 2
    }

    /// <summary>
    /// Where a player comes back into the world (the last checkpoint, the start of their branch). Implemented by the
    /// game and given to <see cref="CoopHost.RespawnPoints"/>; without one a player comes back where the game puts
    /// new players.
    /// </summary>
    public interface IRespawnPointProvider
    {
        /// <param name="playerId">The player as they are now (a rejoining player has a new id).</param>
        /// <param name="seat">Their seat in the party (kept across a rejoin).</param>
        /// <param name="lastPose">Where they were when the link was lost or they fell, when known.</param>
        /// <param name="pose">Where to put them.</param>
        /// <returns>False to leave them where the game puts them anyway.</returns>
        bool TryGetRespawnPoint(int playerId, int seat, RespawnReason reason, ActorPose? lastPose, out ActorPose pose);
    }
}
