using System;

namespace RPGFramework.Abilities
{
    /// <summary>
    /// Hits that come after an ability goes off (<see cref="CastInfo.ImpactDelay"/>, <see cref="CastInfo.ImpactCount"/>):
    /// the use is paid and its cooldown starts at once, and each hit calls the effect when its time comes
    /// (<see cref="IAbilityExecutor.Update"/>). An ability without them hits at once, as always.
    /// <see cref="AbilityExecutor"/> implements it.
    /// </summary>
    public interface IAbilityImpacts
    {
        /// <summary>
        /// How fast the hits of this use come (caster, ability): 1.5 makes the delay and the interval 1/1.5 as long
        /// (attack speed, haste), 0.5 twice as long. Asked once, when the use goes off. Null, or an answer that is
        /// not above 0: 1.
        /// </summary>
        Func<int, IAbility, float> ImpactSpeedFor { get; set; }

        /// <summary>
        /// The use or hit being handled right now: valid while the executor takes a cost or calls an effect, so the
        /// game can tell which use a payment or a hit belongs to. Default otherwise.
        /// </summary>
        AbilityImpact Current { get; }

        /// <summary>
        /// A use with delayed hits went off (paid, charge used); its hits follow. Raised inside the executor call,
        /// before the ability's own cooldown starts.
        /// </summary>
        event Action<AbilityImpact> UseWentOff;

        /// <summary>
        /// A delayed hit did not land (the reason: interrupted, the caster died, the target was no longer valid).
        /// </summary>
        event Action<AbilityImpact, string> ImpactMissed;

        bool HasPendingImpacts(int entityId);

        /// <summary>
        /// Stops the hits still to come of the entity's uses (<paramref name="interruptibleOnly"/>: only those
        /// without <see cref="CastInfo.ImpactsSurviveInterrupt"/>). Each is reported as missed.
        /// </summary>
        /// <returns>How many were stopped.</returns>
        int CancelImpacts(int entityId, float currentTime, string reason, bool interruptibleOnly = false);
    }
}
