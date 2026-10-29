namespace RPGFramework.Abilities
{
    /// <summary>
    /// Instance of an ability for a specific entity.
    /// Tracks per-entity state (cooldowns, charges, casting).
    /// </summary>
    public interface IAbilityInstance
    {
        /// <summary>Ability definition</summary>
        IAbility Ability { get; }

        /// <summary>Entity ID that owns this instance</summary>
        int OwnerEntityId { get; }

        /// <summary>Current cooldown state</summary>
        CooldownState CooldownState { get; }

        /// <summary>Remaining charges (for abilities with charges, -1 for unlimited)</summary>
        int RemainingCharges { get; }

        /// <summary>Whether ability is currently being cast</summary>
        bool IsCasting { get; }

        /// <summary>Cast start time (for tracking cast progress)</summary>
        float CastStartTime { get; }

        /// <summary>Whether ability can be used right now</summary>
        bool CanUse(float currentTime);

        /// <summary>Start casting (for abilities with cast time)</summary>
        void StartCast(float currentTime);

        /// <summary>Cancel casting</summary>
        void CancelCast();

        /// <summary>Complete casting and start cooldown</summary>
        void CompleteCast(float currentTime);

        /// <summary>Use one charge (for charge-based abilities)</summary>
        void UseCharge();

        /// <summary>Restore one charge</summary>
        void RestoreCharge();

        /// <summary>
        /// Gives back the charges whose recharge time has passed (<see cref="AbilityMetadata.ChargeRechargeTime"/>).
        /// </summary>
        void RefreshCharges(float currentTime);

        /// <summary>
        /// Seconds until the next charge comes back; 0 when none is on its way.
        /// </summary>
        float NextChargeIn(float currentTime);
    }
}
