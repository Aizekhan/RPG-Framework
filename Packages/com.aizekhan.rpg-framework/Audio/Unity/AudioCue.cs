using System;
using System.Collections.Generic;
using UnityEngine;

namespace RPGFramework.Audio.Unity
{
    /// <summary>
    /// A sound the game plays on something happening, as an asset: its clips (one picked each time), volume and
    /// pitch spread, 2D or 3D, how many may sound at once and how often. Put it in an <see cref="AudioLibrary"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "RPG Framework/Audio/Cue", fileName = "AudioCue")]
    public sealed class AudioCue : ScriptableObject
    {
        [SerializeField, Tooltip("The id the game plays it by; empty uses the asset's name")] private string _id = "";
        [SerializeField] private AudioClip[] _clips = Array.Empty<AudioClip>();
        [SerializeField, Range(0f, 1f)] private float _volume = 1f;
        [SerializeField, Range(0f, 1f), Tooltip("How much quieter a play may be at random")] private float _volumeSpread;
        [SerializeField, Range(0.1f, 3f)] private float _pitchMin = 1f;
        [SerializeField, Range(0.1f, 3f)] private float _pitchMax = 1f;
        [SerializeField, Tooltip("Heard from where it is played; off: flat (UI)")] private bool _spatial = true;
        [SerializeField, Min(0), Tooltip("How many may sound at once; 0: no limit")] private int _maxVoices = 4;
        [SerializeField, Min(0f), Tooltip("Seconds between two plays")] private float _minInterval;
        [SerializeField, Tooltip("Volume channel: sfx, ui, music or one of the game's")] private string _channel = AudioChannels.Effects;
        [SerializeField, Min(0.01f)] private float _minDistance = 1f;
        [SerializeField, Min(0.01f)] private float _maxDistance = 30f;

        public string Id => string.IsNullOrEmpty(_id) ? name : _id;
        public IReadOnlyList<AudioClip> Clips => _clips ?? Array.Empty<AudioClip>();
        public float MinDistance => _minDistance;
        public float MaxDistance => Mathf.Max(_minDistance, _maxDistance);

        /// <returns>The clip of a variant, or null.</returns>
        public AudioClip Clip(int variant) =>
            _clips != null && variant >= 0 && variant < _clips.Length ? _clips[variant] : null;

        public AudioCueData ToData() => new AudioCueData(Id, Clips.Count)
        {
            Volume = _volume,
            VolumeSpread = _volumeSpread,
            PitchMin = _pitchMin,
            PitchMax = _pitchMax,
            Spatial = _spatial,
            MaxVoices = _maxVoices,
            MinInterval = _minInterval,
            Channel = string.IsNullOrEmpty(_channel) ? AudioChannels.Effects : _channel
        };

        /// <summary>
        /// For code and tests: a cue made without the inspector.
        /// </summary>
        public static AudioCue Create(string id, AudioClip[] clips, bool spatial = true, int maxVoices = 4,
            float minInterval = 0f, string channel = AudioChannels.Effects)
        {
            var cue = CreateInstance<AudioCue>();
            cue.name = id;
            cue._id = id;
            cue._clips = clips ?? Array.Empty<AudioClip>();
            cue._spatial = spatial;
            cue._maxVoices = maxVoices;
            cue._minInterval = minInterval;
            cue._channel = channel;
            return cue;
        }
    }
}
