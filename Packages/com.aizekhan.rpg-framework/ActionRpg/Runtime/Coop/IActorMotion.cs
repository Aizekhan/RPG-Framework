namespace RPGFramework.ActionRpg.Coop
{
    /// <summary>
    /// Where characters are, for co-op: rules never move characters, the presentation does, so the co-op layer asks
    /// it. The Unity side reads and steers the character views; tests use a dictionary.
    /// </summary>
    public interface IActorMotion
    {
        /// <returns>False when this character is not shown (yet).</returns>
        bool TryGetPose(int entityId, out ActorPose pose);

        /// <summary>
        /// Where a character driven from another machine should go (it moves there smoothly).
        /// </summary>
        void SetTargetPose(int entityId, ActorPose pose);
    }
}
