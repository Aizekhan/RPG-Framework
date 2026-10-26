namespace RPGFramework.Persistence
{
    /// <summary>
    /// A system whose state is part of a saved game (inventory, quests, world flags...). Registered with
    /// <see cref="SaveGameCoordinator"/>, which stores each participant in its own section under <see cref="SaveKey"/>.
    /// Implement <see cref="SaveParticipant{TState}"/> rather than this interface directly.
    /// </summary>
    public interface ISaveParticipant
    {
        /// <summary>
        /// Stable section name; renaming it orphans existing saves.
        /// </summary>
        string SaveKey { get; }

        /// <summary>
        /// The shape of this section's state, stored with every save. Raise it when old saves cannot simply be read as
        /// the new shape (a member renamed, retyped or split) and teach <see cref="Read"/> to upgrade them. Adding a
        /// member or removing one needs no new version: missing members keep their defaults, unknown ones are skipped.
        /// </summary>
        int Version { get; }

        byte[] Capture(ISaveSerializer serializer);

        /// <summary>
        /// Deserialises a section without applying it, so a load can fail before any system has changed.
        /// </summary>
        /// <param name="savedVersion">The <see cref="Version"/> the section was saved with (1 for saves older than
        /// section versions); never above the current one.</param>
        object Read(byte[] data, int savedVersion, ISaveSerializer serializer);

        void Restore(object state);
    }

    /// <summary>
    /// Typed <see cref="ISaveParticipant"/>: capture the system's state into a plain serialisable object and apply it back.
    /// When the state type changes in a way old saves cannot be read as, raise <see cref="Version"/> and override
    /// <see cref="Upgrade"/>: read the old shape (a copy of the old class) and convert it, one step per version.
    /// </summary>
    public abstract class SaveParticipant<TState> : ISaveParticipant
    {
        public abstract string SaveKey { get; }

        public virtual int Version => 1;

        protected abstract TState CaptureState();

        protected abstract void RestoreState(TState state);

        /// <summary>
        /// Reads a section saved with an older <see cref="Version"/>. By default it is read as the current type (new
        /// members keep their defaults, removed ones are skipped).
        /// </summary>
        /// <example><code>
        /// public override int Version => 2;
        /// protected override Progress Upgrade(int savedVersion, byte[] data, ISaveSerializer serializer)
        /// {
        ///     var old = serializer.Deserialize&lt;ProgressV1&gt;(data);   // Gold was an int, now Coins is a long
        ///     return new Progress { Coins = old.Gold, Level = old.Level };
        /// }
        /// </code></example>
        protected virtual TState Upgrade(int savedVersion, byte[] data, ISaveSerializer serializer) =>
            serializer.Deserialize<TState>(data);

        byte[] ISaveParticipant.Capture(ISaveSerializer serializer) => serializer.Serialize(CaptureState());

        object ISaveParticipant.Read(byte[] data, int savedVersion, ISaveSerializer serializer) =>
            savedVersion == Version ? serializer.Deserialize<TState>(data) : Upgrade(savedVersion, data, serializer);

        void ISaveParticipant.Restore(object state) => RestoreState((TState)state);
    }
}
