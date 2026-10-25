using System;

namespace RPGFramework.Companions.Runtime
{
    /// <summary>
    /// Default implementation of ICompanionAbility.
    /// Represents a special ability that a companion can use.
    /// </summary>
    public class CompanionAbility : ICompanionAbility
    {
        private float _remainingCooldown;

        public string AbilityId { get; }
        public string Name { get; }
        public string Description { get; }
        public float Cooldown { get; }
        public float RemainingCooldown => _remainingCooldown;
        public bool IsOnCooldown => _remainingCooldown > 0;
        public int RequiredLevel { get; }
        public bool IsPassive { get; }

        // Delegate for the ability effect
        private readonly Func<ICompanion, int, bool> _abilityEffect;

        public CompanionAbility(
            string abilityId,
            string name,
            string description,
            float cooldown,
            int requiredLevel,
            bool isPassive,
            Func<ICompanion, int, bool> abilityEffect)
        {
            AbilityId = abilityId ?? throw new ArgumentNullException(nameof(abilityId));
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Description = description ?? string.Empty;
            Cooldown = cooldown;
            RequiredLevel = requiredLevel;
            IsPassive = isPassive;
            _abilityEffect = abilityEffect ?? throw new ArgumentNullException(nameof(abilityEffect));
            _remainingCooldown = 0;
        }

        public bool CanUse()
        {
            return !IsOnCooldown;
        }

        public bool Use(ICompanion companion, int targetEntityId = -1)
        {
            if (companion == null)
                throw new ArgumentNullException(nameof(companion));

            if (IsPassive)
                return false; // Passive abilities cannot be manually used

            if (!CanUse())
                return false;

            if (companion.Level < RequiredLevel)
                return false;

            // Execute the ability effect
            bool success = _abilityEffect(companion, targetEntityId);

            if (success)
            {
                _remainingCooldown = Cooldown;
            }

            return success;
        }

        public void UpdateCooldown(float deltaTime)
        {
            if (_remainingCooldown > 0)
            {
                _remainingCooldown = Math.Max(0, _remainingCooldown - deltaTime);
            }
        }

        public void ResetCooldown()
        {
            _remainingCooldown = 0;
        }

        public void SetCooldown(float cooldown)
        {
            _remainingCooldown = cooldown;
        }
    }
}
