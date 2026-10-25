using System;

namespace RPGFramework.Abilities
{
    /// <summary>
    /// Default implementation of ITargetValidator.
    /// Validates targets for ability execution using callback pattern.
    /// </summary>
    public class TargetValidator : ITargetValidator
    {
        public bool ValidateTarget(
            IAbility ability,
            int casterEntityId,
            int targetEntityId,
            Func<int, int, float> distanceCheck,
            Func<int, int, bool> relationshipCheck)
        {
            if (ability == null)
                return false;

            // Validate based on target type
            switch (ability.TargetType)
            {
                case TargetType.None:
                    // No target required
                    return true;

                case TargetType.Self:
                    // Must target self
                    return targetEntityId == casterEntityId || targetEntityId == -1;

                case TargetType.Enemy:
                    // Must target enemy
                    if (targetEntityId == -1 || targetEntityId == casterEntityId)
                        return false;

                    // Check if target is enemy using callback
                    if (relationshipCheck != null && !relationshipCheck(casterEntityId, targetEntityId))
                        return false;

                    break;

                case TargetType.Ally:
                    // Must target ally (or self)
                    if (targetEntityId == -1)
                        return false;

                    // Self is always valid ally
                    if (targetEntityId == casterEntityId)
                        return true;

                    // Check if target is ally using callback (opposite of enemy)
                    if (relationshipCheck != null && relationshipCheck(casterEntityId, targetEntityId))
                        return false;

                    break;

                case TargetType.Any:
                    // Any valid entity
                    if (targetEntityId == -1)
                        return false;
                    break;

                case TargetType.Area:
                case TargetType.Ground:
                    // AoE and ground targeting always valid (position validation is game-specific)
                    return true;
            }

            // Check range if specified
            if (ability.Range > 0f && targetEntityId != -1 && targetEntityId != casterEntityId)
            {
                if (distanceCheck == null)
                    return false;

                float distance = distanceCheck(casterEntityId, targetEntityId);
                if (distance > ability.Range)
                    return false;
            }

            return true;
        }
    }
}
