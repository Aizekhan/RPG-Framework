using UnityEditor;
using UnityEngine;
using RPGFramework.Dialogue;

namespace RPGFramework.Editor.CustomEditors
{
    /// <summary>
    /// Custom editor for DialogueData ScriptableObjects with node visualization.
    /// </summary>
    [CustomEditor(typeof(DialogueData))]
    public class DialogueDataEditor : UnityEditor.Editor
    {
        private bool showNodes = true;
        private Vector2 scrollPosition;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Dialogue Configuration", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            // Dialogue ID
            SerializedProperty dialogueIdProp = serializedObject.FindProperty("dialogueId");
            EditorGUILayout.PropertyField(dialogueIdProp);

            if (string.IsNullOrEmpty(dialogueIdProp.stringValue))
            {
                EditorGUILayout.HelpBox("Dialogue ID is required. Please enter a unique identifier.", MessageType.Error);
            }

            EditorGUILayout.Space(5);

            // Title
            SerializedProperty titleProp = serializedObject.FindProperty("title");
            EditorGUILayout.PropertyField(titleProp);

            // Start Node ID
            SerializedProperty startNodeProp = serializedObject.FindProperty("startNodeId");
            EditorGUILayout.PropertyField(startNodeProp);

            if (string.IsNullOrEmpty(startNodeProp.stringValue))
            {
                EditorGUILayout.HelpBox("Start Node ID is required. Specify which node begins the dialogue.", MessageType.Warning);
            }

            EditorGUILayout.Space(10);

            // Nodes
            SerializedProperty nodesProp = serializedObject.FindProperty("nodes");
            showNodes = EditorGUILayout.Foldout(showNodes, $"Dialogue Nodes ({nodesProp.arraySize})", true);

            if (showNodes)
            {
                EditorGUI.indentLevel++;

                if (nodesProp.arraySize == 0)
                {
                    EditorGUILayout.HelpBox("No dialogue nodes defined. Add nodes to create the dialogue tree.", MessageType.Warning);
                }
                else
                {
                    EditorGUILayout.HelpBox("Use the Dialogue Graph Editor window for visual editing: Tools > RPG Framework > Dialogue Graph Editor", MessageType.Info);
                }

                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(300));
                EditorGUILayout.PropertyField(nodesProp, true);
                EditorGUILayout.EndScrollView();

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(10);

            // Quick Actions
            EditorGUILayout.LabelField("Quick Actions", EditorStyles.boldLabel);

            if (GUILayout.Button("Open in Dialogue Graph Editor"))
            {
                EditorGUILayout.HelpBox("Dialogue Graph Editor window is planned for future implementation.", MessageType.Info);
            }

            if (GUILayout.Button("Validate Dialogue Tree"))
            {
                ValidateDialogueTree(nodesProp, startNodeProp.stringValue);
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void ValidateDialogueTree(SerializedProperty nodesProp, string startNodeId)
        {
            int nodeCount = nodesProp.arraySize;
            bool foundStartNode = false;
            bool hasErrors = false;

            for (int i = 0; i < nodeCount; i++)
            {
                SerializedProperty nodeProp = nodesProp.GetArrayElementAtIndex(i);
                SerializedProperty nodeIdProp = nodeProp.FindPropertyRelative("id");

                if (nodeIdProp != null && nodeIdProp.stringValue == startNodeId)
                {
                    foundStartNode = true;
                }

                if (nodeIdProp != null && string.IsNullOrEmpty(nodeIdProp.stringValue))
                {
                    Debug.LogError($"Node at index {i} has empty ID.", target);
                    hasErrors = true;
                }
            }

            if (!foundStartNode && !string.IsNullOrEmpty(startNodeId))
            {
                Debug.LogError($"Start node '{startNodeId}' not found in dialogue nodes.", target);
                hasErrors = true;
            }

            if (!hasErrors)
            {
                Debug.Log("Dialogue tree validation passed. No errors found.", target);
            }
        }
    }
}
