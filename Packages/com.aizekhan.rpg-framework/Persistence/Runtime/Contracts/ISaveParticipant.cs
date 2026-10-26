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

        byte[] Capture(ISaveSerializer serializer);

        /// <summary>
        /// Deserialises a section without applying it, so a load can fail before any system has changed.
        /// </summary>
        object Read(byte[] data, ISaveSerializer serializer);

        void Restore(object state);
    }

    /// <summary>
    /// Typed <see cref="ISaveParticipant"/>: capture the system's state into a plain serialisable object and apply it back.
    /// </summary>
    public abstract class SaveParticipant<TState> : ISaveParticipant
    {
        public abstract string SaveKey { get; }

        protected abstract TState CaptureState();

        protected abstract void RestoreState(TState state);

        byte[] ISaveParticipant.Capture(ISaveSerializer serializer) => serializer.Serialize(CaptureState());

        object ISaveParticipant.Read(byte[] data, ISaveSerializer serializer) => serializer.Deserialize<TState>(data);

        void ISaveParticipant.Restore(object state) => RestoreState((TState)state);
    }
}
