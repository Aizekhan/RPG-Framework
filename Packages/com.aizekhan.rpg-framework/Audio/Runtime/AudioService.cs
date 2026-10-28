using System;
using System.Collections.Generic;
using RPGFramework.Events;

namespace RPGFramework.Audio
{
    /// <summary>
    /// Default <see cref="IAudioService"/>: picks a cue's variant (never the same twice in a row), its volume and pitch,
    /// keeps its voices under <see cref="AudioCueData.MaxVoices"/> and <see cref="AudioCueData.MinInterval"/>, counts
    /// how long each voice sounds with its own clock, and keeps channel volumes in an <see cref="IVolumeStore"/>.
    /// </summary>
    public sealed class AudioService : IAudioService, IDisposable
    {
        private readonly IAudioOutput _output;
        private readonly IEventBus _bus;
        private readonly IVolumeStore _store;
        private readonly Random _random;
        private readonly Dictionary<string, AudioCueData> _cues = new Dictionary<string, AudioCueData>(StringComparer.Ordinal);
        private readonly Dictionary<string, float> _volumes = new Dictionary<string, float>(StringComparer.Ordinal);
        private readonly HashSet<string> _channels = new HashSet<string>(StringComparer.Ordinal)
        {
            AudioChannels.Music, AudioChannels.Effects, AudioChannels.Interface
        };
        private readonly Dictionary<string, double> _lastPlayed = new Dictionary<string, double>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _lastVariant = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _voiceCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly List<(int Id, string Cue, double End)> _voices = new List<(int, string, double)>();
        private readonly List<IDisposable> _subscriptions = new List<IDisposable>();
        private double _clock;
        private int _nextVoice;

        /// <param name="output">What makes the sound.</param>
        /// <param name="bus">The bus <c>On</c> listens to; null: <c>On</c> cannot be used.</param>
        /// <param name="store">Where volumes are kept; null: only while the game runs.</param>
        /// <param name="seed">For variants, volume and pitch; null: different each run.</param>
        public AudioService(IAudioOutput output, IEventBus bus = null, IVolumeStore store = null, int? seed = null)
        {
            _output = output ?? throw new ArgumentNullException(nameof(output));
            _bus = bus;
            _store = store ?? new MemoryVolumeStore();
            _random = seed.HasValue ? new Random(seed.Value) : new Random();
            foreach (string channel in _channels)
                Push(channel);
        }

        public IEnumerable<AudioCueData> Cues => _cues.Values;

        public string Music { get; private set; }

        public void Register(AudioCueData cue)
        {
            if (cue == null)
                throw new ArgumentNullException(nameof(cue));
            _cues[cue.Id] = cue;
            string channel = ChannelOf(cue);
            if (_channels.Add(channel))
                Push(channel);
        }

        public bool Has(string cueId) => cueId != null && _cues.ContainsKey(cueId);

        public AudioCueData Cue(string cueId) => cueId != null && _cues.TryGetValue(cueId, out var cue) ? cue : null;

        public bool Play(string cueId, AudioPosition? position = null)
        {
            var cue = Cue(cueId);
            if (cue == null)
                return false;
            if (cue.MinInterval > 0f && _lastPlayed.TryGetValue(cue.Id, out double last) && _clock - last < cue.MinInterval)
                return false;
            if (cue.MaxVoices > 0 && Voices(cue.Id) >= cue.MaxVoices)
                return false;

            int variant = PickVariant(cue);
            float spread = Clamp01(cue.VolumeSpread);
            float volume = Clamp01(cue.Volume) * (1f - spread * (float)_random.NextDouble());
            float low = Math.Max(0.01f, Math.Min(cue.PitchMin, cue.PitchMax));
            float high = Math.Max(0.01f, Math.Max(cue.PitchMin, cue.PitchMax));
            float pitch = low + (high - low) * (float)_random.NextDouble();
            var voice = new AudioVoice(++_nextVoice, cue.Id, variant, volume, pitch, cue.Spatial ? position : null, ChannelOf(cue));

            float length = _output.Play(voice);
            if (length <= 0f)
                return false;
            _lastPlayed[cue.Id] = _clock;
            _lastVariant[cue.Id] = variant;
            _voices.Add((voice.VoiceId, cue.Id, _clock + length / pitch));
            _voiceCounts[cue.Id] = Voices(cue.Id) + 1;
            return true;
        }

        public int Voices(string cueId) => cueId != null && _voiceCounts.TryGetValue(cueId, out int count) ? count : 0;

        public void StopAll()
        {
            foreach (var voice in _voices)
                _output.Stop(voice.Id);
            _voices.Clear();
            _voiceCounts.Clear();
        }

        public bool PlayMusic(string trackId, float fadeSeconds = 1f)
        {
            if (string.IsNullOrEmpty(trackId))
                return false;
            if (trackId == Music)
                return true;
            if (!_output.PlayMusic(trackId, Math.Max(0f, fadeSeconds)))
                return false;
            Music = trackId;
            return true;
        }

        public void StopMusic(float fadeSeconds = 1f)
        {
            if (Music == null)
                return;
            _output.StopMusic(Math.Max(0f, fadeSeconds));
            Music = null;
        }

        public float GetVolume(string channel)
        {
            channel = channel ?? AudioChannels.Master;
            if (_volumes.TryGetValue(channel, out float volume))
                return volume;
            volume = _store.TryLoad(channel, out float saved) && !float.IsNaN(saved) ? Clamp01(saved) : 1f;
            _volumes[channel] = volume;
            return volume;
        }

        public void SetVolume(string channel, float volume)
        {
            channel = channel ?? AudioChannels.Master;
            if (float.IsNaN(volume))
                throw new ArgumentException("A volume must be a number", nameof(volume));
            volume = Clamp01(volume);
            _volumes[channel] = volume;
            _store.Save(channel, volume);
            if (channel == AudioChannels.Master)
            {
                foreach (string each in _channels)
                    Push(each);
            }
            else
            {
                _channels.Add(channel);
                Push(channel);
            }
        }

        public IDisposable On<TEvent>(string cueId, Func<TEvent, AudioPosition?> at = null, Func<TEvent, bool> when = null)
            where TEvent : IEvent
        {
            if (string.IsNullOrEmpty(cueId))
                throw new ArgumentException("A cue id is needed", nameof(cueId));
            return Listen<TEvent>(e =>
            {
                if (when == null || when(e))
                    Play(cueId, at?.Invoke(e));
            });
        }

        public IDisposable On<TEvent>(Func<TEvent, string> cueOf, Func<TEvent, AudioPosition?> at = null)
            where TEvent : IEvent
        {
            if (cueOf == null)
                throw new ArgumentNullException(nameof(cueOf));
            return Listen<TEvent>(e =>
            {
                string cueId = cueOf(e);
                if (!string.IsNullOrEmpty(cueId))
                    Play(cueId, at?.Invoke(e));
            });
        }

        public void Update(float deltaTime)
        {
            if (deltaTime > 0f)
                _clock += deltaTime;
            _output.Update(Math.Max(0f, deltaTime));
            for (int i = _voices.Count - 1; i >= 0; i--)
            {
                var voice = _voices[i];
                if (voice.End > _clock)
                    continue;
                _voices.RemoveAt(i);
                _voiceCounts[voice.Cue] = Math.Max(0, Voices(voice.Cue) - 1);
                _output.Stop(voice.Id);
            }
        }

        public void Dispose()
        {
            foreach (var subscription in _subscriptions)
                subscription.Dispose();
            _subscriptions.Clear();
            StopAll();
        }

        private IDisposable Listen<TEvent>(Action<TEvent> play) where TEvent : IEvent
        {
            if (_bus == null)
                throw new InvalidOperationException("This audio service was made without an event bus");
            var subscription = _bus.Subscribe(play, receiveSticky: false);
            _subscriptions.Add(subscription);
            return subscription;
        }

        private int PickVariant(AudioCueData cue)
        {
            if (cue.Variants <= 1)
                return 0;
            if (!_lastVariant.TryGetValue(cue.Id, out int last))
                return _random.Next(cue.Variants);
            // Any other than the last one
            int pick = _random.Next(cue.Variants - 1);
            return pick >= last ? pick + 1 : pick;
        }

        private void Push(string channel) =>
            _output.SetVolume(channel, GetVolume(channel) * GetVolume(AudioChannels.Master));

        private static string ChannelOf(AudioCueData cue) =>
            string.IsNullOrEmpty(cue.Channel) ? AudioChannels.Effects : cue.Channel;

        private static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;
    }
}
