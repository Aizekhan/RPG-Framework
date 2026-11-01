using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RPGFramework.Dialogue;
using RPGFramework.Quests;
using RPGFramework.Tools.DialogueEditor;
using RPGFramework.Tools.QuestEditor;
using UnityEditor;
using UnityEngine;

namespace RPGFramework.ContentImport.Editor
{
    /// <summary>
    /// Turns a content file into Quest and Dialogue assets in a folder: an asset whose id is in the file is updated
    /// (anywhere in the project), a new id gets a new asset. Dialogue effects are not part of the Dialogue asset: an
    /// action RPG kit game lists the JSON file in its Game Setup (Content Files) instead, which keeps them.
    /// Menu: RPG Framework > Tools > Import Content (JSON)...; batch mode:
    /// <c>Unity -batchmode -quit -projectPath . -executeMethod RPGFramework.ContentImport.Editor.ContentAssetImport.ImportFromCommandLine -contentFile Content/quests.json [-contentFolder Assets/Content]</c>
    /// (exit code 0 imported, 1 the file has errors, nothing written, 2 failed).
    /// </summary>
    public static class ContentAssetImport
    {
        public const string DefaultFolder = "Assets/Content";

        public sealed class Result
        {
            public List<string> Created { get; } = new List<string>();
            public List<string> Updated { get; } = new List<string>();
            public List<ContentProblem> Problems { get; } = new List<ContentProblem>();
            public bool Imported { get; set; }
        }

        [MenuItem("RPG Framework/Tools/Import Content (JSON)...", false, 41)]
        public static void ImportFromMenu()
        {
            var path = EditorUtility.OpenFilePanel("Import quests and dialogues", "", "json");
            if (string.IsNullOrEmpty(path))
                return;
            var result = Import(File.ReadAllText(path), DefaultFolder);
            Report(path, result);
        }

        public static void ImportFromCommandLine()
        {
            try
            {
                var file = Argument("-contentFile");
                if (string.IsNullOrEmpty(file) || !File.Exists(file))
                    throw new FileNotFoundException("Pass -contentFile <path to a .json file>", file);
                var result = Import(File.ReadAllText(file), Argument("-contentFolder") ?? DefaultFolder);
                Report(file, result);
                EditorApplication.Exit(result.Imported ? 0 : 1);
            }
            catch (Exception exception)
            {
                Debug.LogError("[Content Import] Failed: " + exception);
                EditorApplication.Exit(2);
            }
        }

        /// <summary>Reads the JSON and, when it has no errors, writes the assets into <paramref name="folder"/>.</summary>
        public static Result Import(string json, string folder)
        {
            var result = new Result();
            var existingQuests = LoadAll<QuestAsset>();
            var package = ContentImporter.Read(json, id => existingQuests.Any(q => q.questId == id));
            result.Problems.AddRange(package.Problems);
            if (package.HasErrors)
                return result;

            EnsureFolder(folder);
            var quests = existingQuests.Where(q => !string.IsNullOrEmpty(q.questId)).GroupBy(q => q.questId)
                .ToDictionary(g => g.Key, g => g.First());
            foreach (var data in package.Quests)
            {
                bool found = quests.TryGetValue(data.Id, out var asset);
                if (!found)
                {
                    asset = ScriptableObject.CreateInstance<QuestAsset>();
                    AssetDatabase.CreateAsset(asset, AssetDatabase.GenerateUniqueAssetPath($"{folder}/{FileName(data.Id)}.asset"));
                    quests[data.Id] = asset;
                }
                Fill(asset, data);
                (found ? result.Updated : result.Created).Add(AssetDatabase.GetAssetPath(asset));
            }
            // Prerequisites are asset references: set once every quest of the file exists
            foreach (var data in package.Quests)
            {
                quests[data.Id].prerequisiteQuests = (data.PrerequisiteQuestIds ?? new string[0])
                    .Where(quests.ContainsKey).Select(id => quests[id]).ToArray();
                EditorUtility.SetDirty(quests[data.Id]);
            }

            var dialogues = LoadAll<DialogueAsset>().Where(d => !string.IsNullOrEmpty(d.dialogueId)).GroupBy(d => d.dialogueId)
                .ToDictionary(g => g.Key, g => g.First());
            foreach (var data in package.Dialogues)
            {
                bool found = dialogues.TryGetValue(data.Id, out var asset);
                if (!found)
                {
                    asset = ScriptableObject.CreateInstance<DialogueAsset>();
                    AssetDatabase.CreateAsset(asset, AssetDatabase.GenerateUniqueAssetPath($"{folder}/{FileName(data.Id)}.asset"));
                }
                Fill(asset, data);
                EditorUtility.SetDirty(asset);
                (found ? result.Updated : result.Created).Add(AssetDatabase.GetAssetPath(asset));
            }
            if (package.Effects.Count > 0)
                result.Problems.Add(new ContentProblem(ContentProblemLevel.Warning, "effectsNotInAssets", "file",
                    $"{package.Effects.Count} dialogue effect(s) are not stored in Dialogue assets; list the file in the Game Setup's Content Files to keep them"));
            AssetDatabase.SaveAssets();
            result.Imported = true;
            return result;
        }

        private static void Fill(QuestAsset asset, QuestData data)
        {
            asset.questId = data.Id;
            asset.title = data.Title;
            asset.description = data.Description;
            asset.questType = data.Type ?? "";
            asset.minimumLevel = Math.Max(1, data.MinimumLevel);
            asset.isRepeatable = data.IsRepeatable;
            asset.repeatCooldownHours = (float)(data.RepeatCooldownSeconds / 3600d);
            asset.requirements = (data.Requirements ?? new QuestRequirementData[0])
                .Select(r => new QuestRequirementEntry { kind = r.Kind, id = r.Id, value = r.Value }).ToArray();
            asset.objectives = (data.Objectives ?? new QuestObjectiveData[0]).Select(o => new QuestObjectiveEntry
            {
                objectiveId = o.Id, type = o.Type, targetId = o.TargetId, requiredCount = o.RequiredCount,
                description = o.Description, isOptional = o.IsOptional
            }).ToArray();
            asset.rewards = (data.Rewards ?? new QuestRewardData[0])
                .Select(r => new QuestRewardEntry { type = r.Type, rewardId = r.RewardId, amount = r.Amount }).ToArray();
            asset.startStage = data.StartStageId ?? "";
            asset.stages = (data.Stages ?? new QuestStageData[0]).Select(s => new QuestStageEntry
            {
                stageId = s.Id, title = s.Title, description = s.Description, objectiveIds = s.ObjectiveIds ?? new string[0],
                next = (s.Next ?? new QuestStageLinkData[0]).Select(l => new QuestLinkEntry { toStage = l.To, condition = l.Condition }).ToArray(),
                outcome = s.Outcome
            }).ToArray();
            EditorUtility.SetDirty(asset);
        }

        private static void Fill(DialogueAsset asset, DialogueData data)
        {
            asset.dialogueId = data.Id;
            asset.title = data.Title;
            asset.description = data.Description;
            asset.dialogueType = data.Type ?? "";
            asset.questId = data.QuestId ?? "";
            asset.questStageId = data.QuestStageId ?? "";
            asset.participantIds = (data.ParticipantIds ?? new string[0]).ToList();
            asset.startNodeId = data.RootNodeId;
            asset.nodes = (data.Nodes ?? new DialogueNodeData[0]).Select(n => new DialogueNodeEntry
            {
                nodeId = n.Id, nodeType = n.NodeType, speakerId = n.SpeakerId, text = n.Text, textKey = n.TextKey ?? "",
                nextNodeIds = (n.NextNodeIds ?? new string[0]).ToList(),
                choices = (n.Choices ?? new DialogueChoiceData[0]).Select(c => new DialogueChoiceEntry
                {
                    choiceId = c.Id, text = c.Text, textKey = c.TextKey ?? "", targetNodeId = c.TargetNodeId,
                    hasCondition = c.Condition.HasValue, condition = Condition(c.Condition)
                }).ToList(),
                hasCondition = n.Condition.HasValue,
                condition = Condition(n.Condition)
            }).ToList();
        }

        private static DialogueConditionEntry Condition(DialogueConditionData? data) =>
            data.HasValue
                ? new DialogueConditionEntry { type = data.Value.Type, parameterId = data.Value.ParameterId, requiredValue = data.Value.RequiredValue }
                : new DialogueConditionEntry();

        private static void Report(string file, Result result)
        {
            foreach (var problem in result.Problems)
            {
                if (problem.Level == ContentProblemLevel.Error)
                    Debug.LogError($"[Content Import] {problem}");
                else
                    Debug.LogWarning($"[Content Import] {problem}");
            }
            Debug.Log(result.Imported
                ? $"[Content Import] {file}: {result.Created.Count} created, {result.Updated.Count} updated\n" +
                  string.Join("\n", result.Created.Concat(result.Updated))
                : $"[Content Import] {file} has errors: nothing was written");
        }

        private static List<T> LoadAll<T>() where T : UnityEngine.Object =>
            AssetDatabase.FindAssets("t:" + typeof(T).Name)
                .Select(guid => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(asset => asset != null)
                .ToList();

        private static void EnsureFolder(string folder)
        {
            folder = folder.TrimEnd('/');
            if (AssetDatabase.IsValidFolder(folder))
                return;
            var parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        private static string FileName(string id) =>
            string.Concat(id.Select(c => Path.GetInvalidFileNameChars().Contains(c) || c == ':' ? '_' : c));

        private static string Argument(string name)
        {
            var args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, name);
            return at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
        }
    }
}
