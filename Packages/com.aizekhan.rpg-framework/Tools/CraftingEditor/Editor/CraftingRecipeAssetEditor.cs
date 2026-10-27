using UnityEditor;

namespace RPGFramework.Tools.CraftingEditor.Editor
{
    /// <summary>
    /// Inspector for a crafting recipe: shows whether it is ready, a one-line summary, then its fields.
    /// </summary>
    [CustomEditor(typeof(CraftingRecipeAsset))]
    public class CraftingRecipeAssetEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var recipe = (CraftingRecipeAsset)target;
            serializedObject.Update();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Crafting Recipe", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            var errors = recipe.Validate();
            if (errors.Count == 0)
                EditorGUILayout.HelpBox("Ready", MessageType.Info);
            else
                EditorGUILayout.HelpBox(string.Join("\n", errors), MessageType.Error);

            EditorGUILayout.Space(5);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(recipe.GetSummary(), EditorStyles.wordWrappedLabel);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);
            DrawPropertiesExcluding(serializedObject, "m_Script");

            serializedObject.ApplyModifiedProperties();
        }
    }
}
