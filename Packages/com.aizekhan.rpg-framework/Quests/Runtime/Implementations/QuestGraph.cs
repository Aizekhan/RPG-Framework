using System;
using System.Collections.Generic;
using System.Linq;

namespace RPGFramework.Quests
{
    public enum QuestIssueLevel
    {
        Warning,
        Error
    }

    /// <summary>A problem in a quest's data (<see cref="QuestGraph"/>).</summary>
    public readonly struct QuestIssue
    {
        public QuestIssue(QuestIssueLevel level, string code, string questId, string where, string message)
        {
            Level = level;
            Code = code;
            QuestId = questId;
            Where = where;
            Message = message;
        }

        public QuestIssueLevel Level { get; }

        /// <summary>
        /// noTitle, dupStage, dupObjective, badStart, badLink, unknownObjective, unreachable, deadEnd, linksAfterEnd, noEnd, noWayOut,
        /// badRequirement, cooldownNotRepeatable, selfPrerequisite, dupQuest, missingPrerequisite, prerequisiteCycle.
        /// </summary>
        public string Code { get; }

        public string QuestId { get; }

        /// <summary>The stage, objective or prerequisite it is about; null for the quest itself.</summary>
        public string Where { get; }

        public string Message { get; }

        public override string ToString() => $"[{Level}] {QuestId}{(Where != null ? "/" + Where : "")}: {Message} ({Code})";
    }

    /// <summary>
    /// Checks quests: their stages form a graph that starts somewhere real, links to stages that exist, reaches a
    /// success stage, has no stage that cannot be reached and no place the player gets stuck in; objectives and
    /// requirements are well formed; prerequisites exist and do not go round in a circle.
    /// </summary>
    public static class QuestGraph
    {
        public static List<QuestIssue> Check(IQuest quest)
        {
            var issues = new List<QuestIssue>();
            if (quest == null)
                return issues;
            string id = quest.Id;
            if (string.IsNullOrWhiteSpace(quest.Title))
                issues.Add(Warning("noTitle", id, null, "has no title"));
            foreach (var duplicate in quest.Objectives.GroupBy(o => o.Id).Where(g => g.Count() > 1))
                issues.Add(Error("dupObjective", id, duplicate.Key, "two objectives have this id"));
            if (quest.PrerequisiteQuestIds.Contains(id))
                issues.Add(Error("selfPrerequisite", id, id, "needs itself to be completed first"));

            if (!(quest is IStagedQuest staged))
                return issues;
            foreach (var requirement in staged.Requirements)
            {
                if (string.IsNullOrWhiteSpace(requirement.Kind))
                    issues.Add(Error("badRequirement", id, requirement.Id, "a requirement has no kind"));
            }
            if (!quest.IsRepeatable && staged.RepeatCooldownSeconds > 0d)
                issues.Add(Warning("cooldownNotRepeatable", id, null, "has a repeat cooldown but is not repeatable"));
            if (staged.Stages.Count == 0)
                return issues;

            var stages = new Dictionary<string, QuestStageData>();
            foreach (var stage in staged.Stages)
            {
                if (string.IsNullOrWhiteSpace(stage.Id) || stages.ContainsKey(stage.Id))
                    issues.Add(Error("dupStage", id, stage.Id, string.IsNullOrWhiteSpace(stage.Id) ? "a stage has no id" : "two stages have this id"));
                else
                    stages[stage.Id] = stage;
            }
            var objectiveIds = new HashSet<string>(quest.Objectives.Select(o => o.Id));
            foreach (var stage in stages.Values)
            {
                foreach (var objective in stage.ObjectiveIds ?? Array.Empty<string>())
                {
                    if (!objectiveIds.Contains(objective))
                        issues.Add(Error("unknownObjective", id, stage.Id, $"names objective '{objective}', which the quest does not have"));
                }
                foreach (var link in stage.Next ?? Array.Empty<QuestStageLinkData>())
                {
                    if (string.IsNullOrEmpty(link.To) || !stages.ContainsKey(link.To))
                        issues.Add(Error("badLink", id, stage.Id, $"links to stage '{link.To}', which does not exist"));
                }
                if (stage.Outcome != QuestOutcome.None && (stage.Next?.Length ?? 0) > 0)
                    issues.Add(Warning("linksAfterEnd", id, stage.Id, "ends the quest, its links are never taken"));
                else if (stage.Outcome == QuestOutcome.None && (stage.Next?.Length ?? 0) == 0)
                    issues.Add(Warning("deadEnd", id, stage.Id, "has no way out and does not end the quest"));
            }

            string start = string.IsNullOrEmpty(staged.StartStageId) ? staged.Stages[0].Id : staged.StartStageId;
            if (start == null || !stages.ContainsKey(start))
            {
                issues.Add(Error("badStart", id, start, $"starts at stage '{start}', which does not exist"));
                return issues;
            }

            var reachable = Walk(start, s => Links(stages, s));
            foreach (var stage in stages.Keys.Where(s => !reachable.Contains(s)))
                issues.Add(Warning("unreachable", id, stage, "cannot be reached from the first stage"));
            if (!reachable.Any(s => stages[s].Outcome == QuestOutcome.Success))
                issues.Add(Error("noEnd", id, null, "no success stage can be reached: the quest never completes"));

            // Reachable stages from which no end (success or failure) can be reached: the player is stuck there
            var ends = stages.Values.Where(s => s.Outcome != QuestOutcome.None).Select(s => s.Id).ToList();
            var reverse = stages.Keys.ToDictionary(s => s, s => new List<string>());
            foreach (var stage in stages.Values)
            {
                foreach (var to in Links(stages, stage.Id))
                    reverse[to].Add(stage.Id);
            }
            var canEnd = new HashSet<string>();
            foreach (var end in ends)
                canEnd.UnionWith(Walk(end, s => reverse[s]));
            foreach (var stage in reachable.Where(s => !canEnd.Contains(s) && (stages[s].Next?.Length ?? 0) > 0))
                issues.Add(Warning("noWayOut", id, stage, "goes round in a loop that never reaches an end"));
            return issues;
        }

        /// <summary><see cref="Check(IQuest)"/> for every quest, plus prerequisites that are missing or circular.</summary>
        public static List<QuestIssue> Check(IEnumerable<IQuest> quests)
        {
            var all = quests.Where(q => q != null).ToList();
            var issues = all.SelectMany(Check).ToList();
            var byId = new Dictionary<string, IQuest>();
            foreach (var quest in all)
            {
                if (byId.ContainsKey(quest.Id))
                    issues.Add(new QuestIssue(QuestIssueLevel.Error, "dupQuest", quest.Id, null, "two quests have this id"));
                else
                    byId[quest.Id] = quest;
            }
            foreach (var quest in byId.Values)
            {
                foreach (var prerequisite in quest.PrerequisiteQuestIds.Where(p => p != quest.Id && !byId.ContainsKey(p)))
                    issues.Add(Error("missingPrerequisite", quest.Id, prerequisite, $"needs quest '{prerequisite}', which does not exist"));
            }
            var reported = new HashSet<string>();
            foreach (var quest in byId.Values)
            {
                var cycle = Cycle(quest.Id, byId);
                if (cycle != null && reported.Add(string.Join(">", cycle.Distinct().OrderBy(c => c, StringComparer.Ordinal))))
                    issues.Add(Error("prerequisiteCycle", quest.Id, null, "prerequisites go round: " + string.Join(" -> ", cycle)));
            }
            return issues;
        }

        private static IEnumerable<string> Links(Dictionary<string, QuestStageData> stages, string stageId) =>
            (stages[stageId].Next ?? Array.Empty<QuestStageLinkData>())
                .Where(l => l.To != null && stages.ContainsKey(l.To)).Select(l => l.To);

        private static HashSet<string> Walk(string from, Func<string, IEnumerable<string>> next)
        {
            var seen = new HashSet<string> { from };
            var open = new Stack<string>();
            open.Push(from);
            while (open.Count > 0)
            {
                foreach (var to in next(open.Pop()))
                {
                    if (seen.Add(to))
                        open.Push(to);
                }
            }
            return seen;
        }

        private static List<string> Cycle(string start, Dictionary<string, IQuest> quests)
        {
            var path = new List<string>();
            var onPath = new HashSet<string>();
            var done = new HashSet<string>();
            return Visit(start);

            List<string> Visit(string id)
            {
                if (onPath.Contains(id))
                    return id == start ? new List<string>(path) { id } : null;
                if (done.Contains(id) || !quests.TryGetValue(id, out var quest))
                    return null;
                path.Add(id);
                onPath.Add(id);
                foreach (var prerequisite in quest.PrerequisiteQuestIds.Where(p => p != id))
                {
                    var found = Visit(prerequisite);
                    if (found != null)
                        return found;
                }
                path.RemoveAt(path.Count - 1);
                onPath.Remove(id);
                done.Add(id);
                return null;
            }
        }

        private static QuestIssue Error(string code, string quest, string where, string message) =>
            new QuestIssue(QuestIssueLevel.Error, code, quest, where, message);

        private static QuestIssue Warning(string code, string quest, string where, string message) =>
            new QuestIssue(QuestIssueLevel.Warning, code, quest, where, message);
    }
}
