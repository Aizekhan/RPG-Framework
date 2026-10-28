using System;

namespace RPGFramework.Audio
{
    /// <summary>
    /// Names of the volume channels the framework knows. A game may use channels of its own ("voice", "ambience");
    /// every channel is also turned down by <see cref="Master"/>.
    /// </summary>
    public static class AudioChannels
    {
        public const string Master = "master";
        public const string Music = "music";
        public const string Effects = "sfx";
        public const string Interface = "ui";
    }

    /// <summary>
    /// A point in the world a sound comes from (the engine's units). Sounds without one are flat (2D).
    /// </summary>
    public readonly struct AudioPosition : IEquatable<AudioPosition>
    {
        public AudioPosition(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public float X { get; }
        public float Y { get; }
        public float Z { get; }

        public bool Equals(AudioPosition other) => X == other.X && Y == other.Y && Z == other.Z;
        public override bool Equals(object obj) => obj is AudioPosition other && Equals(other);
        public override int GetHashCode() => (X, Y, Z).GetHashCode();
        public override string ToString() => $"({X}, {Y}, {Z})";
        public static bool operator ==(AudioPosition a, AudioPosition b) => a.Equals(b);
        public static bool operator !=(AudioPosition a, AudioPosition b) => !a.Equals(b);
    }

    /// <summary>
    /// A sound the game plays on something happening ("strike.hit", "coin", "ui.click"): which of its
    /// <see cref="Variants"/> plays is picked each time, with a spread of volume and pitch so repeats do not sound
    /// the same; <see cref="MaxVoices"/> and <see cref="MinInterval"/> keep 20 coins from piling up. The clips
    /// themselves belong to the engine side (<c>AudioCue</c> asset).
    /// </summary>
    public sealed class AudioCueData
    {
        public AudioCueData(string id, int variants = 1)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentException("A cue needs an id", nameof(id));
            Id = id;
            Variants = Math.Max(1, variants);
        }

        public string Id { get; }

        /// <summary>
        /// How many clips the cue has; one is picked each time (never the same one twice in a row when there are more).
        /// </summary>
        public int Variants { get; }

        /// <summary>
        /// 0..1.
        /// </summary>
        public float Volume { get; set; } = 1f;

        /// <summary>
        /// How much quieter a play may be at random (0.1: 90%..100% of <see cref="Volume"/>).
        /// </summary>
        public float VolumeSpread { get; set; }

        public float PitchMin { get; set; } = 1f;
        public float PitchMax { get; set; } = 1f;

        /// <summary>
        /// Heard from where it is played (a position given to Play); false: flat, wherever it is played from.
        /// </summary>
        public bool Spatial { get; set; } = true;

        /// <summary>
        /// How many of this cue may sound at once; a play beyond it is skipped. 0: no limit.
        /// </summary>
        public int MaxVoices { get; set; }

        /// <summary>
        /// Seconds that must pass between two plays of this cue; a play sooner is skipped.
        /// </summary>
        public float MinInterval { get; set; }

        /// <summary>
        /// The volume channel it goes through (<see cref="AudioChannels.Effects"/> by default).
        /// </summary>
        public string Channel { get; set; } = AudioChannels.Effects;
    }

    /// <summary>
    /// One play of a cue, as the service decided it, for the output to sound.
    /// </summary>
    public readonly struct AudioVoice
    {
        public AudioVoice(int voiceId, string cueId, int variant, float volume, float pitch, AudioPosition? position, string channel)
        {
            VoiceId = voiceId;
            CueId = cueId;
            Variant = variant;
            Volume = volume;
            Pitch = pitch;
            Position = position;
            Channel = channel;
        }

        public int VoiceId { get; }
        public string CueId { get; }
        public int Variant { get; }

        /// <summary>
        /// The cue's volume for this play; the channel's volume is applied by the output (<see cref="IAudioOutput.SetVolume"/>).
        /// </summary>
        public float Volume { get; }

        public float Pitch { get; }

        /// <summary>
        /// Null: flat (2D).
        /// </summary>
        public AudioPosition? Position { get; }

        public string Channel { get; }
    }
}
