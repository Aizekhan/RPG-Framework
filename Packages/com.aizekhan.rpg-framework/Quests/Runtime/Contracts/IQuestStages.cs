using System;
using System.Collections.Generic;

namespace RPGFramework.Quests
{
    /// <summary>
    /// A quest with stages, a type, requirements and a repeat cooldown (<see cref="Quest"/> is one). A quest without
    /// stages works as before: it completes when its required objectives are done.
    /// </summary>
    public interface IStagedQuest : IQuest
    {
        /// <summary>The game's own kind of quest ("main", "side", "faction", "daily"...); free text.</summary>
        string Type { get; }

        /// <summary>Empty for a quest without stages.</summary>
        IReadOnlyList<QuestStageData> Stages { get; }

        /// <summary>The first stage; the first in <see cref="Stages"/> when not set.</summary>
        string StartStageId { get; }

        IReadOnlyList<QuestRequirementData> Requirements { get; }

        /// <summary>Seconds after a completion before a repeatable quest can be taken again; 0 = at once.</summary>
        double RepeatCooldownSeconds { get; }
    }

    /// <summary>
    /// Answers a quest's <see cref="QuestRequirementData"/> for an entity: the game knows levels, factions, items
    /// and flags. Given to <see cref="QuestManager"/>; without it, a quest with requirements cannot be taken and
    /// <see cref="IQuest.MinimumLevel"/> is not checked.
    /// </summary>
    public interface IQuestRequirements
    {
        bool IsMet(int entityId, QuestRequirementData requirement);
    }

    /// <summary>Why a quest cannot be taken (<see cref="IQuestStages.WhyNot"/>).</summary>
    public enum QuestRefusal
    {
        None,
        UnknownQuest,
        AlreadyActive,
        AlreadyCompleted,
        CoolingDown,
        PrerequisiteMissing,
        LevelTooLow,
        RequirementNotMet
    }

    /// <summary>
    /// The stages of the quests an entity has taken (<see cref="QuestManager"/> implements it next to
    /// <see cref="IQuestManager"/>). A stage changes when its required objectives are done and it has a link without
    /// a condition, when the game signals a link's condition, or when the game moves it.
    /// </summary>
    public interface IQuestStages
    {
        /// <summary>The stage the quest is in; null when it is not active or has no stages.</summary>
        string CurrentStageOf(int entityId, string questId);

        /// <summary>
        /// Moves the quest to <paramref name="stageId"/> along a link of its current stage (any stage with
        /// <paramref name="force"/>). Entering a success stage completes the quest, a failure stage fails it.
        /// </summary>
        bool AdvanceStage(int entityId, string questId, string stageId, bool force = false);

        /// <summary>
        /// The game says <paramref name="condition"/> happened ("golem_defeated"): every active quest of the entity
        /// whose current stage has a link with that condition takes it. Returns how many quests moved.
        /// </summary>
        int Signal(int entityId, string condition);

        /// <summary>Why the entity cannot take the quest now; <see cref="QuestRefusal.None"/> when it can.</summary>
        QuestRefusal WhyNot(int entityId, string questId);

        /// <summary>When the entity last completed the quest; null when never (or not since the save).</summary>
        DateTime? LastCompletedAt(int entityId, string questId);

        /// <summary>Time left before a repeatable quest can be taken again; zero when it can.</summary>
        TimeSpan CooldownLeft(int entityId, string questId);

        /// <summary>For loading a save: puts an active quest in a stage without checks, events or outcomes.</summary>
        bool RestoreStage(int entityId, string questId, string stageId);

        /// <summary>For loading a save: the time the entity last completed the quest (for its cooldown).</summary>
        void RestoreCompletedAt(int entityId, string questId, DateTime completedAtUtc);
    }
}
