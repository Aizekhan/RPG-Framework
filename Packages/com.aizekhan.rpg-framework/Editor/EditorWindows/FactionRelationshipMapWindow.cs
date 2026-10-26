// TODO: This Editor window needs refactoring - Faction is a runtime class, not a ScriptableObject asset
#if FALSE

using UnityEditor;
using UnityEngine;
using RPGFramework.Factions;
using System.Collections.Generic;

namespace RPGFramework.Editor.EditorWindows
{
    /// <summary>
    /// Visual faction relationship map for viewing faction standings and relationships.
    /// </summary>
    public class FactionRelationshipMapWindow : EditorWindow
    {
        private List<Faction> allFactions = new List<Faction>();
        private Vector2 scrollPosition;
        private Faction selectedFaction;

        [MenuItem("RPG Framework/Tools/Faction Relationship Map", false, 4)]
        public static void ShowWindow()
        {
            FactionRelationshipMapWindow window = GetWindow<FactionRelationshipMapWindow>("Faction Relationships");
            window.minSize = new Vector2(700, 500);
            window.Show();
        }

        private void OnEnable()
        {
            RefreshFactionList();
        }

        private void OnGUI()
        {
            DrawHeader();

            if (allFactions.Count == 0)
            {
                EditorGUILayout.HelpBox("No factions found in the project.", MessageType.Info);
                if (GUILayout.Button("Refresh"))
                {
                    RefreshFactionList();
                }
                return;
            }

            EditorGUILayout.BeginHorizontal();

            // Left panel - Faction list
            DrawFactionListPanel();

            // Right panel - Relationship visualization
            DrawRelationshipPanel();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Faction Relationship Map", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Total Factions: {allFactions.Count}", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);
        }

        private void DrawFactionListPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(250));

            EditorGUILayout.LabelField("Factions", EditorStyles.boldLabel);

            if (GUILayout.Button("Refresh Factions"))
            {
                RefreshFactionList();
            }

            EditorGUILayout.Space(5);

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            foreach (var faction in allFactions)
            {
                if (faction == null) continue;

                bool isSelected = selectedFaction == faction;
                Color originalBg = GUI.backgroundColor;

                if (isSelected)
                {
                    GUI.backgroundColor = new Color(0.5f, 0.7f, 1f);
                }

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                if (GUILayout.Button(faction.Name, EditorStyles.label))
                {
                    selectedFaction = faction;
                }

                // Show faction type and active status
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Type: {faction.FactionType}", EditorStyles.miniLabel);
                EditorGUILayout.LabelField(faction.IsActive ? "Active" : "Inactive", EditorStyles.miniLabel, GUILayout.Width(60));
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndVertical();

                GUI.backgroundColor = originalBg;
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.EndVertical();
        }

        private void DrawRelationshipPanel()
        {
            EditorGUILayout.BeginVertical();

            if (selectedFaction == null)
            {
                EditorGUILayout.HelpBox("Select a faction to view relationships.", MessageType.Info);
            }
            else
            {
                DrawFactionDetails();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawFactionDetails()
        {
            EditorGUILayout.LabelField($"Faction: {selectedFaction.Name}", EditorStyles.boldLabel);
            EditorGUILayout.Space(10);

            // Basic Info
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Basic Information", EditorStyles.boldLabel);

            EditorGUILayout.LabelField($"ID: {selectedFaction.FactionId}");
            EditorGUILayout.LabelField($"Type: {selectedFaction.FactionType}");
            EditorGUILayout.LabelField($"Default Reputation: {selectedFaction.DefaultReputation}");
            EditorGUILayout.LabelField($"Active: {(selectedFaction.IsActive ? "Yes" : "No")}");

            EditorGUILayout.Space(5);

            EditorGUILayout.LabelField("Description:", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(selectedFaction.Description, EditorStyles.wordWrappedLabel);

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // Reputation Visualization
            DrawReputationVisualization();

            EditorGUILayout.Space(10);

            // Relationship Map
            DrawRelationshipMap();
        }

        private void DrawReputationVisualization()
        {
            EditorGUILayout.LabelField("Default Reputation Breakdown", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            int reputation = selectedFaction.DefaultReputation;
            FactionStanding standing = GetStandingFromReputation(reputation);

            EditorGUILayout.LabelField($"Standing: {standing}");

            // Draw reputation bar
            Rect barRect = GUILayoutUtility.GetRect(18, 25);
            float normalizedRep = (reputation + 100) / 200f; // Assuming -100 to +100 range
            Color repColor = GetReputationColor(reputation);

            EditorGUI.DrawRect(barRect, new Color(0.2f, 0.2f, 0.2f));
            Rect fillRect = new Rect(barRect.x, barRect.y, barRect.width * normalizedRep, barRect.height);
            EditorGUI.DrawRect(fillRect, repColor);

            GUIStyle centeredStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
            GUI.Label(barRect, $"{reputation} / 100", centeredStyle);

            // Standing thresholds
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Standing Thresholds:", EditorStyles.boldLabel);

            DrawStandingThreshold("Exalted", 90, 100);
            DrawStandingThreshold("Revered", 75, 89);
            DrawStandingThreshold("Honored", 50, 74);
            DrawStandingThreshold("Friendly", 25, 49);
            DrawStandingThreshold("Neutral", -24, 24);
            DrawStandingThreshold("Unfriendly", -49, -25);
            DrawStandingThreshold("Hostile", -74, -50);
            DrawStandingThreshold("Hated", -100, -75);

            EditorGUILayout.EndVertical();
        }

        private void DrawStandingThreshold(string name, int min, int max)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"  {name}:", GUILayout.Width(100));
            EditorGUILayout.LabelField($"{min} to {max}", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawRelationshipMap()
        {
            EditorGUILayout.LabelField("Relationships with Other Factions", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.HelpBox(
                "Faction relationship system would display:\n" +
                "- Allied factions\n" +
                "- Neutral factions\n" +
                "- Enemy factions\n\n" +
                "This requires a FactionRelationship component to be implemented.",
                MessageType.Info
            );

            // List other factions for reference
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Other Factions:", EditorStyles.boldLabel);

            foreach (var faction in allFactions)
            {
                if (faction == null || faction == selectedFaction) continue;

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"  • {faction.Name}", GUILayout.Width(200));
                EditorGUILayout.LabelField($"({faction.FactionType})", EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        private FactionStanding GetStandingFromReputation(int reputation)
        {
            if (reputation < -75) return FactionStanding.Hated;
            if (reputation < -50) return FactionStanding.Hostile;
            if (reputation < -25) return FactionStanding.Unfriendly;
            if (reputation < 25) return FactionStanding.Neutral;
            if (reputation < 50) return FactionStanding.Friendly;
            if (reputation < 75) return FactionStanding.Honored;
            if (reputation < 90) return FactionStanding.Revered;
            return FactionStanding.Exalted;
        }

        private Color GetReputationColor(int reputation)
        {
            FactionStanding standing = GetStandingFromReputation(reputation);
            return standing switch
            {
                FactionStanding.Hated => new Color(0.8f, 0.1f, 0.1f),
                FactionStanding.Hostile => new Color(0.9f, 0.3f, 0.2f),
                FactionStanding.Unfriendly => new Color(0.9f, 0.5f, 0.3f),
                FactionStanding.Neutral => new Color(0.7f, 0.7f, 0.5f),
                FactionStanding.Friendly => new Color(0.5f, 0.8f, 0.5f),
                FactionStanding.Honored => new Color(0.3f, 0.7f, 0.9f),
                FactionStanding.Revered => new Color(0.5f, 0.4f, 0.9f),
                FactionStanding.Exalted => new Color(0.9f, 0.7f, 0.2f),
                _ => Color.white
            };
        }

        private void RefreshFactionList()
        {
            allFactions.Clear();
            string[] guids = AssetDatabase.FindAssets("t:Faction");

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Faction faction = AssetDatabase.LoadAssetAtPath<Faction>(path);
                if (faction != null)
                {
                    allFactions.Add(faction);
                }
            }
        }
    }
}

#endif
