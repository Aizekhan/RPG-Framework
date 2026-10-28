using System;
using System.Collections.Generic;
using RPGFramework.Events;

namespace RPGFramework.Audio
{
    /// <summary>
    /// The game's sound: cues played on what happens (from code with <see cref="Play"/>, or from bus events with
    /// <see cref="On{TEvent}(string, Func{TEvent, AudioPosition?}, Func{TEvent, bool})"/>), music with crossfades, and
    /// volume channels kept between runs. It decides what plays, when and how loud; the output makes the sound.
    /// In co-op every machine plays its own sounds from the events and states it receives: nothing goes over the
    /// network.
    /// </summary>
    public interface IAudioService
    {
        /// <summary>
        /// Adds a cue, or replaces the one with its id.
        /// </summary>
        void Register(AudioCueData cue);

        bool Has(string cueId);

        /// <returns>The cue, or null.</returns>
        AudioCueData Cue(string cueId);

        IEnumerable<AudioCueData> Cues { get; }

        /// <summary>
        /// Plays a cue: from a point in the world, or flat without one (and always flat for a cue that is not spatial).
        /// </summary>
        /// <returns>False when it did not play: no such cue, too many of it sounding, too soon after the last one,
        /// or no clip.</returns>
        bool Play(string cueId, AudioPosition? position = null);

        /// <summary>
        /// How many of a cue are sounding now.
        /// </summary>
        int Voices(string cueId);

        /// <summary>
        /// Silences every cue sounding now (music goes on).
        /// </summary>
        void StopAll();

        /// <summary>
        /// The track playing now; null when none.
        /// </summary>
        string Music { get; }

        /// <summary>
        /// Crossfades to a track; the track already playing goes on untouched.
        /// </summary>
        /// <returns>False when there is no such track.</returns>
        bool PlayMusic(string trackId, float fadeSeconds = 1f);

        void StopMusic(float fadeSeconds = 1f);

        /// <summary>
        /// A channel's own volume (0..1; 1 until set), without master.
        /// </summary>
        float GetVolume(string channel);

        /// <summary>
        /// Sets a channel's volume (0..1) and keeps it for the next run. <see cref="AudioChannels.Master"/> turns every
        /// channel down.
        /// </summary>
        void SetVolume(string channel, float volume);

        /// <summary>
        /// Plays a cue each time an event is published (on this machine's bus).
        /// </summary>
        /// <param name="at">Where it sounds (null: flat); null plays it flat.</param>
        /// <param name="when">Only for events this says yes to; null: every one.</param>
        /// <returns>Dispose to stop.</returns>
        IDisposable On<TEvent>(string cueId, Func<TEvent, AudioPosition?> at = null, Func<TEvent, bool> when = null)
            where TEvent : IEvent;

        /// <summary>
        /// Plays the cue an event calls for ("strike.hit" or "strike.weak" by damage); a null or empty id plays nothing.
        /// </summary>
        IDisposable On<TEvent>(Func<TEvent, string> cueOf, Func<TEvent, AudioPosition?> at = null)
            where TEvent : IEvent;

        /// <summary>
        /// Real time passes (sounds end, fades move); not stopped by a paused game.
        /// </summary>
        void Update(float deltaTime);
    }
}
