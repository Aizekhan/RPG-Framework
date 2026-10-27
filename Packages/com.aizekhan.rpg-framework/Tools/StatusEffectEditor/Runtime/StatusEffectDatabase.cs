using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using RPGFramework.StatusEffects;

namespace RPGFramework.Tools.StatusEffectEditor
{
    /// <summary>
    /// All the status effects of a game in one asset. Checks them together and registers them into a
    /// <see cref="StatusEffectManager"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "StatusEffectDatabase", menuName = "RPG Framework/Status Effect Database", order = 106)]
    public class StatusEffectDatabase : ScriptableObject
    {
        [Tooltip("Every status effect the game can apply")]
        public List<StatusEffectAsset> effects = new List<StatusEffectAsset>();

        /// <summary>
        /// Finds an effect by its id, or null.
        /// </summary>
        public StatusEffectAsset GetEffect(string effectId)
        {
            return effects?.FirstOrDefault(e => e != null && e.effectId == effectId);
        }

        /// <summary>
        /// Converts every effect to its runtime definition, leaving out empty slots.
        /// </summary>
        public List<StatusEffectData> ToDataList()
        {
            return (effects ?? new List<StatusEffectAsset>())
                .Where(e => e != null)
                .Select(e => e.ToData())
                .ToList();
        }

        /// <summary>
        /// Registers every effect into the manager so it can be applied by id. Empty slots are skipped.
        /// The manager keeps the first effect of an id and ignores later ones with the same id.
        /// </summary>
        /// <returns>How many effects were handed to the manager.</returns>
        public int RegisterInto(StatusEffectManager manager)
        {
            if (manager == null)
                throw new System.ArgumentNullException(nameof(manager));

            var data = ToDataList();
            foreach (var effect in data)
                manager.RegisterEffect(effect);

            return data.Count;
        }

        /// <summary>
        /// Lists every problem in the database: no effects, empty slots, ids used twice, and each effect's own problems.
        /// Empty means ready.
        /// </summary>
        public List<string> Validate()
        {
            var errors = new List<string>();

            if (effects == null || effects.Count == 0)
            {
                errors.Add("Database is empty - add status effects");
                return errors;
            }

            int empty = effects.Count(e => e == null);
            if (empty > 0)
                errors.Add($"{empty} empty slot(s) in the database");

            var duplicateIds = effects
                .Where(e => e != null && !string.IsNullOrWhiteSpace(e.effectId))
                .GroupBy(e => e.effectId)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicateIds.Count > 0)
                errors.Add($"Duplicate effect IDs (only the first one is used): {string.Join(", ", duplicateIds)}");

            foreach (var effect in effects.Where(e => e != null))
            {
                var effectErrors = effect.Validate();
                if (effectErrors.Count > 0)
                {
                    errors.Add($"Effect '{effect.effectName}' ({effect.effectId}):");
                    errors.AddRange(effectErrors.Select(e => $"  - {e}"));
                }
            }

            return errors;
        }
    }
}
