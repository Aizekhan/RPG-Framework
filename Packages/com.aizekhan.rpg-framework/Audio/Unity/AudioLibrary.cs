using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace RPGFramework.Audio.Unity
{
    /// <summary>
    /// A music track of an <see cref="AudioLibrary"/> (it loops).
    /// </summary>
    [Serializable]
    public sealed class MusicTrack
    {
        public string Id = "";
        public AudioClip Clip;
        [Range(0f, 1f)] public float Volume = 1f;
    }

    /// <summary>
    /// Where a volume channel goes in an <see cref="AudioMixer"/>: its group and the exposed parameter (in dB) its
    /// volume sets. Without one the channel's volume is applied to each sound.
    /// </summary>
    [Serializable]
    public sealed class AudioChannelRoute
    {
        public string Channel = AudioChannels.Effects;
        public AudioMixerGroup Group;

        [Tooltip("Exposed mixer parameter for the channel's volume (dB); empty: the volume is applied to each sound")]
        public string VolumeParameter = "";
    }

    /// <summary>
    /// The game's sounds: cues, music tracks and, optionally, a mixer for the volume channels. Give it to
    /// <see cref="AudioModule"/> (or to the action RPG kit's host).
    /// </summary>
    [CreateAssetMenu(menuName = "RPG Framework/Audio/Library", fileName = "AudioLibrary")]
    public sealed class AudioLibrary : ScriptableObject
    {
        [SerializeField] private List<AudioCue> _cues = new List<AudioCue>();
        [SerializeField] private List<MusicTrack> _music = new List<MusicTrack>();
        [SerializeField] private AudioMixer _mixer;
        [SerializeField] private List<AudioChannelRoute> _channels = new List<AudioChannelRoute>();
        [SerializeField, Min(0), Tooltip("Sources made at start, so the first sounds do not create any")] private int _prewarmVoices = 8;
        [SerializeField, Min(1), Tooltip("Sources kept for reuse at most")] private int _maxPooledVoices = 64;

        public IReadOnlyList<AudioCue> Cues => _cues;
        public IReadOnlyList<MusicTrack> Music => _music;
        public AudioMixer Mixer => _mixer;
        public IReadOnlyList<AudioChannelRoute> Channels => _channels;
        public int PrewarmVoices => _prewarmVoices;
        public int MaxPooledVoices => Mathf.Max(1, _maxPooledVoices);

        /// <returns>The cue with the id, or null.</returns>
        public AudioCue Cue(string id)
        {
            foreach (var cue in _cues)
            {
                if (cue != null && cue.Id == id)
                    return cue;
            }
            return null;
        }

        /// <returns>The track with the id, or null.</returns>
        public MusicTrack Track(string id)
        {
            foreach (var track in _music)
            {
                if (track != null && track.Id == id)
                    return track;
            }
            return null;
        }

        /// <returns>The channel's route, or null.</returns>
        public AudioChannelRoute Route(string channel)
        {
            foreach (var route in _channels)
            {
                if (route != null && route.Channel == channel)
                    return route;
            }
            return null;
        }

        /// <summary>
        /// For code and tests: a library made without the inspector.
        /// </summary>
        public static AudioLibrary Create(IEnumerable<AudioCue> cues, IEnumerable<MusicTrack> music = null)
        {
            var library = CreateInstance<AudioLibrary>();
            library.name = "AudioLibrary";
            if (cues != null)
                library._cues.AddRange(cues);
            if (music != null)
                library._music.AddRange(music);
            return library;
        }
    }
}
