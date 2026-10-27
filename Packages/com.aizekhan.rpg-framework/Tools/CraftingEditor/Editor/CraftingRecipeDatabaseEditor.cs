using System.Linq;
using UnityEditor;
using UnityEngine;

namespace RPGFramework.Tools.CraftingEditor.Editor
{
    /// <summary>
    /// Inspector for the crafting recipe database: shows whether all recipes are ready, then the list,
    /// with a button that collects every recipe asset in the project.
    /// </summary>
    [CustomEditor(typeof(CraftingRecipeDatabase))]
    public class CraftingRecipeDatabaseEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var database = (CraftingRecipeDatabase)target;
            serializedObject.Update();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Crafting Recipe Database", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            var errors = database.Validate();
            if (errors.Count == 0)
                EditorGUILayout.HelpBox($"Ready - {database.recipes.Count} recipe(s)", MessageType.Info);
            else
                EditorGUILayout.HelpBox(string.Join("\n", errors), MessageType.Error);

            EditorGUILayout.Space(10);
            DrawPropertiesExcluding(serializedObject, "m_Script");

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(10);
            if (GUILayout.Button("Find All Recipes In Project"))
            {
                Undo.RecordObject(database, "Find All Crafting Recipes");
                database.recipes = AssetDatabase.FindAssets("t:CraftingRecipeAsset")
                    .Select(guid => AssetDatabase.LoadAssetAtPath<CraftingRecipeAsset>(AssetDatabase.GUIDToAssetPath(guid)))
                    .Where(asset => asset != null)
                    .ToList();
                EditorUtility.SetDirty(database);
            }
        }
    }
}
