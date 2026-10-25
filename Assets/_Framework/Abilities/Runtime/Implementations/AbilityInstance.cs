namespace RPGFramework.Abilities
{
    /// <summary>
    /// Default implementation of IAbilityInstance.
    /// Represents an ability instance for a specific entity with state tracking.
    /// </summary>
    public class AbilityInstance : IAbilityInstance
    {
        public IAbility Ability { get; }
        public int OwnerEntityId { get; }
        public CooldownState CooldownState { get; private set; }
        public int RemainingCharges { get; private set; }
        public bool IsCasting { get; private set; }
        public float CastStartTime { get; private set; }

        public AbilityInstance(IAbility ability, int ownerEntityId)
        {
            Ability = ability;
            OwnerEntityId = ownerEntityId;
            RemainingCharges = ability.Metadata.MaxCharges > 0 ? ability.Metadata.MaxCharges : -1;
            IsCasting = false;
            CastStartTime = 0f;
            CooldownState = new CooldownState(ability.AbilityId, 0f, 0f);
        }

        public bool CanUse(float currentTime)
        {
            // Check cooldown
            if (CooldownState.IsOnCooldown(currentTime))
                return false;

            // Check charges
            if (RemainingCharges == 0)
                return false;

            // Check if already casting
            if (IsCasting)
                return false;

            return true;
        }

        public void StartCast(float currentTime)
        {
            IsCasting = true;
            CastStartTime = currentTime;
        }

        public void CancelCast()
        {
            IsCasting = false;
            CastStartTime = 0f;
        }

        public void CompleteCast(float currentTime)
        {
            IsCasting = false;
            CastStartTime = 0f;

            // Start cooldown
            CooldownState = new CooldownState(Ability.AbilityId, Ability.Cooldown, currentTime);

            // Use charge if applicable
            if (RemainingCharges > 0)
            {
                RemainingCharges--;
            }
        }

        public void UseCharge()
        {
            if (RemainingCharges > 0)
                RemainingCharges--;
        }

        public void RestoreCharge()
        {
            if (Ability.Metadata.MaxCharges > 0 && RemainingCharges < Ability.Metadata.MaxCharges)
                RemainingCharges++;
        }
    }
}
