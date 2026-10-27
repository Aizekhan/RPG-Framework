using UnityEngine;
using RPGFramework.Abilities;

namespace RPGFramework.Tools.AbilityEditor
{
    /// <summary>
    /// One thing an ability takes from the caster when used, e.g. 30 mana.
    /// </summary>
    [System.Serializable]
    public class AbilityCostEntry
    {
        [Tooltip("Resource the game takes, e.g. 'MP', 'HP' or 'Stamina'")]
        public string resourceType = "MP";

        [Tooltip("How much of it is taken")]
        [Min(0f)]
        public float amount = 10f;

        [Tooltip("Whether the amount is a share of the caster's maximum (your game decides how) rather than a flat number")]
        public bool isPercentage = false;

        /// <summary>
        /// The cost as the ability system stores it.
        /// </summary>
        public ResourceCost ToResourceCost()
        {
            return new ResourceCost(resourceType, amount, isPercentage);
        }
    }
}
