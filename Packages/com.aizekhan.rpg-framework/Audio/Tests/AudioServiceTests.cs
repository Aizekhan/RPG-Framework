using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RPGFramework.Audio.Unity;
using RPGFramework.Bootstrap;
using RPGFramework.Events;
using RPGFramework.Logging;
using UnityEngine;
using Logger = RPGFramework.Logging.Logger;

namespace RPGFramework.Audio.Tests
{
    /// <summary>
    /// The audio service: cues pick a variant (not the same twice running) with a volume and pitch spread, stay under
    /// their voice limit and interval, play from a point or flat, follow bus events, crossfade music, and keep
    /// channel volumes; the Unity output plays on pooled sources; the module wires it to the host.
    /// </summary>
    public sealed class AudioServiceTests
    {
        private struct CoinPicked : IEvent
        {
            public int Amount;
            public float X;
        }

        private RecordingAudioOutput _output;
        private EventBus _bus;
        private MemoryVolumeStore _store;
        private AudioService _audio;
        private readonly List<Object> _cleanup = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            _output = new RecordingAudioOutput { ClipLength = 1f };
            _bus = new EventBus();
            _store = new MemoryVolumeStore();
            _audio = new AudioService(_output, _bus, _store, seed: 3);
        }

        [TearDown]
        public void TearDown()
        {
            _audio.Dispose();
            _bus.Dispose();
            foreach (var obj in _cleanup)
            {
                if (obj != null)
                    Object.DestroyImmediate(obj);
            }
            _cleanup.Clear();
        }

        [Test]
        public void ACue_PicksAnotherVariantEachTime_WithinItsSpread()
        {
            _audio.Register(new AudioCueData("strike.hit", variants: 3)
            {
                Volume = 0.8f, VolumeSpread = 0.25f, PitchMin = 0.9f, PitchMax = 1.1f
            });

            for (int i = 0; i < 30; i++)
                Assert.That(_audio.Play("strike.hit"), Is.True);

            var played = _output.Played;
            for (int i = 1; i < played.Count; i++)
                Assert.That(played[i].Variant, Is.Not.EqualTo(played[i - 1].Variant), "never the same one twice running");
            Assert.That(played.Select(p => p.Variant).Distinct().Count(), Is.EqualTo(3));
            Assert.That(played.All(p => p.Volume >= 0.6f - 1e-4f && p.Volume <= 0.8f + 1e-4f), Is.True);
            Assert.That(played.All(p => p.Pitch >= 0.9f && p.Pitch <= 1.1f), Is.True);
            Assert.That(_audio.Play("nothing"), Is.False, "no such cue");
        }

        [Test]
        public void OftenPlayedCues_StayUnderTheirVoicesAndInterval()
        {
            _audio.Register(new AudioCueData("coin") { MaxVoices = 2 });
            _audio.Register(new AudioCueData("step") { MinInterval = 0.25f });

            Assert.That(_audio.Play("coin"), Is.True);
            Assert.That(_audio.Play("coin"), Is.True);
            Assert.That(_audio.Play("coin"), Is.False, "two already sound");
            Assert.That(_audio.Voices("coin"), Is.EqualTo(2));
            _audio.Update(1.01f);
            Assert.That(_audio.Voices("coin"), Is.Zero, "they ran out");
            Assert.That(_output.Sounding, Is.Zero, "and their sources were given back");
            Assert.That(_audio.Play("coin"), Is.True);

            Assert.That(_audio.Play("step"), Is.True);
            _audio.Update(0.1f);
            Assert.That(_audio.Play("step"), Is.False, "too soon");
            _audio.Update(0.2f);
            Assert.That(_audio.Play("step"), Is.True);

            _audio.StopAll();
            Assert.That(_output.Sounding, Is.Zero);
            Assert.That(_audio.Voices("coin"), Is.Zero);
        }

        [Test]
        public void AVoiceLasts_ItsClipAtItsPitch()
        {
            _audio.Register(new AudioCueData("low") { PitchMin = 0.5f, PitchMax = 0.5f, MaxVoices = 1 });
            Assert.That(_audio.Play("low"), Is.True);
            _audio.Update(1.5f);
            Assert.That(_audio.Voices("low"), Is.EqualTo(1), "half the pitch: twice as long");
            _audio.Update(0.6f);
            Assert.That(_audio.Voices("low"), Is.Zero);
        }

        [Test]
        public void SpatialCues_SoundFromWhereTheyArePlayed_FlatOnesFromNowhere()
        {
            _audio.Register(new AudioCueData("monster.killed"));
            _audio.Register(new AudioCueData("ui.click") { Spatial = false, Channel = AudioChannels.Interface });

            _audio.Play("monster.killed", new AudioPosition(1f, 2f, 3f));
            _audio.Play("monster.killed");
            _audio.Play("ui.click", new AudioPosition(5f, 5f, 5f));

            Assert.That(_output.Played[0].Position, Is.EqualTo(new AudioPosition(1f, 2f, 3f)));
            Assert.That(_output.Played[1].Position, Is.Null, "no point: flat");
            Assert.That(_output.Played[2].Position, Is.Null, "a flat cue stays flat");
            Assert.That(_output.Played[2].Channel, Is.EqualTo(AudioChannels.Interface));
        }

        [Test]
        public void BusEvents_PlayTheirCues()
        {
            _audio.Register(new AudioCueData("coin"));
            _audio.Register(new AudioCueData("coin.big"));
            var small = _audio.On<CoinPicked>("coin", e => new AudioPosition(e.X, 0f, 0f), e => e.Amount < 10);
            _audio.On<CoinPicked>(e => e.Amount >= 10 ? "coin.big" : null);

            _bus.Publish(new CoinPicked { Amount = 1, X = 4f });
            _bus.Publish(new CoinPicked { Amount = 50 });
            Assert.That(_output.Played.Select(p => p.CueId), Is.EqualTo(new[] { "coin", "coin.big" }));
            Assert.That(_output.Played[0].Position.Value.X, Is.EqualTo(4f));

            small.Dispose();
            _bus.Publish(new CoinPicked { Amount = 1 });
            Assert.That(_output.Played, Has.Count.EqualTo(2), "stopped listening");
        }

        [Test]
        public void Music_Crossfades_AndATrackAlreadyPlayingGoesOn()
        {
            _output.Tracks = new HashSet<string> { "camp", "boss" };

            Assert.That(_audio.PlayMusic("camp", 2f), Is.True);
            Assert.That(_audio.Music, Is.EqualTo("camp"));
            Assert.That(_output.LastFade, Is.EqualTo(2f));
            Assert.That(_audio.PlayMusic("camp", 0.5f), Is.True);
            Assert.That(_output.LastFade, Is.EqualTo(2f), "not started again");
            Assert.That(_audio.PlayMusic("night"), Is.False, "no such track");
            Assert.That(_audio.Music, Is.EqualTo("camp"));

            _audio.PlayMusic("boss", 1f);
            Assert.That(_output.Music, Is.EqualTo("boss"));
            _audio.StopMusic(3f);
            Assert.That(_audio.Music, Is.Null);
            Assert.That(_output.Music, Is.Null);
        }

        [Test]
        public void Volumes_TurnChannelsDown_ThroughMaster_AndAreKept()
        {
            _audio.SetVolume(AudioChannels.Effects, 0.5f);
            _audio.SetVolume(AudioChannels.Master, 0.5f);
            Assert.That(_output.VolumeOf(AudioChannels.Effects), Is.EqualTo(0.25f));
            Assert.That(_output.VolumeOf(AudioChannels.Music), Is.EqualTo(0.5f));
            Assert.That(_audio.GetVolume(AudioChannels.Effects), Is.EqualTo(0.5f), "its own, without master");
            _audio.SetVolume("voice", 2f);
            Assert.That(_audio.GetVolume("voice"), Is.EqualTo(1f), "0..1");

            var next = new RecordingAudioOutput();
            using (var nextRun = new AudioService(next, store: _store))
            {
                Assert.That(nextRun.GetVolume(AudioChannels.Master), Is.EqualTo(0.5f), "kept for the next run");
                Assert.That(next.VolumeOf(AudioChannels.Effects), Is.EqualTo(0.25f));
            }
        }

        [Test]
        public void TheUnityOutput_PlaysOnPooledSources_AndGivesThemBack()
        {
            var clips = new[] { Clip("a"), Clip("b") };
            var hit = AudioCue.Create("hit", clips, maxVoices: 0);
            var library = AudioLibrary.Create(new[] { hit }, new[] { new MusicTrack { Id = "camp", Clip = Clip("camp") } });
            _cleanup.Add(hit);
            _cleanup.Add(library);
            var output = new UnityAudioOutput(library);
            _cleanup.Add(output.Root.gameObject);
            var audio = new AudioService(output, store: new MemoryVolumeStore(), seed: 1);
            audio.Register(hit.ToData());

            Assert.That(audio.Play("hit", new AudioPosition(1f, 0f, 0f)), Is.True);
            Assert.That(audio.Play("hit"), Is.True);
            var sources = output.Root.GetComponentsInChildren<AudioSource>(true);
            Assert.That(output.Sounding, Is.EqualTo(2));
            var spatial = sources.Single(s => s.gameObject.activeSelf && s.spatialBlend == 1f);
            Assert.That(spatial.transform.position.x, Is.EqualTo(1f));

            audio.Update(5f);
            Assert.That(output.Sounding, Is.Zero);
            int made = output.Root.GetComponentsInChildren<AudioSource>(true).Length;
            audio.Play("hit");
            audio.Play("hit");
            Assert.That(output.Root.GetComponentsInChildren<AudioSource>(true).Length, Is.EqualTo(made), "reused, none made");

            audio.SetVolume(AudioChannels.Effects, 0.5f);
            Assert.That(output.Root.GetComponentsInChildren<AudioSource>().Where(s => s.clip != null && s.name == "Voice")
                .All(s => s.volume <= 0.5f + 1e-4f), Is.True, "a sounding voice follows its channel");

            Assert.That(audio.PlayMusic("camp", 0f), Is.True);
            Assert.That(output.Root.GetComponentsInChildren<AudioSource>(true).Count(s => s.loop && s.clip != null), Is.EqualTo(1));
            Assert.That(audio.PlayMusic("night"), Is.False);
            output.Dispose();
        }

        [Test]
        public void TheModule_GivesTheHostAServiceWithTheLibrarysCues()
        {
            var cue = AudioCue.Create("level.up", new[] { Clip("up") }, spatial: false);
            var library = AudioLibrary.Create(new[] { cue });
            _cleanup.Add(cue);
            _cleanup.Add(library);
            var output = new RecordingAudioOutput();
            using (var logger = new Logger(LogSeverity.Warning, new MemoryLogSink()))
            using (var host = new GameHost(logger))
            {
                host.Start(b => b.UseModule(new AudioModule(library, output, new MemoryVolumeStore())));
                var audio = host.Container.Resolve<IAudioService>();
                Assert.That(audio.Has("level.up"), Is.True);
                Assert.That(audio.Cue("level.up").Spatial, Is.False);
                audio.On<CoinPicked>("level.up");
                host.Container.Resolve<IEventBus>().Publish(new CoinPicked());
                Assert.That(output.Played.Select(p => p.CueId), Is.EqualTo(new[] { "level.up" }), "on the host's bus");
            }
        }

        private AudioClip Clip(string name)
        {
            var clip = AudioClip.Create(name, 4410, 1, 44100, false);
            _cleanup.Add(clip);
            return clip;
        }
    }
}
