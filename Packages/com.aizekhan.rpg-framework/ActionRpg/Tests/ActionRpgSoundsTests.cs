using System.Linq;
using NUnit.Framework;
using RPGFramework.Audio;
using RPGFramework.Combat;
using RPGFramework.Events;

namespace RPGFramework.ActionRpg.Tests
{
    /// <summary>
    /// The kit's own cues: those the library has play on the kit's events, at the character; the rest stay silent.
    /// </summary>
    public sealed class ActionRpgSoundsTests
    {
        [Test]
        public void TheKitsCues_PlayOnItsEvents_AtTheCharacter()
        {
            var output = new RecordingAudioOutput();
            using (var bus = new EventBus())
            using (var audio = new AudioService(output, bus, seed: 1))
            {
                audio.Register(new AudioCueData(ActionRpgSounds.Hit));
                audio.Register(new AudioCueData(ActionRpgSounds.CriticalHit));
                audio.Register(new AudioCueData(ActionRpgSounds.Killed));
                var sounds = ActionRpgSounds.Wire(audio, id => id == 7 ? new AudioPosition(7f, 0f, 0f) : (AudioPosition?)null);

                bus.Publish(new ActorDamagedEvent(7, 1, 5f, 10f));
                bus.Publish(new ActorDamagedEvent(7, 1, 9f, 1f, AttackContext.Effect(1).AsCritical(2f)));
                bus.Publish(new ActorKilledEvent(7, "goblin", 1, true));
                bus.Publish(new ActorHealedEvent(7, 5f, 15f));

                Assert.That(output.Played.Select(p => p.CueId),
                    Is.EqualTo(new[] { ActionRpgSounds.Hit, ActionRpgSounds.CriticalHit, ActionRpgSounds.Killed }),
                    "no heal cue in the library: silent");
                Assert.That(output.Played.All(p => p.Position == new AudioPosition(7f, 0f, 0f)), Is.True);

                sounds.Dispose();
                bus.Publish(new ActorKilledEvent(7, "goblin", 1, true));
                Assert.That(output.Played, Has.Count.EqualTo(3));
            }
        }
    }
}
