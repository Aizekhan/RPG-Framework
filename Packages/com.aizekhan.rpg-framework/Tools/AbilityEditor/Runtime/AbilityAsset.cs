using System.Collections.Generic;
using UnityEngine;
using RPGFramework.Abilities;

namespace RPGFramework.Tools.AbilityEditor
{
    /// <summary>
    /// An ability a designer fills in the inspector. <see cref="ToAbility"/> turns it into the ability the game runs
    /// (register it in an ability registry, or put it in an <see cref="AbilityDatabase"/>). What the ability does when
    /// it lands is the game's own code: the ability executor calls the game back with the ability id.
    /// </summary>
    [CreateAssetMenu(fileName = "NewAbility", menuName = "RPG Framework/Ability", order = 105)]
    public class AbilityAsset : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Unique id the game uses to find and use this ability, e.g. 'fireball'")]
        public string abilityId = "ability_new";

        [Tooltip("Name shown to players")]
        public string abilityName = "New Ability";

        [Tooltip("Text shown to players, e.g. in a tooltip")]
        [TextArea(3, 6)]
        public string description = "Ability description...";

        [Tooltip("Icon id your UI uses to draw the ability")]
        public string iconId = "";

        [Tooltip("How the ability is started: used by hand (Active), always on (Passive), started by something happening (Triggered), switched on and off (Toggle) or held (Channeled)")]
        public AbilityType abilityType = AbilityType.Active;

        [Tooltip("Lowest character level that may use it")]
        [Min(0)]
        public int minLevel = 0;

        [Header("Costs")]
        [Tooltip("What using the ability takes from the caster; the game is asked whether the caster has enough and takes it")]
        public List<AbilityCostEntry> costs = new List<AbilityCostEntry>();

        [Header("Timing")]
        [Tooltip("Seconds before it can be used again; 0 means no wait")]
        [Min(0f)]
        public float cooldown = 0f;

        [Tooltip("Seconds of casting before it goes off; 0 means it goes off at once. The game finishes a cast that takes time")]
        [Min(0f)]
        public float castTime = 0f;

        [Tooltip("Seconds it keeps going while held, for held (channeled) abilities; 0 for everything else")]
        [Min(0f)]
        public float channelDuration = 0f;

        [Tooltip("Seconds between the ticks of a held ability (a beam burning every 0.5 s); each tick calls its effect again. 0: it lands once")]
        [Min(0f)]
        public float channelTickInterval = 0f;

        [Tooltip("Whether being hit or stunned can stop the cast")]
        public bool isInterruptible = true;

        [Tooltip("Whether the caster may walk while casting")]
        public bool canMoveWhileCasting = false;

        [Tooltip("How many times it can be used before it runs out; 0 means no limit")]
        [Min(0)]
        public int maxCharges = 0;

        [Tooltip("Seconds for a used charge to come back, one at a time; 0 means charges come back only when the game gives them")]
        [Min(0f)]
        public float chargeRechargeTime = 0f;

        [Header("Targeting")]
        [Tooltip("Who or what it may be aimed at")]
        public TargetType targetType = TargetType.None;

        [Tooltip("How far away the target may be; 0 means melee or self, with no distance check")]
        [Min(0f)]
        public float range = 0f;

        [Tooltip("Radius of the area it hits; 0 means a single target")]
        [Min(0f)]
        public float aoeRadius = 0f;

        /// <summary>
        /// Builds the ability the game runs from this asset. Empty cost slots are left out.
        /// </summary>
        public IAbility ToAbility()
        {
            var metadata = new AbilityMetadata(
                abilityId,
                abilityName,
                abilityType,
                cooldown: cooldown,
                range: range,
                aoeRadius: aoeRadius,
                minLevel: minLevel,
                maxCharges: maxCharges)
            {
                Description = description ?? string.Empty,
                IconId = iconId ?? string.Empty,
                ChargeRechargeTime = chargeRechargeTime
            };

            var resourceCosts = new List<ResourceCost>();
            if (costs != null)
            {
                foreach (var cost in costs)
                {
                    if (cost != null)
                        resourceCosts.Add(cost.ToResourceCost());
                }
            }

            var castInfo = new CastInfo(castTime, channelDuration, isInterruptible, canMoveWhileCasting)
            {
                ChannelTickInterval = channelTickInterval
            };

            return new Ability(metadata, resourceCosts, castInfo, targetType);
        }

        /// <summary>
        /// Lists what is wrong with this ability in plain words; empty when it is ready to use.
        /// </summary>
        public List<string> Validate()
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(abilityId))
                errors.Add("Ability ID cannot be empty");

            if (string.IsNullOrWhiteSpace(abilityName))
                errors.Add("Ability Name cannot be empty");

            if (minLevel < 0)
                errors.Add("Min Level cannot be negative");

            if (cooldown < 0f)
                errors.Add("Cooldown cannot be negative");

            if (castTime < 0f)
                errors.Add("Cast Time cannot be negative");

            if (channelDuration < 0f)
                errors.Add("Channel Duration cannot be negative");

            if (maxCharges < 0)
                errors.Add("Max Charges cannot be negative (0 means no limit)");

            if (channelTickInterval < 0f)
                errors.Add("Channel Tick Interval cannot be negative");

            if (chargeRechargeTime < 0f)
                errors.Add("Charge Recharge Time cannot be negative");

            if (range < 0f)
                errors.Add("Range cannot be negative");

            if (aoeRadius < 0f)
                errors.Add("Area Radius cannot be negative");

            if (costs != null)
            {
                var charged = new HashSet<string>();
                for (int i = 0; i < costs.Count; i++)
                {
                    var cost = costs[i];
                    if (cost == null)
                        continue;

                    if (string.IsNullOrWhiteSpace(cost.resourceType))
                    {
                        errors.Add($"Cost {i + 1} names no resource");
                        continue;
                    }

                    if (cost.amount < 0f)
                        errors.Add($"Cost {i + 1} ({cost.resourceType}) cannot be negative");

                    // Each cost is checked on its own, so two costs of the same resource could pass the check
                    // one by one and still take more than the caster has
                    if (!charged.Add(cost.resourceType))
                        errors.Add($"Resource '{cost.resourceType}' is charged more than once; put it in one cost");
                }
            }

            return errors;
        }
    }
}
