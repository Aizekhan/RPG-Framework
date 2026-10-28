using System;
using System.Collections.Generic;
using RPGFramework.Pooling;
using UnityEngine;

namespace RPGFramework.Audio.Unity
{
    /// <summary>
    /// Sounds an <see cref="AudioLibrary"/> with Unity: each voice on an <see cref="AudioSource"/> from a pool (made
    /// once, reused), music on two sources that crossfade. A channel routed to a mixer group plays through it; a
    /// channel with a mixer volume parameter is turned up and down there, others on each source.
    /// </summary>
    public sealed class UnityAudioOutput : IAudioOutput, IDisposable
    {
        private readonly AudioLibrary _library;
        private readonly ObjectPool<AudioSource> _sources;
        private readonly Dictionary<int, (AudioSource Source, float Volume, string Channel)> _playing =
            new Dictionary<int, (AudioSource, float, string)>();
        private readonly Dictionary<string, float> _channelVolumes = new Dictionary<string, float>(StringComparer.Ordinal);
        private readonly AudioSource[] _music = new AudioSource[2];
        private readonly float[] _musicFrom = new float[2];
        private readonly float[] _musicTo = new float[2];
        private MusicTrack _musicTrack;
        private int _musicCurrent;
        private float _fadeLength;
        private float _fadeTime;

        /// <param name="root">Where the sources live; null makes an "Audio" object kept across scenes.</param>
        public UnityAudioOutput(AudioLibrary library, Transform root = null)
        {
            _library = library != null ? library : throw new ArgumentNullException(nameof(library));
            if (root == null)
            {
                var audio = new GameObject("Audio");
                if (Application.isPlaying)
                    UnityEngine.Object.DontDestroyOnLoad(audio);
                root = audio.transform;
                OwnsRoot = true;
            }
            Root = root;
            for (int i = 0; i < 2; i++)
            {
                _music[i] = NewSource("Music" + i);
                _music[i].loop = true;
                _music[i].spatialBlend = 0f;
                _music[i].outputAudioMixerGroup = _library.Route(AudioChannels.Music)?.Group;
            }
            _sources = new ObjectPool<AudioSource>(() => NewSource("Voice"),
                onGet: source => source.gameObject.SetActive(true),
                onRelease: source =>
                {
                    source.Stop();
                    source.clip = null;
                    source.gameObject.SetActive(false);
                },
                onDestroy: source =>
                {
                    if (source != null)
                        Discard(source.gameObject);
                },
                initialSize: library.PrewarmVoices, maxSize: library.MaxPooledVoices, poolName: "AudioSources");
        }

        public Transform Root { get; }

        private bool OwnsRoot { get; }

        public int Sounding => _playing.Count;

        public float Play(in AudioVoice voice)
        {
            var cue = _library.Cue(voice.CueId);
            var clip = cue != null ? cue.Clip(voice.Variant) : null;
            if (clip == null)
                return 0f;
            var source = _sources.Get();
            source.clip = clip;
            source.loop = false;
            source.pitch = voice.Pitch;
            source.outputAudioMixerGroup = _library.Route(voice.Channel)?.Group;
            source.volume = voice.Volume * Factor(voice.Channel);
            if (voice.Position.HasValue)
            {
                var at = voice.Position.Value;
                source.transform.position = new Vector3(at.X, at.Y, at.Z);
                source.spatialBlend = 1f;
                source.minDistance = cue.MinDistance;
                source.maxDistance = cue.MaxDistance;
            }
            else
            {
                source.transform.localPosition = Vector3.zero;
                source.spatialBlend = 0f;
            }
            source.Play();
            _playing[voice.VoiceId] = (source, voice.Volume, voice.Channel);
            return clip.length;
        }

        public void Stop(int voiceId)
        {
            if (!_playing.TryGetValue(voiceId, out var playing))
                return;
            _playing.Remove(voiceId);
            if (playing.Source != null)
                _sources.Release(playing.Source);
        }

        public bool PlayMusic(string trackId, float fadeSeconds)
        {
            var track = _library.Track(trackId);
            if (track == null || track.Clip == null)
                return false;
            _musicTrack = track;
            int incoming = 1 - _musicCurrent;
            var source = _music[incoming];
            source.clip = track.Clip;
            source.volume = 0f;
            source.Play();
            _musicFrom[incoming] = 0f;
            _musicTo[incoming] = 1f;
            _musicFrom[_musicCurrent] = Level(_musicCurrent);
            _musicTo[_musicCurrent] = 0f;
            _musicCurrent = incoming;
            StartFade(fadeSeconds);
            return true;
        }

        public void StopMusic(float fadeSeconds)
        {
            for (int i = 0; i < 2; i++)
            {
                _musicFrom[i] = Level(i);
                _musicTo[i] = 0f;
            }
            StartFade(fadeSeconds);
        }

        public void SetVolume(string channel, float volume)
        {
            _channelVolumes[channel] = volume;
            var route = _library.Route(channel);
            if (_library.Mixer != null && route != null && !string.IsNullOrEmpty(route.VolumeParameter))
                _library.Mixer.SetFloat(route.VolumeParameter, volume > 0.0001f ? 20f * Mathf.Log10(volume) : -80f);
            foreach (var playing in _playing.Values)
            {
                if (playing.Channel == channel && playing.Source != null)
                    playing.Source.volume = playing.Volume * Factor(channel);
            }
            if (channel == AudioChannels.Music)
                ApplyMusic();
        }

        public void Update(float deltaTime)
        {
            if (_fadeLength <= 0f)
                return;
            _fadeTime = Mathf.Min(_fadeLength, _fadeTime + deltaTime);
            ApplyMusic();
            if (_fadeTime >= _fadeLength)
                EndFade();
        }

        public void Dispose()
        {
            foreach (var playing in _playing.Values)
            {
                if (playing.Source != null)
                    playing.Source.Stop();
            }
            _playing.Clear();
            _sources.Clear();
            if (OwnsRoot && Root != null)
                Discard(Root.gameObject);
        }

        private void StartFade(float seconds)
        {
            _fadeLength = Mathf.Max(0f, seconds);
            _fadeTime = 0f;
            if (_fadeLength <= 0f)
                EndFade();
            else
                ApplyMusic();
        }

        private void EndFade()
        {
            _fadeLength = 0f;
            for (int i = 0; i < 2; i++)
            {
                _musicFrom[i] = _musicTo[i];
                if (_musicTo[i] <= 0f && _music[i].isPlaying)
                {
                    _music[i].Stop();
                    _music[i].clip = null;
                }
            }
            ApplyMusic();
        }

        // 0..1 of a music source, before the track's and the channel's volume
        private float Level(int index)
        {
            if (_fadeLength <= 0f)
                return _musicFrom[index];
            return Mathf.Lerp(_musicFrom[index], _musicTo[index], _fadeTime / _fadeLength);
        }

        private void ApplyMusic()
        {
            float track = _musicTrack != null ? _musicTrack.Volume : 1f;
            for (int i = 0; i < 2; i++)
                _music[i].volume = Level(i) * track * Factor(AudioChannels.Music);
        }

        // The channel's volume on the source, unless the mixer does it
        private float Factor(string channel)
        {
            var route = _library.Route(channel);
            if (_library.Mixer != null && route != null && !string.IsNullOrEmpty(route.VolumeParameter))
                return 1f;
            return _channelVolumes.TryGetValue(channel ?? "", out float volume) ? volume : 1f;
        }

        private AudioSource NewSource(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(Root, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            if (name == "Voice")
                go.SetActive(false);
            return source;
        }

        private static void Discard(GameObject go)
        {
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(go);
            else
                UnityEngine.Object.DestroyImmediate(go);
        }
    }
}
