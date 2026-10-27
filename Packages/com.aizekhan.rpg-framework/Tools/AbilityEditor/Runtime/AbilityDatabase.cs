using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using RPGFramework.Abilities;

namespace RPGFramework.Tools.AbilityEditor
{
    /// <summary>
    /// The list of all ability assets in a game. <see cref="ToAbilityRegistry"/> builds the registry the ability
    /// executor looks abilities up in.
    /// </summary>
    [CreateAssetMenu(fileName = "AbilityDatabase", menuName = "RPG Framework/Ability Database", order = 106)]
    public class AbilityDatabase : ScriptableObject
    {
        [Tooltip("All ability assets the game knows")]
        public List<AbilityAsset> abilities = new List<AbilityAsset>();

        /// <summary>
        /// Finds an ability asset by its id, or null.
        /// </summary>
        public AbilityAsset GetAbility(string abilityId)
        {
            return abilities?.FirstOrDefault(a => a != null && a.abilityId == abilityId);
        }

        /// <summary>
        /// Puts every ability into the given registry. Empty slots are skipped.
        /// </summary>
        public void RegisterInto(IAbilityRegistry registry)
        {
            if (registry == null || abilities == null)
                return;

            foreach (var asset in abilities)
            {
                if (asset != null)
                    registry.RegisterAbility(asset.ToAbility());
            }
        }

        /// <summary>
        /// Builds a new registry holding every ability in this database.
        /// </summary>
        public IAbilityRegistry ToAbilityRegistry()
        {
            var registry = new AbilityRegistry();
            RegisterInto(registry);
            return registry;
        }

        /// <summary>
        /// Lists what is wrong with the database and its abilities in plain words; empty when it is ready to use.
        /// </summary>
        public List<string> Validate()
        {
            var errors = new List<string>();

            if (abilities == null || abilities.Count == 0)
            {
                errors.Add("Database is empty - no abilities added");
                return errors;
            }

            int emptySlots = abilities.Count(a => a == null);
            if (emptySlots > 0)
                errors.Add($"{emptySlots} empty slot(s) in database");

            // The registry keeps only the last ability with a given id, so a duplicate silently replaces another
            var duplicateIds = abilities
                .Where(a => a != null && !string.IsNullOrWhiteSpace(a.abilityId))
                .GroupBy(a => a.abilityId)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicateIds.Count > 0)
                errors.Add($"Duplicate ability IDs: {string.Join(", ", duplicateIds)}");

            foreach (var asset in abilities.Where(a => a != null))
            {
                var abilityErrors = asset.Validate();
                if (abilityErrors.Count > 0)
                {
                    errors.Add($"Ability '{asset.abilityName}' ({asset.abilityId}):");
                    errors.AddRange(abilityErrors.Select(e => $"  - {e}"));
                }
            }

            return errors;
        }

        /// <summary>
        /// Removes empty slots from the list.
        /// </summary>
        public void RemoveEmptySlots()
        {
            abilities?.RemoveAll(a => a == null);
        }
    }
}
