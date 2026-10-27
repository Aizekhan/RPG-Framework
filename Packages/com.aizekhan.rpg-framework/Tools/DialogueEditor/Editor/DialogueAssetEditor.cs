using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using RPGFramework.Dialogue;
using RPGFramework.Tools.DialogueEditor;

namespace RPGFramework.Tools.DialogueEditor.Editor
{
    /// <summary>
    /// Inspector for a <see cref="DialogueAsset"/>: what is wrong with the conversation at the top, then its fields,
    /// then a read-only outline of who says what and where each answer leads.
    /// </summary>
    [CustomEditor(typeof(DialogueAsset))]
    public class DialogueAssetEditor : UnityEditor.Editor
    {
        private bool showOutline = true;

        public override void OnInspectorGUI()
        {
            var dialogue = (DialogueAsset)target;
            serializedObject.Update();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Dialogue Designer", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            DrawValidation(dialogue);

            EditorGUILayout.Space(10);
            DrawProperty("dialogueId");
            DrawProperty("title");
            DrawProperty("description");
            DrawProperty("startNodeId");
            DrawProperty("nodes");

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(10);
            showOutline = EditorGUILayout.Foldout(showOutline, "Conversation Outline", true);
            if (showOutline)
            {
                EditorGUI.indentLevel++;
                DrawOutline(dialogue);
                EditorGUI.indentLevel--;
            }
        }

        private void DrawProperty(string propertyName)
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty(propertyName), true);
        }

        private static void DrawValidation(DialogueAsset dialogue)
        {
            var errors = dialogue.Validate();

            if (errors.Count == 0)
                EditorGUILayout.HelpBox("Ready: the conversation can be played from start to end", MessageType.Info);
            else
                EditorGUILayout.HelpBox(string.Join("\n", errors), MessageType.Error);
        }

        private static void DrawOutline(DialogueAsset dialogue)
        {
            var nodes = (dialogue.nodes ?? new List<DialogueNodeEntry>()).Where(n => n != null).ToList();
            if (nodes.Count == 0)
            {
                EditorGUILayout.LabelField("No nodes yet", EditorStyles.miniLabel);
                return;
            }

            var ids = new HashSet<string>(nodes.Select(n => n.nodeId));

            foreach (var node in nodes)
            {
                string start = node.nodeId == dialogue.startNodeId ? "  (start)" : "";
                string speaker = string.IsNullOrWhiteSpace(node.speakerId) ? "" : $"{node.speakerId}: ";
                EditorGUILayout.LabelField($"[{node.nodeType}] {node.nodeId}{start}", EditorStyles.boldLabel);

                EditorGUI.indentLevel++;
                if (!string.IsNullOrWhiteSpace(node.text))
                    EditorGUILayout.LabelField($"{speaker}\"{node.text}\"", EditorStyles.wordWrappedLabel);

                if (node.nodeType != DialogueNodeType.End)
                {
                    if (node.nextNodeIds != null)
                    {
                        foreach (var next in node.nextNodeIds)
                            EditorGUILayout.LabelField($"next -> {Target(next, ids)}");
                    }

                    if (node.choices != null)
                    {
                        foreach (var choice in node.choices.Where(c => c != null))
                            EditorGUILayout.LabelField($"\"{choice.text}\" -> {Target(choice.targetNodeId, ids)}", EditorStyles.wordWrappedLabel);
                    }
                }
                EditorGUI.indentLevel--;
            }
        }

        private static string Target(string nodeId, HashSet<string> ids)
        {
            if (string.IsNullOrWhiteSpace(nodeId))
                return "(nowhere)";
            return ids.Contains(nodeId) ? nodeId : $"{nodeId} (missing)";
        }
    }
}
