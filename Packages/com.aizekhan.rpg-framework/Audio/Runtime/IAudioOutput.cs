using System.Collections.Generic;

namespace RPGFramework.Audio
{
    /// <summary>
    /// What actually makes the sound: the engine (<c>UnityAudioOutput</c>) or a recorder in tests
    /// (<see cref="RecordingAudioOutput"/>). <see cref="IAudioService"/> decides what plays, when and how loud.
    /// </summary>
    public interface IAudioOutput
    {
        /// <summary>
        /// Starts a voice.
        /// </summary>
        /// <returns>Seconds it lasts at pitch 1 (the clip's length); 0 or less when it cannot play (no such clip).</returns>
        float Play(in AudioVoice voice);

        /// <summary>
        /// The voice is over (it ran out or was stopped): its source can be reused.
        /// </summary>
        void Stop(int voiceId);

        /// <summary>
        /// Fades the music playing now out and the track in over <paramref name="fadeSeconds"/>.
        /// </summary>
        /// <returns>False when there is no such track.</returns>
        bool PlayMusic(string trackId, float fadeSeconds);

        void StopMusic(float fadeSeconds);

        /// <summary>
        /// How loud a channel is now, master included (0..1).
        /// </summary>
        void SetVolume(string channel, float volume);

        /// <summary>
        /// Lets fades move on (seconds of real time).
        /// </summary>
        void Update(float deltaTime);
    }

    /// <summary>
    /// Where volumes are kept between runs (Unity's PlayerPrefs, the game's settings file).
    /// </summary>
    public interface IVolumeStore
    {
        bool TryLoad(string channel, out float volume);
        void Save(string channel, float volume);
    }

    /// <summary>
    /// Volumes kept only while the game runs.
    /// </summary>
    public sealed class MemoryVolumeStore : IVolumeStore
    {
        private readonly Dictionary<string, float> _volumes = new Dictionary<string, float>();

        public bool TryLoad(string channel, out float volume) => _volumes.TryGetValue(channel ?? "", out volume);

        public void Save(string channel, float volume) => _volumes[channel ?? ""] = volume;
    }
}
