using System.Collections.Generic;

namespace RPGFramework.Audio
{
    /// <summary>
    /// An output that plays nothing and remembers everything: for tests, servers and machines without sound.
    /// Every clip lasts <see cref="ClipLength"/> seconds.
    /// </summary>
    public sealed class RecordingAudioOutput : IAudioOutput
    {
        private readonly Dictionary<string, float> _volumes = new Dictionary<string, float>();
        private readonly HashSet<int> _sounding = new HashSet<int>();

        public float ClipLength { get; set; } = 1f;

        /// <summary>
        /// Tracks that exist; null: every track does.
        /// </summary>
        public HashSet<string> Tracks { get; set; }

        public List<AudioVoice> Played { get; } = new List<AudioVoice>();
        public List<int> Stopped { get; } = new List<int>();

        /// <summary>
        /// The track playing now; null when none.
        /// </summary>
        public string Music { get; private set; }

        public float LastFade { get; private set; }

        /// <summary>
        /// Voices that started and have not stopped.
        /// </summary>
        public int Sounding => _sounding.Count;

        public float VolumeOf(string channel) => _volumes.TryGetValue(channel, out float volume) ? volume : 1f;

        public float Play(in AudioVoice voice)
        {
            Played.Add(voice);
            _sounding.Add(voice.VoiceId);
            return ClipLength;
        }

        public void Stop(int voiceId)
        {
            if (_sounding.Remove(voiceId))
                Stopped.Add(voiceId);
        }

        public bool PlayMusic(string trackId, float fadeSeconds)
        {
            if (Tracks != null && !Tracks.Contains(trackId))
                return false;
            Music = trackId;
            LastFade = fadeSeconds;
            return true;
        }

        public void StopMusic(float fadeSeconds)
        {
            Music = null;
            LastFade = fadeSeconds;
        }

        public void SetVolume(string channel, float volume) => _volumes[channel] = volume;

        public void Update(float deltaTime)
        {
        }
    }
}
