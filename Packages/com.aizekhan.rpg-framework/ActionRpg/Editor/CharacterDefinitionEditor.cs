using UnityEditor;
using UnityEngine;

namespace RPGFramework.ActionRpg.Editor
{
    /// <summary>
    /// Character inspector with a preview of the prefab and what the game adds to it.
    /// </summary>
    [CustomEditor(typeof(CharacterDefinition))]
    public sealed class CharacterDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var character = (CharacterDefinition)target;
            EditorGUILayout.LabelField("Id", character.Id, EditorStyles.boldLabel);

            DrawDefaultInspector();

            EditorGUILayout.Space();
            if (character.Prefab == null)
            {
                EditorGUILayout.HelpBox("No prefab: the game shows a capsule in the placeholder colour. Drag any model " +
                                        "prefab into 'Prefab' (pivot at the feet, facing +Z).", MessageType.Info);
                return;
            }

            var preview = AssetPreview.GetAssetPreview(character.Prefab);
            if (preview != null)
                GUILayout.Label(preview, GUILayout.Width(128), GUILayout.Height(128));
            else if (AssetPreview.IsLoadingAssetPreview(character.Prefab.GetInstanceID()))
                Repaint();

            var missing = new System.Collections.Generic.List<string>();
            if (character.Prefab.GetComponentInChildren<HealthBarView>(true) == null)
                missing.Add("a health bar");
            if (character.Prefab.GetComponent<StrikeAnimator>() == null)
                missing.Add("a strike lunge");
            if (character.Prefab.GetComponent<PlayerController>() == null && character.Prefab.GetComponent<EnemyBrain>() == null)
                missing.Add("movement (player controls or enemy AI, depending on its role)");
            if (missing.Count > 0)
                EditorGUILayout.HelpBox("Added in play mode: " + string.Join(", ", missing) +
                                        ". Add these components to the prefab yourself to tune them.", MessageType.None);
        }
    }
}
