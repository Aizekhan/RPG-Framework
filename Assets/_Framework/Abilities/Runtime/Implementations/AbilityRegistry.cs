using System.Collections.Generic;
using System.Linq;

namespace RPGFramework.Abilities
{
    /// <summary>
    /// Default implementation of IAbilityRegistry.
    /// Manages registration and lookup of ability definitions.
    /// </summary>
    public class AbilityRegistry : IAbilityRegistry
    {
        private readonly Dictionary<string, IAbility> _abilities = new Dictionary<string, IAbility>();

        public void RegisterAbility(IAbility ability)
        {
            if (ability == null)
                return;

            _abilities[ability.AbilityId] = ability;
        }

        public bool UnregisterAbility(string abilityId)
        {
            return _abilities.Remove(abilityId);
        }

        public IAbility GetAbility(string abilityId)
        {
            return _abilities.TryGetValue(abilityId, out var ability) ? ability : null;
        }

        public bool HasAbility(string abilityId)
        {
            return _abilities.ContainsKey(abilityId);
        }

        public IReadOnlyList<IAbility> GetAllAbilities()
        {
            return _abilities.Values.ToList();
        }

        public IReadOnlyList<IAbility> GetAbilitiesByType(AbilityType type)
        {
            return _abilities.Values.Where(a => a.Type == type).ToList();
        }

        public void Clear()
        {
            _abilities.Clear();
        }
    }
}
