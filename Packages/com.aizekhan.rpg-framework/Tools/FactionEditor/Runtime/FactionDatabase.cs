using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using RPGFramework.Factions;

namespace RPGFramework.Tools.FactionEditor
{
    /// <summary>
    /// Designer asset that lists the factions of a game and how they feel about each other. It becomes the
    /// <see cref="FactionRegistry"/> the game runs on (reputation, and PvP teams through faction hostility).
    /// </summary>
    [CreateAssetMenu(fileName = "FactionDatabase", menuName = "RPG Framework/Faction Database", order = 105)]
    public class FactionDatabase : ScriptableObject
    {
        /// <summary>Lowest starting reputation the game keeps; lower values are raised to this.</summary>
        public const int MinReputation = -100;

        /// <summary>Highest starting reputation the game keeps; higher values are lowered to this.</summary>
        public const int MaxReputation = 100;

        /// <summary>Lowest reputation modifier the game keeps; lower values are raised to this.</summary>
        public const float MinModifier = -1f;

        /// <summary>Highest reputation modifier the game keeps; higher values are lowered to this.</summary>
        public const float MaxModifier = 1f;

        [Header("Factions")]
        [Tooltip("Every faction in the game")]
        public List<FactionEntry> factions = new List<FactionEntry>();

        [Header("Relationships")]
        [Tooltip("How pairs of factions feel about each other. A pair that is not listed is neutral. Easiest to edit in the relationship grid below")]
        public List<FactionRelationEntry> relationships = new List<FactionRelationEntry>();

        /// <summary>
        /// Builds a registry with every faction and relationship in this asset. Throws if <see cref="Validate"/>
        /// finds problems, so a broken asset is noticed instead of silently losing factions.
        /// </summary>
        public FactionRegistry ToRegistry()
        {
            var problems = Validate();
            if (problems.Count > 0)
                throw new InvalidOperationException(
                    $"Faction database '{name}' has problems:\n- " + string.Join("\n- ", problems));

            var registry = new FactionRegistry();

            foreach (var entry in factions)
            {
                registry.RegisterFaction(new Faction(
                    entry.factionId,
                    entry.factionName,
                    entry.description,
                    entry.factionType,
                    entry.defaultReputation,
                    entry.isActive));
            }

            foreach (var relation in relationships)
            {
                registry.RegisterRelationship(new FactionRelationship(
                    relation.factionA,
                    relation.factionB,
                    relation.relationType,
                    relation.reputationModifier));
            }

            return registry;
        }

        /// <summary>
        /// Finds problems a designer must fix. An empty list means the asset is ready.
        /// </summary>
        public List<string> Validate()
        {
            var problems = new List<string>();
            var knownIds = new HashSet<string>();
            var duplicateIds = new HashSet<string>();

            if (factions == null || factions.Count == 0)
                problems.Add("There are no factions yet");

            if (factions != null)
            {
                for (int i = 0; i < factions.Count; i++)
                {
                    var entry = factions[i];
                    if (entry == null)
                    {
                        problems.Add($"Faction {i + 1} is empty");
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(entry.factionId))
                        problems.Add($"Faction {i + 1} has no ID");
                    else if (!knownIds.Add(entry.factionId))
                        duplicateIds.Add(entry.factionId);

                    if (string.IsNullOrWhiteSpace(entry.factionName))
                        problems.Add($"Faction '{entry.factionId}' has no name");

                    if (entry.defaultReputation < MinReputation || entry.defaultReputation > MaxReputation)
                        problems.Add($"Faction '{entry.factionId}' starting reputation {entry.defaultReputation} must be between {MinReputation} and {MaxReputation}");
                }
            }

            foreach (var id in duplicateIds)
                problems.Add($"Duplicate faction ID: {id}");

            if (relationships == null)
                return problems;

            var seenPairs = new Dictionary<string, FactionRelationEntry>();
            for (int i = 0; i < relationships.Count; i++)
            {
                var relation = relationships[i];
                if (relation == null)
                {
                    problems.Add($"Relationship {i + 1} is empty");
                    continue;
                }

                string label = $"Relationship {i + 1} ({relation.factionA} - {relation.factionB})";

                if (string.IsNullOrWhiteSpace(relation.factionA) || string.IsNullOrWhiteSpace(relation.factionB))
                {
                    problems.Add($"{label} needs two faction IDs");
                    continue;
                }

                if (relation.factionA == relation.factionB)
                {
                    problems.Add($"{label} relates a faction to itself");
                    continue;
                }

                if (!knownIds.Contains(relation.factionA))
                    problems.Add($"{label} names faction '{relation.factionA}', which is not in the faction list");
                if (!knownIds.Contains(relation.factionB))
                    problems.Add($"{label} names faction '{relation.factionB}', which is not in the faction list");

                if (relation.reputationModifier < MinModifier || relation.reputationModifier > MaxModifier)
                    problems.Add($"{label} reputation modifier {relation.reputationModifier} must be between {MinModifier} and {MaxModifier}");

                string key = PairKey(relation.factionA, relation.factionB);
                if (seenPairs.TryGetValue(key, out var earlier))
                {
                    problems.Add(earlier.relationType != relation.relationType
                        ? $"{label} is listed more than once with different relations ({earlier.relationType} and {relation.relationType}); only the last one would count"
                        : $"{label} is listed more than once; only the last one would count");
                }
                else
                {
                    seenPairs[key] = relation;
                }
            }

            return problems;
        }

        /// <summary>
        /// How two factions feel about each other: the relation listed for the pair (in either order), or
        /// <see cref="FactionRelationType.Neutral"/> when the pair is not listed. If the pair is listed more than once,
        /// the last one counts, as in the game.
        /// </summary>
        public FactionRelationType GetRelation(string factionA, string factionB)
        {
            var relation = FindLast(factionA, factionB);
            return relation != null ? relation.relationType : FactionRelationType.Neutral;
        }

        /// <summary>
        /// Sets how two factions feel about each other, keeping one entry per pair. Neutral removes the pair (an
        /// unlisted pair is neutral). A new or changed relation gets the usual reputation modifier for its kind, which
        /// can be fine-tuned in the relationship list afterwards. Does nothing for a faction paired with itself or an
        /// empty ID.
        /// </summary>
        public void SetRelation(string factionA, string factionB, FactionRelationType relationType)
        {
            if (string.IsNullOrWhiteSpace(factionA) || string.IsNullOrWhiteSpace(factionB) || factionA == factionB)
                return;

            if (relationships == null)
                relationships = new List<FactionRelationEntry>();

            if (relationType == FactionRelationType.Neutral)
            {
                relationships.RemoveAll(r => r != null && IsPair(r, factionA, factionB));
                return;
            }

            float modifier = FactionRelationship.Create(factionA, factionB, relationType).ReputationModifier;
            var existing = FindLast(factionA, factionB);
            if (existing != null)
            {
                // Keep the existing entry (and its order), drop any extra copies of the pair
                relationships.RemoveAll(r => r != null && r != existing && IsPair(r, factionA, factionB));
                if (existing.relationType != relationType)
                {
                    existing.relationType = relationType;
                    existing.reputationModifier = modifier;
                }
                return;
            }

            relationships.Add(new FactionRelationEntry
            {
                factionA = factionA,
                factionB = factionB,
                relationType = relationType,
                reputationModifier = modifier
            });
        }

        private FactionRelationEntry FindLast(string factionA, string factionB)
        {
            if (relationships == null)
                return null;

            for (int i = relationships.Count - 1; i >= 0; i--)
            {
                var relation = relationships[i];
                if (relation != null && IsPair(relation, factionA, factionB))
                    return relation;
            }

            return null;
        }

        private static bool IsPair(FactionRelationEntry relation, string factionA, string factionB)
        {
            return (relation.factionA == factionA && relation.factionB == factionB)
                   || (relation.factionA == factionB && relation.factionB == factionA);
        }

        private static string PairKey(string factionA, string factionB)
        {
            return string.CompareOrdinal(factionA, factionB) <= 0
                ? factionA + "\n" + factionB
                : factionB + "\n" + factionA;
        }

        /// <summary>
        /// IDs of the factions in the list that have one, each once, in list order. Used for the relationship grid.
        /// </summary>
        public List<string> GetFactionIds()
        {
            if (factions == null)
                return new List<string>();

            return factions
                .Where(f => f != null && !string.IsNullOrWhiteSpace(f.factionId))
                .Select(f => f.factionId)
                .Distinct()
                .ToList();
        }
    }

    /// <summary>
    /// One faction as the designer fills it in.
    /// </summary>
    [Serializable]
    public class FactionEntry
    {
        [Tooltip("Unique ID the game and other assets use for this faction (e.g. 'mages_guild')")]
        public string factionId = "faction_new";

        [Tooltip("Name shown to players")]
        public string factionName = "New Faction";

        [TextArea(2, 4)]
        [Tooltip("What the faction is and wants")]
        public string description = "";

        [Tooltip("What kind of group this is")]
        public FactionType factionType = FactionType.Other;

        [Range(FactionDatabase.MinReputation, FactionDatabase.MaxReputation)]
        [Tooltip("Reputation a new character starts with: below 0 starts disliked, above 0 starts liked")]
        public int defaultReputation = 0;

        [Tooltip("Whether the faction is in the game now; turn off for factions unlocked later")]
        public bool isActive = true;
    }

    /// <summary>
    /// How one pair of factions feels about each other. The order of the two IDs does not matter.
    /// </summary>
    [Serializable]
    public class FactionRelationEntry
    {
        [Tooltip("ID of one faction in the pair")]
        public string factionA = "";

        [Tooltip("ID of the other faction in the pair")]
        public string factionB = "";

        [Tooltip("How the two factions feel about each other. Enemy and Rival factions fight each other in PvP")]
        public FactionRelationType relationType = FactionRelationType.Neutral;

        [Range(FactionDatabase.MinModifier, FactionDatabase.MaxModifier)]
        [Tooltip("How much reputation with one faction spills over to the other: 0.5 gives half of the gain, -0.75 takes away three quarters")]
        public float reputationModifier = 0f;
    }
}
