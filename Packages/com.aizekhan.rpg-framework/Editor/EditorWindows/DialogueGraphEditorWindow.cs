// TODO: This Editor window needs refactoring to work with DialogueData struct instead of ScriptableObject
#if FALSE

using UnityEditor;
using UnityEngine;
using RPGFramework.Dialogue;
using System.Collections.Generic;

namespace RPGFramework.Editor.EditorWindows
{
    /// <summary>
    /// Visual dialogue graph editor for creating dialogue trees.
    /// </summary>
    public class DialogueGraphEditorWindow : EditorWindow
    {
        private DialogueData selectedDialogue;
        private Vector2 scrollPosition;
        private Vector2 graphOffset = Vector2.zero;
        private float zoomLevel = 1f;

        private List<DialogueData> availableDialogues = new List<DialogueData>();

        [MenuItem("RPG Framework/Tools/Dialogue Graph Editor", false, 2)]
        public static void ShowWindow()
        {
            DialogueGraphEditorWindow window = GetWindow<DialogueGraphEditorWindow>("Dialogue Graph Editor");
            window.minSize = new Vector2(900, 600);
            window.Show();
        }

        private void OnEnable()
        {
            RefreshDialogueList();
        }

        private void OnGUI()
        {
            DrawToolbar();

            if (selectedDialogue == null)
            {
                DrawNoDialogueSelected();
            }
            else
            {
                DrawDialogueGraph();
            }
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            // Dialogue selector
            EditorGUILayout.LabelField("Dialogue:", GUILayout.Width(60));

            int currentIndex = selectedDialogue != null ? availableDialogues.IndexOf(selectedDialogue) : 0;
            string[] dialogueNames = new string[availableDialogues.Count + 1];
            dialogueNames[0] = "None";

            for (int i = 0; i < availableDialogues.Count; i++)
            {
                dialogueNames[i + 1] = availableDialogues[i] != null ? availableDialogues[i].name : "Unknown";
            }

            int newIndex = EditorGUILayout.Popup(currentIndex + 1, dialogueNames, GUILayout.Width(200)) - 1;
            if (newIndex >= 0 && newIndex < availableDialogues.Count)
            {
                selectedDialogue = availableDialogues[newIndex];
            }
            else
            {
                selectedDialogue = null;
            }

            GUILayout.FlexibleSpace();

            // Tools
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                RefreshDialogueList();
            }

            if (GUILayout.Button("Create New", EditorStyles.toolbarButton, GUILayout.Width(80)))
            {
                CreateNewDialogue();
            }

            if (selectedDialogue != null)
            {
                if (GUILayout.Button("Add Node", EditorStyles.toolbarButton, GUILayout.Width(70)))
                {
                    // Add node functionality would go here
                    EditorUtility.DisplayDialog("Info", "Node creation functionality to be implemented.", "OK");
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawNoDialogueSelected()
        {
            GUILayout.FlexibleSpace();
            EditorGUILayout.BeginVertical();

            GUIStyle centeredStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 16
            };

            EditorGUILayout.LabelField("No Dialogue Selected", centeredStyle);
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Select a dialogue from the toolbar or create a new one", EditorStyles.centeredGreyMiniLabel);

            EditorGUILayout.Space(20);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Create New Dialogue", GUILayout.Width(200), GUILayout.Height(30)))
            {
                CreateNewDialogue();
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
            GUILayout.FlexibleSpace();
        }

        private void DrawDialogueGraph()
        {
            EditorGUILayout.BeginVertical();

            // Graph info
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"Editing: {selectedDialogue.name}", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Zoom: {zoomLevel:F2}x", GUILayout.Width(100));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // Graph area
            Rect graphRect = GUILayoutUtility.GetRect(position.width, position.height - 60);

            // Draw grid background
            DrawGrid(graphRect);

            // Draw dialogue nodes (simplified visualization)
            DrawDialogueNodes(graphRect);

            // Instructions
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Graph Editor Instructions:", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("• Visual node-based editing to be implemented", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("• Use Dialogue Data Editor in Inspector for now", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("• Drag to pan, scroll to zoom", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndVertical();
        }

        private void DrawGrid(Rect rect)
        {
            Color gridColor = new Color(0.5f, 0.5f, 0.5f, 0.2f);
            int gridSize = 50;

            // Vertical lines
            for (int i = 0; i < rect.width / gridSize; i++)
            {
                float x = rect.x + i * gridSize + (graphOffset.x % gridSize);
                DrawLine(new Vector2(x, rect.y), new Vector2(x, rect.y + rect.height), gridColor);
            }

            // Horizontal lines
            for (int i = 0; i < rect.height / gridSize; i++)
            {
                float y = rect.y + i * gridSize + (graphOffset.y % gridSize);
                DrawLine(new Vector2(rect.x, y), new Vector2(rect.x + rect.width, y), gridColor);
            }
        }

        private void DrawDialogueNodes(Rect graphRect)
        {
            if (selectedDialogue == null) return;

            SerializedObject serializedDialogue = new SerializedObject(selectedDialogue);
            SerializedProperty nodesProp = serializedDialogue.FindProperty("nodes");

            if (nodesProp == null || nodesProp.arraySize == 0)
            {
                GUIStyle style = new GUIStyle(EditorStyles.label)
                {
                    alignment = TextAnchor.MiddleCenter
                };
                GUI.Label(graphRect, "No dialogue nodes defined.\nAdd nodes using the Inspector.", style);
                return;
            }

            // Draw simplified node representation
            float nodeWidth = 150f;
            float nodeHeight = 60f;
            float spacing = 200f;

            for (int i = 0; i < Mathf.Min(nodesProp.arraySize, 10); i++)
            {
                SerializedProperty nodeProp = nodesProp.GetArrayElementAtIndex(i);
                SerializedProperty nodeIdProp = nodeProp.FindPropertyRelative("id");
                SerializedProperty textProp = nodeProp.FindPropertyRelative("text");

                float x = graphRect.x + 50 + (i % 3) * spacing + graphOffset.x;
                float y = graphRect.y + 50 + (i / 3) * 150 + graphOffset.y;

                Rect nodeRect = new Rect(x, y, nodeWidth, nodeHeight);

                // Draw node
                GUI.Box(nodeRect, "", EditorStyles.helpBox);

                GUIStyle labelStyle = new GUIStyle(EditorStyles.label)
                {
                    alignment = TextAnchor.UpperLeft,
                    wordWrap = true,
                    fontSize = 10
                };

                Rect labelRect = new Rect(x + 5, y + 5, nodeWidth - 10, nodeHeight - 10);
                string nodeId = nodeIdProp != null ? nodeIdProp.stringValue : $"Node {i}";
                string nodeText = textProp != null && textProp.stringValue.Length > 30
                    ? textProp.stringValue.Substring(0, 30) + "..."
                    : (textProp != null ? textProp.stringValue : "");

                GUI.Label(labelRect, $"{nodeId}\n{nodeText}", labelStyle);
            }

            if (nodesProp.arraySize > 10)
            {
                GUI.Label(new Rect(graphRect.x + 50, graphRect.y + graphRect.height - 30, 300, 20),
                    $"Showing 10 of {nodesProp.arraySize} nodes", EditorStyles.miniLabel);
            }
        }

        private void DrawLine(Vector2 start, Vector2 end, Color color)
        {
            Handles.BeginGUI();
            Handles.color = color;
            Handles.DrawLine(start, end);
            Handles.EndGUI();
        }

        private void RefreshDialogueList()
        {
            availableDialogues.Clear();
            string[] guids = AssetDatabase.FindAssets("t:DialogueData");

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                DialogueData dialogue = AssetDatabase.LoadAssetAtPath<DialogueData>(path);
                if (dialogue != null)
                {
                    availableDialogues.Add(dialogue);
                }
            }
        }

        private void CreateNewDialogue()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create New Dialogue",
                "NewDialogue",
                "asset",
                "Enter a name for the new dialogue"
            );

            if (!string.IsNullOrEmpty(path))
            {
                DialogueData newDialogue = ScriptableObject.CreateInstance<DialogueData>();
                AssetDatabase.CreateAsset(newDialogue, path);
                AssetDatabase.SaveAssets();

                RefreshDialogueList();
                selectedDialogue = newDialogue;

                EditorUtility.DisplayDialog("Success", "Dialogue created successfully!", "OK");
            }
        }
    }
}

#endif
