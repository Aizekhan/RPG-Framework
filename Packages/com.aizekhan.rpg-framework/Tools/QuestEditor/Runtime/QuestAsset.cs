using System.Linq;
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

        [Tooltip("The game's own kind of quest (main, side, faction, daily...); free text")]
        public string questType = "";

        [Tooltip("Hours before a repeatable quest can be taken again (24 for a daily); 0 = at once")]
        [Min(0f)]
        public float repeatCooldownHours;

        [Tooltip("Checked by the game (IQuestRequirements) before the quest can be taken: a level, faction, item or flag")]
        public QuestRequirementEntry[] requirements = new QuestRequirementEntry[0];

        [Header("Stages")]
        [Tooltip("The first stage; empty = the first in the list")]
        public string startStage = "";

        [Tooltip("Stages with links between them. Empty = the quest completes when its objectives are done")]
        public QuestStageEntry[] stages = new QuestStageEntry[0];

        [Header("Objectives")]
        [Tooltip("Quest objectives that must be completed")]
        public QuestObjectiveEntry[] objectives = new QuestObjectiveEntry[1];

        [Header("Rewards")]
        [Tooltip("Rewards granted upon quest completion")]
        public QuestRewardEntry[] rewards = new QuestRewardEntry[1];

        /// <summary>
        /// Convert this ScriptableObject to Framework QuestData struct. Empty objective, reward and prerequisite slots
        /// are left out.
        /// </summary>
        public QuestData ToQuestData()
        {
            // Empty slots (added in the inspector and not filled yet, or never filled in an asset made in code) are
            // skipped: an empty prerequisite would otherwise lock the quest forever
            string[] prerequisiteIds = (prerequisiteQuests ?? System.Array.Empty<QuestAsset>())
                .Where(p => p != null)
                .Select(p => p.questId)
                .ToArray();

            QuestObjectiveData[] objectiveData = (objectives ?? System.Array.Empty<QuestObjectiveEntry>())
                .Where(o => o != null)
                .Select(o => o.ToObjectiveData())
                .ToArray();

            QuestRewardData[] rewardData = (rewards ?? System.Array.Empty<QuestRewardEntry>())
                .Where(r => r != null)
                .Select(r => r.ToRewardData())
                .ToArray();

            return new QuestData
            {
                Id = questId,
                Title = title,
                Description = description,
                Objectives = objectiveData,
                Rewards = rewardData,
                PrerequisiteQuestIds = prerequisiteIds,
                MinimumLevel = minimumLevel,
                IsRepeatable = isRepeatable,
                Type = questType ?? "",
                RepeatCooldownSeconds = repeatCooldownHours * 3600d,
                Requirements = (requirements ?? System.Array.Empty<QuestRequirementEntry>())
                    .Where(r => r != null && !string.IsNullOrWhiteSpace(r.kind))
                    .Select(r => new QuestRequirementData { Kind = r.kind.Trim(), Id = r.id ?? "", Value = r.value })
                    .ToArray(),
                StartStageId = startStage ?? "",
                Stages = (stages ?? System.Array.Empty<QuestStageEntry>())
                    .Where(s => s != null)
                    .Select(s => s.ToStageData())
                    .ToArray()
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
    /// A quest stage in the inspector (<see cref="QuestStageData"/>).
    /// </summary>
    [System.Serializable]
    public class QuestStageEntry
    {
        [Tooltip("Unique within the quest")]
        public string stageId = "s1";

        public string title = "";

        [TextArea(2, 4)]
        public string description = "";

        [Tooltip("Ids of the objectives that count in this stage; an objective named by no stage counts in all of them")]
        public string[] objectiveIds = new string[0];

        [Tooltip("Where the quest goes next; the first link that applies is taken")]
        public QuestLinkEntry[] next = new QuestLinkEntry[0];

        [Tooltip("Success completes the quest, Failure fails it")]
        public QuestOutcome outcome = QuestOutcome.None;

        public QuestStageData ToStageData() => new QuestStageData
        {
            Id = stageId,
            Title = title,
            Description = description,
            ObjectiveIds = (objectiveIds ?? System.Array.Empty<string>()).Where(o => !string.IsNullOrWhiteSpace(o)).ToArray(),
            Next = (next ?? System.Array.Empty<QuestLinkEntry>())
                .Where(l => l != null && !string.IsNullOrWhiteSpace(l.toStage))
                .Select(l => new QuestStageLinkData { To = l.toStage, Condition = l.condition ?? "" })
                .ToArray(),
            Outcome = outcome
        };
    }

    /// <summary>A link between quest stages in the inspector (<see cref="QuestStageLinkData"/>).</summary>
    [System.Serializable]
    public class QuestLinkEntry
    {
        public string toStage = "";

        [Tooltip("A name the game signals (IQuestStages.Signal); empty = when the stage's objectives are done")]
        public string condition = "";
    }

    /// <summary>A quest requirement in the inspector (<see cref="QuestRequirementData"/>).</summary>
    [System.Serializable]
    public class QuestRequirementEntry
    {
        [Tooltip("level, faction, item, flag, or a kind of the game's own")]
        public string kind = QuestRequirementKinds.Flag;

        [Tooltip("The faction, item or flag; empty for a level")]
        public string id = "";

        [Tooltip("The level, standing or count; 1 for a flag")]
        public int value = 1;
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
