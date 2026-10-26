using UnityEngine;
using RPGFramework.Quests;

namespace RPGFramework.Tools.QuestEditor
{
    /// <summary>
    /// ScriptableObject asset for visually creating and editing quests in Unity Inspector.
    /// Wraps RPGFramework.Quests.QuestData for Unity Editor integration.
    /// </summary>
    [CreateAssetMenu(fileName = "NewQuest", menuName = "RPG Framework/Quest Asset", order = 100)]
    public class QuestAsset : ScriptableObject
    {
        [Header("Quest Identity")]
        [Tooltip("Unique quest identifier (e.g., 'quest_tutorial_kill_goblins')")]
        public string questId = "quest_new_quest";

        [Tooltip("Display name shown to players")]
        public string title = "New Quest";

        [TextArea(3, 6)]
        [Tooltip("Quest description/story text")]
        public string description = "Quest description here...";

        [Header("Requirements")]
        [Tooltip("Quests that must be completed before this one is available")]
        public QuestAsset[] prerequisiteQuests;

        [Tooltip("Minimum character level required to accept this quest")]
        [Min(1)]
        public int minimumLevel = 1;

        [Tooltip("Can this quest be repeated after completion?")]
        public bool isRepeatable = false;

        [Header("Objectives")]
        [Tooltip("Quest objectives that must be completed")]
        public QuestObjectiveEntry[] objectives = new QuestObjectiveEntry[1];

        [Header("Rewards")]
        [Tooltip("Rewards granted upon quest completion")]
        public QuestRewardEntry[] rewards = new QuestRewardEntry[1];

        /// <summary>
        /// Convert this ScriptableObject to Framework QuestData struct.
        /// </summary>
        public QuestData ToQuestData()
        {
            // Convert prerequisite assets to IDs
            string[] prerequisiteIds = new string[prerequisiteQuests?.Length ?? 0];
            for (int i = 0; i < prerequisiteIds.Length; i++)
            {
                prerequisiteIds[i] = prerequisiteQuests[i] != null
                    ? prerequisiteQuests[i].questId
                    : "";
            }

            // Convert objectives
            QuestObjectiveData[] objectiveData = new QuestObjectiveData[objectives?.Length ?? 0];
            for (int i = 0; i < objectiveData.Length; i++)
            {
                objectiveData[i] = objectives[i].ToObjectiveData();
            }

            // Convert rewards
            QuestRewardData[] rewardData = new QuestRewardData[rewards?.Length ?? 0];
            for (int i = 0; i < rewardData.Length; i++)
            {
                rewardData[i] = rewards[i].ToRewardData();
            }

            return new QuestData
            {
                Id = questId,
                Title = title,
                Description = description,
                Objectives = objectiveData,
                Rewards = rewardData,
                PrerequisiteQuestIds = prerequisiteIds,
                MinimumLevel = minimumLevel,
                IsRepeatable = isRepeatable
            };
        }

        /// <summary>
        /// Validate quest data for common issues.
        /// </summary>
        public void OnValidate()
        {
            // Ensure quest ID is valid
            if (string.IsNullOrWhiteSpace(questId))
            {
                questId = name.ToLower().Replace(" ", "_");
            }

            // Ensure at least one objective
            if (objectives == null || objectives.Length == 0)
            {
                objectives = new QuestObjectiveEntry[1];
            }

            // Initialize null objectives
            for (int i = 0; i < objectives.Length; i++)
            {
                if (objectives[i] == null)
                {
                    objectives[i] = new QuestObjectiveEntry();
                }
            }

            // Ensure at least one reward
            if (rewards == null || rewards.Length == 0)
            {
                rewards = new QuestRewardEntry[1];
            }

            // Initialize null rewards
            for (int i = 0; i < rewards.Length; i++)
            {
                if (rewards[i] == null)
                {
                    rewards[i] = new QuestRewardEntry();
                }
            }
        }
    }

    /// <summary>
    /// Serializable quest objective for Unity Inspector.
    /// </summary>
    [System.Serializable]
    public class QuestObjectiveEntry
    {
        [Tooltip("Unique objective identifier")]
        public string objectiveId = "objective_1";

        [Tooltip("Objective type (Kill, Collect, Interact, Explore)")]
        public ObjectiveType type = ObjectiveType.Kill;

        [Tooltip("Target entity/item/location ID")]
        public string targetId = "target_id";

        [Tooltip("Number of times objective must be completed")]
        [Min(1)]
        public int requiredCount = 1;

        [TextArea(2, 4)]
        [Tooltip("Objective description shown to player")]
        public string description = "Complete the objective";

        [Tooltip("Is this objective optional?")]
        public bool isOptional = false;

        public QuestObjectiveData ToObjectiveData()
        {
            return new QuestObjectiveData
            {
                Id = objectiveId,
                Type = type,
                TargetId = targetId,
                RequiredCount = requiredCount,
                Description = description,
                IsOptional = isOptional
            };
        }
    }

    /// <summary>
    /// Serializable quest reward for Unity Inspector.
    /// </summary>
    [System.Serializable]
    public class QuestRewardEntry
    {
        [Tooltip("Reward type (Experience, Currency, Item, etc.)")]
        public RewardType type = RewardType.Experience;

        [Tooltip("Reward identifier (item ID, currency type, etc.)")]
        public string rewardId = "reward_xp";

        [Tooltip("Amount/quantity of reward")]
        [Min(1)]
        public int amount = 100;

        public QuestRewardData ToRewardData()
        {
            return new QuestRewardData
            {
                Type = type,
                RewardId = rewardId,
                Amount = amount
            };
        }
    }
}
