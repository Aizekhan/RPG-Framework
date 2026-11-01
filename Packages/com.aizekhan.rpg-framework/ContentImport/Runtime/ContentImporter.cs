using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RPGFramework.Dialogue;
using RPGFramework.Persistence;
using RPGFramework.Quests;

namespace RPGFramework.ContentImport
{
    public enum ContentProblemLevel
    {
        Warning,
        Error
    }

    /// <summary>A problem in an imported file: its form, or the quest and dialogue graph checks.</summary>
    public readonly struct ContentProblem
    {
        public ContentProblem(ContentProblemLevel level, string code, string where, string message)
        {
            Level = level;
            Code = code;
            Where = where;
            Message = message;
        }

        public ContentProblemLevel Level { get; }

        /// <summary>badJson, badSchema, noId, unknownValue, dupId, or a <see cref="QuestIssue"/>/<see cref="DialogueIssue"/> code.</summary>
        public string Code { get; }

        /// <summary>"quest hammer/s2", "dialogue edge/n1"...</summary>
        public string Where { get; }

        public string Message { get; }

        public override string ToString() => $"[{Level}] {Where}: {Message} ({Code})";
    }

    /// <summary>
    /// A dialogue effect from a file: what happens when the player reaches the node or picks the answer. The kind is
    /// the game's (the action RPG kit reads its DialogueEffectKind names).
    /// </summary>
    public sealed class ContentEffect
    {
        public string DialogueId { get; set; }

        /// <summary>Set for an effect on a line.</summary>
        public string NodeId { get; set; }

        /// <summary>Set for an effect on an answer.</summary>
        public string ChoiceId { get; set; }

        public string Kind { get; set; }
        public string Target { get; set; }
        public int Amount { get; set; } = 1;
    }

    /// <summary>What <see cref="ContentImporter.Read"/> made of a file.</summary>
    public sealed class ContentPackage
    {
        public List<QuestData> Quests { get; } = new List<QuestData>();

        /// <summary>Only dialogues without duplicate node ids (they can be built into a <see cref="Dialogue.Dialogue"/>).</summary>
        public List<DialogueData> Dialogues { get; } = new List<DialogueData>();

        public List<ContentEffect> Effects { get; } = new List<ContentEffect>();
        public List<ContentProblem> Problems { get; } = new List<ContentProblem>();

        public bool HasErrors => Problems.Any(p => p.Level == ContentProblemLevel.Error);
    }

    /// <summary>
    /// Reads quests and dialogues from JSON (<see cref="Schema"/>, the shape of <see cref="ContentFile"/>) into the
    /// framework's <see cref="QuestData"/> and <see cref="DialogueData"/>, with effects for the game, and checks them
    /// (<see cref="QuestGraph"/>, <see cref="DialogueGraph"/>). It changes nothing: the game or the editor import adds
    /// the result. <see cref="Write"/> turns content back into the same JSON.
    /// </summary>
    public static class ContentImporter
    {
        public const string Schema = "rpgframework.content/1";

        private static readonly JsonSaveSerializer Json = new JsonSaveSerializer();

        /// <param name="knownQuest">Quests the game already has (for the quest a dialogue belongs to); null = only
        /// the file's own quests, and a dialogue's quest outside the file is a warning.</param>
        public static ContentPackage Read(string json, Func<string, bool> knownQuest = null)
        {
            var package = new ContentPackage();
            ContentFile file;
            try
            {
                file = Json.Deserialize<ContentFile>(Encoding.UTF8.GetBytes(json ?? ""));
            }
            catch (Exception exception)
            {
                package.Problems.Add(Error("badJson", "file", "is not valid JSON: " + (exception.InnerException ?? exception).Message));
                return package;
            }
            if (file == null)
            {
                package.Problems.Add(Error("badJson", "file", "is empty"));
                return package;
            }
            if (string.IsNullOrEmpty(file.schema))
                package.Problems.Add(Warning("badSchema", "file", $"has no schema; read as {Schema}"));
            else if (file.schema != Schema)
                package.Problems.Add(Error("badSchema", "file", $"is '{file.schema}'; this framework reads {Schema}"));

            foreach (var quest in file.quests ?? new List<QuestJson>())
            {
                if (quest == null)
                    continue;
                if (string.IsNullOrWhiteSpace(quest.id))
                {
                    package.Problems.Add(Error("noId", "quest " + (quest.title ?? "?"), "has no id"));
                    continue;
                }
                package.Quests.Add(ToQuest(quest, package.Problems));
            }
            foreach (var dialogue in file.dialogues ?? new List<DialogueJson>())
            {
                if (dialogue == null)
                    continue;
                if (string.IsNullOrWhiteSpace(dialogue.id))
                {
                    package.Problems.Add(Error("noId", "dialogue " + (dialogue.title ?? "?"), "has no id"));
                    continue;
                }
                var data = ToDialogue(dialogue, package);
                var duplicates = (data.Nodes ?? new DialogueNodeData[0]).GroupBy(n => n.Id).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
                if (duplicates.Count > 0)
                    package.Problems.Add(Error("dupNode", "dialogue " + dialogue.id, "two nodes have the id " + string.Join(", ", duplicates)));
                else
                    package.Dialogues.Add(data);
            }

            Check(package, knownQuest);
            return package;
        }

        /// <summary>The content as JSON in the <see cref="Schema"/> shape (effects go on their lines and answers).</summary>
        public static string Write(IEnumerable<QuestData> quests, IEnumerable<DialogueData> dialogues, IEnumerable<ContentEffect> effects = null)
        {
            var effectList = (effects ?? Enumerable.Empty<ContentEffect>()).ToList();
            var file = new ContentFile { schema = Schema };
            foreach (var quest in quests ?? Enumerable.Empty<QuestData>())
                file.quests.Add(FromQuest(quest));
            foreach (var dialogue in dialogues ?? Enumerable.Empty<DialogueData>())
                file.dialogues.Add(FromDialogue(dialogue, effectList));
            return Encoding.UTF8.GetString(Json.Serialize(file));
        }

        // --- quests

        private static QuestData ToQuest(QuestJson json, List<ContentProblem> problems)
        {
            string where = "quest " + json.id;
            return new QuestData
            {
                Id = json.id.Trim(),
                Title = json.title ?? "",
                Description = json.description ?? "",
                Type = json.type ?? "",
                MinimumLevel = Math.Max(0, json.minimumLevel),
                IsRepeatable = json.repeatable,
                RepeatCooldownSeconds = Math.Max(0d, json.cooldownSeconds),
                PrerequisiteQuestIds = Clean(json.prerequisites),
                Requirements = (json.requirements ?? new List<RequirementJson>()).Where(r => r != null)
                    .Select(r => new QuestRequirementData { Kind = (r.kind ?? "").Trim(), Id = r.id ?? "", Value = r.value }).ToArray(),
                Objectives = (json.objectives ?? new List<ObjectiveJson>()).Where(o => o != null).Select((o, i) => new QuestObjectiveData
                {
                    Id = string.IsNullOrWhiteSpace(o.id) ? "o" + (i + 1) : o.id.Trim(),
                    Type = Parse(o.type, ObjectiveType.Custom, where + " objective " + o.id, problems),
                    TargetId = o.target ?? "",
                    RequiredCount = Math.Max(1, o.count),
                    Description = o.description ?? "",
                    IsOptional = o.optional
                }).ToArray(),
                Rewards = (json.rewards ?? new List<RewardJson>()).Where(r => r != null).Select(r => new QuestRewardData
                {
                    Type = Parse(r.type, RewardType.Custom, where + " reward " + r.id, problems),
                    RewardId = r.id ?? "",
                    Amount = r.amount
                }).ToArray(),
                StartStageId = json.start ?? "",
                Stages = (json.stages ?? new List<StageJson>()).Where(s => s != null).Select(s => new QuestStageData
                {
                    Id = (s.id ?? "").Trim(),
                    Title = s.title ?? "",
                    Description = s.description ?? "",
                    ObjectiveIds = Clean(s.objectives),
                    Next = (s.next ?? new List<LinkJson>()).Where(l => l != null && !string.IsNullOrWhiteSpace(l.to))
                        .Select(l => new QuestStageLinkData { To = l.to.Trim(), Condition = (l.condition ?? "").Trim() }).ToArray(),
                    Outcome = Parse(s.outcome, QuestOutcome.None, where + "/" + s.id, problems)
                }).ToArray()
            };
        }

        private static QuestJson FromQuest(QuestData quest) => new QuestJson
        {
            id = quest.Id,
            title = quest.Title,
            description = quest.Description,
            type = quest.Type ?? "",
            minimumLevel = quest.MinimumLevel,
            repeatable = quest.IsRepeatable,
            cooldownSeconds = quest.RepeatCooldownSeconds,
            prerequisites = (quest.PrerequisiteQuestIds ?? new string[0]).ToList(),
            requirements = (quest.Requirements ?? new QuestRequirementData[0])
                .Select(r => new RequirementJson { kind = r.Kind, id = r.Id, value = r.Value }).ToList(),
            objectives = (quest.Objectives ?? new QuestObjectiveData[0]).Select(o => new ObjectiveJson
            {
                id = o.Id, type = Name(o.Type), target = o.TargetId, count = o.RequiredCount, description = o.Description, optional = o.IsOptional
            }).ToList(),
            rewards = (quest.Rewards ?? new QuestRewardData[0])
                .Select(r => new RewardJson { type = Name(r.Type), id = r.RewardId, amount = r.Amount }).ToList(),
            start = quest.StartStageId ?? "",
            stages = (quest.Stages ?? new QuestStageData[0]).Select(s => new StageJson
            {
                id = s.Id, title = s.Title, description = s.Description, objectives = (s.ObjectiveIds ?? new string[0]).ToList(),
                next = (s.Next ?? new QuestStageLinkData[0]).Select(l => new LinkJson { to = l.To, condition = l.Condition }).ToList(),
                outcome = Name(s.Outcome)
            }).ToList()
        };

        // --- dialogues

        private static DialogueData ToDialogue(DialogueJson json, ContentPackage package)
        {
            string id = json.id.Trim();
            string where = "dialogue " + id;
            var nodes = new List<DialogueNodeData>();
            foreach (var node in json.nodes ?? new List<NodeJson>())
            {
                if (node == null)
                    continue;
                if (string.IsNullOrWhiteSpace(node.id))
                {
                    package.Problems.Add(Error("noId", where, "a node has no id"));
                    continue;
                }
                string nodeId = node.id.Trim();
                var choices = new List<DialogueChoiceData>();
                int index = 0;
                foreach (var choice in node.choices ?? new List<ChoiceJson>())
                {
                    if (choice == null)
                        continue;
                    index++;
                    string choiceId = string.IsNullOrWhiteSpace(choice.id) ? "c" + index : choice.id.Trim();
                    choices.Add(new DialogueChoiceData
                    {
                        Id = choiceId,
                        Text = choice.text ?? "",
                        TextKey = choice.key ?? "",
                        TargetNodeId = (choice.to ?? "").Trim(),
                        Condition = ToCondition(choice.condition, where + "/" + nodeId + " answer " + choiceId, package.Problems)
                    });
                    foreach (var effect in choice.effects ?? new List<EffectJson>())
                        AddEffect(package, effect, id, null, choiceId);
                }
                var type = string.IsNullOrWhiteSpace(node.type)
                    ? (choices.Count > 0 ? DialogueNodeType.Choice : DialogueNodeType.Line)
                    : Parse(node.type, DialogueNodeType.Line, where + "/" + nodeId, package.Problems);
                nodes.Add(new DialogueNodeData
                {
                    Id = nodeId,
                    NodeType = type,
                    SpeakerId = node.speaker ?? "",
                    Text = node.text ?? "",
                    TextKey = node.key ?? "",
                    NextNodeIds = Clean(node.next),
                    Choices = choices.ToArray(),
                    Condition = ToCondition(node.condition, where + "/" + nodeId, package.Problems)
                });
                foreach (var effect in node.effects ?? new List<EffectJson>())
                    AddEffect(package, effect, id, nodeId, null);
            }
            return new DialogueData
            {
                Id = id,
                Title = json.title ?? "",
                Description = json.description ?? "",
                Type = json.type ?? "",
                QuestId = (json.quest ?? "").Trim(),
                QuestStageId = (json.stage ?? "").Trim(),
                ParticipantIds = Clean(json.participants),
                RootNodeId = string.IsNullOrWhiteSpace(json.start) ? nodes.FirstOrDefault().Id : json.start.Trim(),
                Nodes = nodes.ToArray()
            };
        }

        private static DialogueJson FromDialogue(DialogueData dialogue, List<ContentEffect> effects) => new DialogueJson
        {
            id = dialogue.Id,
            title = dialogue.Title,
            description = dialogue.Description,
            type = dialogue.Type ?? "",
            quest = dialogue.QuestId ?? "",
            stage = dialogue.QuestStageId ?? "",
            participants = (dialogue.ParticipantIds ?? new string[0]).ToList(),
            start = dialogue.RootNodeId,
            nodes = (dialogue.Nodes ?? new DialogueNodeData[0]).Select(n => new NodeJson
            {
                id = n.Id, type = Name(n.NodeType), speaker = n.SpeakerId, text = n.Text, key = n.TextKey ?? "",
                next = (n.NextNodeIds ?? new string[0]).ToList(),
                condition = FromCondition(n.Condition),
                choices = (n.Choices ?? new DialogueChoiceData[0]).Select(c => new ChoiceJson
                {
                    id = c.Id, text = c.Text, key = c.TextKey ?? "", to = c.TargetNodeId, condition = FromCondition(c.Condition),
                    effects = effects.Where(e => e.DialogueId == dialogue.Id && e.ChoiceId == c.Id).Select(FromEffect).ToList()
                }).ToList(),
                effects = effects.Where(e => e.DialogueId == dialogue.Id && e.NodeId == n.Id).Select(FromEffect).ToList()
            }).ToList()
        };

        private static readonly Dictionary<string, ConditionType> ConditionNames = new Dictionary<string, ConditionType>(StringComparer.OrdinalIgnoreCase)
        {
            ["quest"] = ConditionType.QuestComplete, ["questComplete"] = ConditionType.QuestComplete,
            ["stat"] = ConditionType.StatCheck, ["statCheck"] = ConditionType.StatCheck,
            ["item"] = ConditionType.ItemOwned, ["itemOwned"] = ConditionType.ItemOwned,
            ["variable"] = ConditionType.VariableCheck, ["variableCheck"] = ConditionType.VariableCheck, ["flag"] = ConditionType.VariableCheck,
            ["custom"] = ConditionType.Custom,
        };

        private static DialogueConditionData? ToCondition(ConditionJson json, string where, List<ContentProblem> problems)
        {
            if (json == null || string.IsNullOrWhiteSpace(json.type))
                return null;
            if (!ConditionNames.TryGetValue(json.type.Trim(), out var type))
            {
                problems.Add(Error("unknownValue", where, $"condition type '{json.type}' is not one of {string.Join(", ", ConditionNames.Keys)}"));
                return null;
            }
            return new DialogueConditionData { Type = type, ParameterId = json.id ?? "", RequiredValue = json.value };
        }

        private static ConditionJson FromCondition(DialogueConditionData? condition) =>
            condition.HasValue
                ? new ConditionJson { type = Name(condition.Value.Type), id = condition.Value.ParameterId, value = condition.Value.RequiredValue }
                : null;

        private static void AddEffect(ContentPackage package, EffectJson json, string dialogueId, string nodeId, string choiceId)
        {
            if (json == null || string.IsNullOrWhiteSpace(json.kind))
                return;
            package.Effects.Add(new ContentEffect
            {
                DialogueId = dialogueId, NodeId = nodeId, ChoiceId = choiceId, Kind = json.kind.Trim(), Target = json.target ?? "",
                Amount = json.amount
            });
        }

        private static EffectJson FromEffect(ContentEffect effect) =>
            new EffectJson { kind = effect.Kind, target = effect.Target, amount = effect.Amount };

        // --- checks

        private static void Check(ContentPackage package, Func<string, bool> knownQuest)
        {
            foreach (var duplicate in package.Quests.GroupBy(q => q.Id).Where(g => g.Count() > 1))
                package.Problems.Add(Error("dupId", "quest " + duplicate.Key, "two quests have this id"));
            foreach (var duplicate in package.Dialogues.GroupBy(d => d.Id).Where(g => g.Count() > 1))
                package.Problems.Add(Error("dupId", "dialogue " + duplicate.Key, "two dialogues have this id"));

            var quests = package.Quests.GroupBy(q => q.Id).ToDictionary(g => g.Key, g => g.First());
            foreach (var issue in QuestGraph.Check(quests.Values.Select(q => (IQuest)new Quest(q))))
            {
                // A prerequisite can be a quest the game has outside this file
                bool outside = issue.Code == "missingPrerequisite" && knownQuest != null && knownQuest(issue.Where);
                if (outside)
                    continue;
                var level = issue.Level == QuestIssueLevel.Error && !(issue.Code == "missingPrerequisite" && knownQuest == null)
                    ? ContentProblemLevel.Error : ContentProblemLevel.Warning;
                package.Problems.Add(new ContentProblem(level, issue.Code, "quest " + issue.QuestId + (issue.Where != null ? "/" + issue.Where : ""), issue.Message));
            }

            Func<string, bool> questExists = id => quests.ContainsKey(id) || (knownQuest != null && knownQuest(id));
            Func<string, string, bool> stageExists = (quest, stage) =>
                !quests.TryGetValue(quest, out var data) || (data.Stages ?? new QuestStageData[0]).Any(s => s.Id == stage);
            foreach (var dialogue in package.Dialogues.GroupBy(d => d.Id).Select(g => g.First()))
            {
                foreach (var issue in DialogueGraph.Check(new global::RPGFramework.Dialogue.Dialogue(dialogue), questExists, stageExists))
                {
                    var level = issue.Level == DialogueIssueLevel.Error && !(issue.Code == "missingQuest" && knownQuest == null)
                        ? ContentProblemLevel.Error : ContentProblemLevel.Warning;
                    package.Problems.Add(new ContentProblem(level, issue.Code,
                        "dialogue " + issue.DialogueId + (issue.NodeId != null ? "/" + issue.NodeId : ""), issue.Message));
                }
            }
        }

        // --- helpers

        private static T Parse<T>(string value, T fallback, string where, List<ContentProblem> problems) where T : struct
        {
            if (string.IsNullOrWhiteSpace(value))
                return fallback;
            if (Enum.TryParse(value.Trim(), true, out T parsed) && Enum.IsDefined(typeof(T), parsed))
                return parsed;
            problems.Add(Error("unknownValue", where, $"'{value}' is not one of {string.Join(", ", Enum.GetNames(typeof(T)).Select(Camel))}"));
            return fallback;
        }

        private static string Name<T>(T value) where T : struct => Camel(value.ToString());

        private static string Camel(string name) => string.IsNullOrEmpty(name) ? name : char.ToLowerInvariant(name[0]) + name.Substring(1);

        private static string[] Clean(List<string> values) =>
            (values ?? new List<string>()).Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim()).ToArray();

        private static ContentProblem Error(string code, string where, string message) =>
            new ContentProblem(ContentProblemLevel.Error, code, where, message);

        private static ContentProblem Warning(string code, string where, string message) =>
            new ContentProblem(ContentProblemLevel.Warning, code, where, message);
    }
}
