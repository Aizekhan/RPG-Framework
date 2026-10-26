using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using RPGFramework.Quests;

namespace RPGFramework.Tools.QuestEditor
{
    /// <summary>
    /// Central database of all quest assets in the project.
    /// Provides quest lookup, validation, and management utilities.
    /// </summary>
    [CreateAssetMenu(fileName = "QuestDatabase", menuName = "RPG Framework/Quest Database", order = 99)]
    public class QuestDatabase : ScriptableObject
    {
        [Header("Quest Registry")]
        [Tooltip("All quests in the game")]
        public List<QuestAsset> quests = new List<QuestAsset>();

        [Header("Statistics")]
        [SerializeField]
        [Tooltip("Total number of quests")]
        private int totalQuests;

        [SerializeField]
        [Tooltip("Number of main quests")]
        private int mainQuests;

        [SerializeField]
        [Tooltip("Number of side quests")]
        private int sideQuests;

        [SerializeField]
        [Tooltip("Number of repeatable quests")]
        private int repeatableQuests;

        /// <summary>
        /// Get quest asset by ID.
        /// </summary>
        public QuestAsset GetQuest(string questId)
        {
            return quests?.FirstOrDefault(q => q != null && q.questId == questId);
        }

        /// <summary>
        /// Get all quest assets.
        /// </summary>
        public IReadOnlyList<QuestAsset> GetAllQuests()
        {
            return quests?.Where(q => q != null).ToList() ?? new List<QuestAsset>();
        }

        /// <summary>
        /// Convert all quests to Framework QuestData array.
        /// </summary>
        public QuestData[] ToQuestDataArray()
        {
            return quests?
                .Where(q => q != null)
                .Select(q => q.ToQuestData())
                .ToArray() ?? System.Array.Empty<QuestData>();
        }

        /// <summary>
        /// Check if quest ID is unique.
        /// </summary>
        public bool IsQuestIdUnique(string questId, QuestAsset excludeQuest = null)
        {
            if (quests == null) return true;

            return !quests.Any(q =>
                q != null &&
                q != excludeQuest &&
                q.questId == questId);
        }

        /// <summary>
        /// Get all quests that have the specified quest as a prerequisite.
        /// </summary>
        public List<QuestAsset> GetDependentQuests(QuestAsset quest)
        {
            if (quests == null || quest == null)
                return new List<QuestAsset>();

            return quests
                .Where(q => q != null &&
                           q.prerequisiteQuests != null &&
                           q.prerequisiteQuests.Contains(quest))
                .ToList();
        }

        /// <summary>
        /// Validate all quests in database.
        /// </summary>
        public List<string> ValidateDatabase()
        {
            List<string> errors = new List<string>();

            if (quests == null || quests.Count == 0)
            {
                errors.Add("Database is empty");
                return errors;
            }

            // Check for duplicate IDs
            var duplicateIds = quests
                .Where(q => q != null)
                .GroupBy(q => q.questId)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key);

            foreach (var id in duplicateIds)
            {
                errors.Add($"Duplicate quest ID: {id}");
            }

            // Check for null quests
            int nullCount = quests.Count(q => q == null);
            if (nullCount > 0)
            {
                errors.Add($"{nullCount} null quest(s) in database");
            }

            // Check for missing prerequisites
            foreach (var quest in quests.Where(q => q != null))
            {
                if (quest.prerequisiteQuests == null) continue;

                foreach (var prereq in quest.prerequisiteQuests)
                {
                    if (prereq == null)
                    {
                        errors.Add($"Quest '{quest.questId}' has null prerequisite");
                    }
                    else if (!quests.Contains(prereq))
                    {
                        errors.Add($"Quest '{quest.questId}' references prerequisite '{prereq.questId}' not in database");
                    }
                }
            }

            // Check for circular dependencies (a quest whose prerequisites lead into a loop can never be started)
            var reachesLoop = new Dictionary<QuestAsset, bool>();
            foreach (var quest in quests.Where(q => q != null))
            {
                if (ReachesLoop(quest, reachesLoop, new HashSet<QuestAsset>()))
                {
                    errors.Add($"Circular dependency detected for quest: {quest.questId}");
                }
            }

            return errors;
        }

        /// <summary>
        /// Whether following the quest's prerequisites leads into a loop. Each quest is walked once (results are kept in
        /// <paramref name="known"/>), so long branching chains are checked quickly.
        /// </summary>
        private static bool ReachesLoop(QuestAsset quest, Dictionary<QuestAsset, bool> known, HashSet<QuestAsset> onPath)
        {
            if (known.TryGetValue(quest, out var result))
                return result;
            if (!onPath.Add(quest))
                return true;

            result = false;
            if (quest.prerequisiteQuests != null)
            {
                foreach (var prereq in quest.prerequisiteQuests)
                {
                    if (prereq != null && ReachesLoop(prereq, known, onPath))
                    {
                        result = true;
                        break;
                    }
                }
            }

            onPath.Remove(quest);
            known[quest] = result;
            return result;
        }

        /// <summary>
        /// Update statistics.
        /// </summary>
        public void UpdateStatistics()
        {
            if (quests == null)
            {
                totalQuests = mainQuests = sideQuests = repeatableQuests = 0;
                return;
            }

            var validQuests = quests.Where(q => q != null).ToList();
            totalQuests = validQuests.Count;
            repeatableQuests = validQuests.Count(q => q.isRepeatable);

            // Note: Main vs Side quest classification would require additional metadata
            // For now, we'll assume all non-repeatable quests are main quests
            mainQuests = totalQuests - repeatableQuests;
            sideQuests = 0; // Would need quest metadata to determine
        }

        private void OnValidate()
        {
            UpdateStatistics();
        }
    }
}
