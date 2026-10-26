using UnityEngine;
using UnityEditor;
using RPGFramework.Quests;
using System.IO;

namespace RPGFramework.Tools.QuestEditor.Editor
{
    /// <summary>
    /// Wizard for quick quest creation with templates.
    /// </summary>
    public class QuestCreationWizard : EditorWindow
    {
        private string questTitle = "New Quest";
        private string questDescription = "";
        private QuestTemplate selectedTemplate = QuestTemplate.Custom;
        private string savePath = "Assets/Quests";
        private bool addToDatabase = true;
        private QuestDatabase targetDatabase;

        private enum QuestTemplate
        {
            Custom,
            KillQuest,
            CollectionQuest,
            TalkToNPC,
            ExploreLocation
        }

        [MenuItem("RPG Framework/Tools/Quest Creation Wizard")]
        public static void ShowWindow()
        {
            var window = GetWindow<QuestCreationWizard>("Quest Wizard");
            window.minSize = new Vector2(400, 450);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Quest Creation Wizard", EditorStyles.boldLabel);
            EditorGUILayout.Space(10);

            EditorGUILayout.HelpBox(
                "Create a new quest asset quickly using templates or from scratch.",
                MessageType.Info);

            EditorGUILayout.Space(10);

            // Template selection
            EditorGUILayout.LabelField("Template", EditorStyles.boldLabel);
            selectedTemplate = (QuestTemplate)EditorGUILayout.EnumPopup("Quest Type", selectedTemplate);

            EditorGUILayout.Space(5);

            // Template description
            DrawTemplateDescription();

            EditorGUILayout.Space(10);

            // Basic info
            EditorGUILayout.LabelField("Quest Details", EditorStyles.boldLabel);
            questTitle = EditorGUILayout.TextField("Title", questTitle);
            EditorGUILayout.LabelField("Description");
            questDescription = EditorGUILayout.TextArea(questDescription, GUILayout.Height(60));

            EditorGUILayout.Space(10);

            // Save options
            EditorGUILayout.LabelField("Save Options", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Save Path", GUILayout.Width(70));
            savePath = EditorGUILayout.TextField(savePath);
            if (GUILayout.Button("Browse", GUILayout.Width(60)))
            {
                string path = EditorUtility.OpenFolderPanel("Select Quest Folder", "Assets", "");
                if (!string.IsNullOrEmpty(path))
                {
                    savePath = "Assets" + path.Substring(Application.dataPath.Length);
                }
            }
            EditorGUILayout.EndHorizontal();

            addToDatabase = EditorGUILayout.Toggle("Add to Database", addToDatabase);
            if (addToDatabase)
            {
                targetDatabase = (QuestDatabase)EditorGUILayout.ObjectField(
                    "Target Database",
                    targetDatabase,
                    typeof(QuestDatabase),
                    false);
            }

            EditorGUILayout.Space(20);

            // Create button
            GUI.enabled = !string.IsNullOrWhiteSpace(questTitle);
            if (GUILayout.Button("Create Quest", GUILayout.Height(40)))
            {
                CreateQuest();
            }
            GUI.enabled = true;

            EditorGUILayout.Space(10);
        }

        private void DrawTemplateDescription()
        {
            string description = selectedTemplate switch
            {
                QuestTemplate.KillQuest => "Kill X enemies\n• 1 Kill objective\n• Experience + Gold rewards",
                QuestTemplate.CollectionQuest => "Collect X items\n• 1 Collect objective\n• Item reward",
                QuestTemplate.TalkToNPC => "Talk to an NPC\n• 1 Interact objective\n• Experience reward",
                QuestTemplate.ExploreLocation => "Discover a location\n• 1 Explore objective\n• Experience reward",
                QuestTemplate.Custom => "Empty quest with minimal setup\n• Customize all fields manually",
                _ => ""
            };

            EditorGUILayout.HelpBox(description, MessageType.None);
        }

        private void CreateQuest()
        {
            // Ensure save path exists
            if (!Directory.Exists(savePath))
            {
                Directory.CreateDirectory(savePath);
            }

            // Create asset
            QuestAsset quest = CreateInstance<QuestAsset>();
            quest.questId = "quest_" + questTitle.ToLower().Replace(" ", "_");
            quest.title = questTitle;
            quest.description = string.IsNullOrWhiteSpace(questDescription)
                ? $"Complete the quest: {questTitle}"
                : questDescription;

            // Apply template
            ApplyTemplate(quest);

            // Save asset
            string fileName = questTitle.Replace(" ", "") + ".asset";
            string assetPath = Path.Combine(savePath, fileName);
            assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);

            AssetDatabase.CreateAsset(quest, assetPath);
            AssetDatabase.SaveAssets();

            // Add to database
            if (addToDatabase && targetDatabase != null)
            {
                Undo.RecordObject(targetDatabase, "Add Quest to Database");
                if (!targetDatabase.quests.Contains(quest))
                {
                    targetDatabase.quests.Add(quest);
                    targetDatabase.UpdateStatistics();
                    EditorUtility.SetDirty(targetDatabase);
                }
            }

            // Select and ping
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = quest;
            EditorGUIUtility.PingObject(quest);

            // Show confirmation
            EditorUtility.DisplayDialog(
                "Quest Created",
                $"Quest '{questTitle}' created successfully at:\n{assetPath}",
                "OK");

            // Reset form
            questTitle = "New Quest";
            questDescription = "";
            selectedTemplate = QuestTemplate.Custom;
        }

        private void ApplyTemplate(QuestAsset quest)
        {
            switch (selectedTemplate)
            {
                case QuestTemplate.KillQuest:
                    quest.objectives = new[]
                    {
                        new QuestObjectiveEntry
                        {
                            objectiveId = "objective_kill",
                            type = ObjectiveType.Kill,
                            targetId = "enemy_type",
                            requiredCount = 5,
                            description = "Kill 5 enemies"
                        }
                    };
                    quest.rewards = new[]
                    {
                        new QuestRewardEntry
                        {
                            type = RewardType.Experience,
                            rewardId = "reward_xp",
                            amount = 100
                        },
                        new QuestRewardEntry
                        {
                            type = RewardType.Currency,
                            rewardId = "reward_gold",
                            amount = 50
                        }
                    };
                    break;

                case QuestTemplate.CollectionQuest:
                    quest.objectives = new[]
                    {
                        new QuestObjectiveEntry
                        {
                            objectiveId = "objective_collect",
                            type = ObjectiveType.Collect,
                            targetId = "item_id",
                            requiredCount = 10,
                            description = "Collect 10 items"
                        }
                    };
                    quest.rewards = new[]
                    {
                        new QuestRewardEntry
                        {
                            type = RewardType.Item,
                            rewardId = "reward_item",
                            amount = 1
                        }
                    };
                    break;

                case QuestTemplate.TalkToNPC:
                    quest.objectives = new[]
                    {
                        new QuestObjectiveEntry
                        {
                            objectiveId = "objective_talk",
                            type = ObjectiveType.Interact,
                            targetId = "npc_id",
                            requiredCount = 1,
                            description = "Talk to NPC"
                        }
                    };
                    quest.rewards = new[]
                    {
                        new QuestRewardEntry
                        {
                            type = RewardType.Experience,
                            rewardId = "reward_xp",
                            amount = 50
                        }
                    };
                    break;

                case QuestTemplate.ExploreLocation:
                    quest.objectives = new[]
                    {
                        new QuestObjectiveEntry
                        {
                            objectiveId = "objective_explore",
                            type = ObjectiveType.Explore,
                            targetId = "location_id",
                            requiredCount = 1,
                            description = "Discover the location"
                        }
                    };
                    quest.rewards = new[]
                    {
                        new QuestRewardEntry
                        {
                            type = RewardType.Experience,
                            rewardId = "reward_xp",
                            amount = 75
                        }
                    };
                    break;

                case QuestTemplate.Custom:
                default:
                    quest.objectives = new[]
                    {
                        new QuestObjectiveEntry
                        {
                            objectiveId = "objective_1",
                            type = ObjectiveType.Kill,
                            targetId = "target",
                            requiredCount = 1,
                            description = "Complete the objective"
                        }
                    };
                    quest.rewards = new[]
                    {
                        new QuestRewardEntry
                        {
                            type = RewardType.Experience,
                            rewardId = "reward_xp",
                            amount = 100
                        }
                    };
                    break;
            }
        }
    }
}
