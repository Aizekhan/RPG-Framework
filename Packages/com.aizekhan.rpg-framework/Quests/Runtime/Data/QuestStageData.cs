using System;

namespace RPGFramework.Quests
{
    /// <summary>
    /// How a stage ends a quest: it does not (the quest goes on), with success (the quest completes and gives its
    /// rewards) or with failure (the quest fails).
    /// </summary>
    public enum QuestOutcome
    {
        None,
        Success,
        Failure
    }

    /// <summary>
    /// One stage of a quest: what the player does now (its objectives) and where the quest goes next (its links).
    /// </summary>
    [Serializable]
    public struct QuestStageData
    {
        /// <summary>Unique within the quest ("s1", "find_the_golem").</summary>
        public string Id;

        public string Title;
        public string Description;

        /// <summary>
        /// Ids of the quest's objectives that count in this stage. An objective named by no stage counts in every
        /// stage.
        /// </summary>
        public string[] ObjectiveIds;

        /// <summary>Where the quest can go from here, in order; the first one that applies is taken.</summary>
        public QuestStageLinkData[] Next;

        public QuestOutcome Outcome;
    }

    /// <summary>
    /// A way out of a stage. Without a condition it is taken when the stage's required objectives are done; with one
    /// it is taken when the game signals that condition (<see cref="IQuestStages.Signal"/>), done objectives or not.
    /// </summary>
    [Serializable]
    public struct QuestStageLinkData
    {
        public string To;

        /// <summary>A name the game signals ("golem_defeated"); empty = when the stage's objectives are done.</summary>
        public string Condition;
    }

    /// <summary>
    /// Something the game checks before the quest can be taken, beyond the prerequisite quests: a level, a faction
    /// standing, an item, a world flag, or a kind of the game's own. <see cref="IQuestRequirements"/> answers it.
    /// </summary>
    [Serializable]
    public struct QuestRequirementData
    {
        /// <summary><see cref="QuestRequirementKinds"/> or a kind of the game's own.</summary>
        public string Kind;

        /// <summary>What it is about: a faction id, an item id, a flag name; empty for a level.</summary>
        public string Id;

        /// <summary>The amount: the level, the standing, the item count; 1 for a flag.</summary>
        public int Value;
    }

    /// <summary>The usual requirement kinds; a game can use its own strings too.</summary>
    public static class QuestRequirementKinds
    {
        public const string Level = "level";
        public const string Faction = "faction";
        public const string Item = "item";
        public const string Flag = "flag";
    }
}
