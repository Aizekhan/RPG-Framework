using System;
using RPGFramework.Audio;
using RPGFramework.Events;
using RPGFramework.Interaction;
using RPGFramework.Progression;
using RPGFramework.Trading;

namespace RPGFramework.ActionRpg
{
    /// <summary>
    /// Cues the action RPG kit plays by itself when the host's <c>AudioLibrary</c> has them: a hit and a kill at the
    /// character, a heal, a level up, a finished trade, a used place. Each machine plays them from the events on its
    /// own bus (a friend's machine gets hits, kills, heals and uses from the host). Cues the library does not have
    /// stay silent; the game plays the rest itself with <see cref="IAudioService.Play"/> or <c>On</c>.
    /// </summary>
    public static class ActionRpgSounds
    {
        public const string Hit = "kit.hit";
        public const string CriticalHit = "kit.hit.critical";
        public const string Killed = "kit.killed";
        public const string Healed = "kit.heal";
        public const string LevelUp = "kit.level_up";
        public const string Trade = "kit.trade";
        public const string Interact = "kit.interact";

        /// <summary>
        /// Plays the kit's cues the service has.
        /// </summary>
        /// <param name="at">Where a character is now (null when it has no view).</param>
        /// <returns>Dispose to stop.</returns>
        public static IDisposable Wire(IAudioService audio, Func<int, AudioPosition?> at)
        {
            if (audio == null)
                throw new ArgumentNullException(nameof(audio));
            at = at ?? (_ => null);
            var bag = new SubscriptionBag();
            if (audio.Has(Hit) || audio.Has(CriticalHit))
            {
                audio.On<ActorDamagedEvent>(e => e.Context.IsCritical && audio.Has(CriticalHit) ? CriticalHit : audio.Has(Hit) ? Hit : null,
                    e => at(e.TargetId)).AddTo(bag);
            }
            if (audio.Has(Killed))
                audio.On<ActorKilledEvent>(Killed, e => at(e.EntityId)).AddTo(bag);
            if (audio.Has(Healed))
                audio.On<ActorHealedEvent>(Healed, e => at(e.EntityId), e => e.Amount > 0f).AddTo(bag);
            if (audio.Has(LevelUp))
                audio.On<LevelUpEvent>(LevelUp).AddTo(bag);
            if (audio.Has(Trade))
                audio.On<TradeCompletedEvent>(Trade).AddTo(bag);
            if (audio.Has(Interact))
                audio.On<InteractedEvent>(Interact, e => at(e.PlayerId)).AddTo(bag);
            return bag;
        }
    }
}
