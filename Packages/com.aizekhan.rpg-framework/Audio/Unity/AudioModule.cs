using System;
using RPGFramework.Bootstrap;
using RPGFramework.Events;
using UnityEngine;

namespace RPGFramework.Audio.Unity
{
    /// <summary>
    /// Keeps volumes in Unity's PlayerPrefs ("RPGFramework.Audio.&lt;channel&gt;").
    /// </summary>
    public sealed class PlayerPrefsVolumeStore : IVolumeStore
    {
        public const string Prefix = "RPGFramework.Audio.";

        public bool TryLoad(string channel, out float volume)
        {
            string key = Prefix + channel;
            volume = PlayerPrefs.GetFloat(key, 1f);
            return PlayerPrefs.HasKey(key);
        }

        public void Save(string channel, float volume)
        {
            PlayerPrefs.SetFloat(Prefix + channel, volume);
            PlayerPrefs.Save();
        }
    }

    /// <summary>
    /// Unity points for <see cref="IAudioService"/>.
    /// </summary>
    public static class AudioUnityExtensions
    {
        public static AudioPosition ToAudio(this Vector3 position) => new AudioPosition(position.x, position.y, position.z);

        /// <summary>
        /// Plays a cue from a point in the world.
        /// </summary>
        public static bool Play(this IAudioService audio, string cueId, Vector3 position) =>
            audio != null && audio.Play(cueId, position.ToAudio());

        /// <summary>
        /// Plays a cue from where a transform is now; null plays it flat.
        /// </summary>
        public static bool Play(this IAudioService audio, string cueId, Transform from) =>
            audio != null && audio.Play(cueId, from != null ? from.position.ToAudio() : (AudioPosition?)null);
    }

    /// <summary>
    /// Registers <see cref="IAudioService"/> with the cues of an <see cref="AudioLibrary"/>, sounded by
    /// <see cref="UnityAudioOutput"/> (pooled sources, crossfading music), volumes kept in PlayerPrefs. Events for
    /// <c>On</c> come from the host's bus. A service registered before this module configures is kept. Tests and
    /// servers pass an output of their own (<see cref="RecordingAudioOutput"/>) and drive
    /// <see cref="IAudioService.Update"/> themselves.
    /// </summary>
    public sealed class AudioModule : GameFeatureModule
    {
        public const string ModuleName = "Framework.Audio";

        private readonly AudioLibrary _library;
        private readonly IAudioOutput _output;
        private readonly IVolumeStore _store;
        private AudioService _service;
        private UnityAudioOutput _unityOutput;

        /// <param name="library">The game's sounds; null: no cues until the game registers some.</param>
        /// <param name="output">Null: Unity's (needs a library).</param>
        /// <param name="store">Null: PlayerPrefs.</param>
        public AudioModule(AudioLibrary library, IAudioOutput output = null, IVolumeStore store = null)
        {
            _library = library;
            _output = output;
            _store = store;
        }

        public override string Name => ModuleName;

        protected override void OnConfigure()
        {
            if (!Container.IsRegistered<IAudioService>())
                Container.RegisterFactory<IAudioService>(c => Create(c.Resolve<IEventBus>()));
        }

        protected override void OnShutdown()
        {
            _service?.Dispose();
            _unityOutput?.Dispose();
            _service = null;
            _unityOutput = null;
        }

        private IAudioService Create(IEventBus bus)
        {
            var output = _output;
            if (output == null)
            {
                _unityOutput = new UnityAudioOutput(_library != null ? _library : AudioLibrary.Create(null));
                output = _unityOutput;
            }
            _service = new AudioService(output, bus, _store ?? new PlayerPrefsVolumeStore());
            if (_library != null)
            {
                foreach (var cue in _library.Cues)
                {
                    if (cue != null)
                        _service.Register(cue.ToData());
                }
            }
            if (_unityOutput != null)
                _unityOutput.Root.gameObject.AddComponent<AudioDriver>().Tick = _service.Update;
            return _service;
        }
    }
}
