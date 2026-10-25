using UnityEditor;
using UnityEngine;
using RPGFramework.Crafting;

namespace RPGFramework.Editor.CustomEditors
{
    /// <summary>
    /// Custom editor for CraftingRecipeData with visual recipe display.
    /// </summary>
    [CustomEditor(typeof(CraftingRecipeData))]
    public class CraftingRecipeDataEditor : UnityEditor.Editor
    {
        private bool showIngredients = true;
        private bool showResults = true;
        private bool showRequirements = true;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Crafting Recipe Configuration", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            // Recipe ID
            SerializedProperty recipeIdProp = serializedObject.FindProperty("recipeId");
            EditorGUILayout.PropertyField(recipeIdProp);

            if (string.IsNullOrEmpty(recipeIdProp.stringValue))
            {
                EditorGUILayout.HelpBox("Recipe ID is required. Please enter a unique identifier.", MessageType.Error);
            }

            EditorGUILayout.Space(5);

            // Name
            SerializedProperty nameProp = serializedObject.FindProperty("name");
            EditorGUILayout.PropertyField(nameProp);

            // Description
            SerializedProperty descriptionProp = serializedObject.FindProperty("description");
            EditorGUILayout.LabelField("Description");
            descriptionProp.stringValue = EditorGUILayout.TextArea(descriptionProp.stringValue, GUILayout.Height(40));

            EditorGUILayout.Space(10);

            // Crafting Station Requirements
            EditorGUILayout.LabelField("Station Requirements", EditorStyles.boldLabel);
            SerializedProperty stationTypeProp = serializedObject.FindProperty("stationType");
            EditorGUILayout.PropertyField(stationTypeProp);

            SerializedProperty stationLevelProp = serializedObject.FindProperty("minimumStationLevel");
            EditorGUILayout.PropertyField(stationLevelProp);

            SerializedProperty craftingTimeProp = serializedObject.FindProperty("craftingTimeSeconds");
            EditorGUILayout.PropertyField(craftingTimeProp);

            EditorGUILayout.Space(10);

            // Ingredients
            SerializedProperty ingredientsProp = serializedObject.FindProperty("ingredients");
            showIngredients = EditorGUILayout.Foldout(showIngredients, $"Ingredients ({ingredientsProp.arraySize})", true);
            if (showIngredients)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(ingredientsProp, true);

                if (ingredientsProp.arraySize == 0)
                {
                    EditorGUILayout.HelpBox("Recipe has no ingredients. Add at least one ingredient.", MessageType.Warning);
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(10);

            // Results
            SerializedProperty resultsProp = serializedObject.FindProperty("results");
            showResults = EditorGUILayout.Foldout(showResults, $"Results ({resultsProp.arraySize})", true);
            if (showResults)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(resultsProp, true);

                if (resultsProp.arraySize == 0)
                {
                    EditorGUILayout.HelpBox("Recipe has no results. Add at least one result item.", MessageType.Error);
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(10);

            // Requirements
            SerializedProperty requirementsProp = serializedObject.FindProperty("requirements");
            showRequirements = EditorGUILayout.Foldout(showRequirements, $"Additional Requirements ({requirementsProp.arraySize})", true);
            if (showRequirements)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(requirementsProp, true);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(10);

            // Recipe Summary
            DrawRecipeSummary();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawRecipeSummary()
        {
            EditorGUILayout.LabelField("Recipe Summary", EditorStyles.boldLabel);

            SerializedProperty ingredientsProp = serializedObject.FindProperty("ingredients");
            SerializedProperty resultsProp = serializedObject.FindProperty("results");
            SerializedProperty craftingTimeProp = serializedObject.FindProperty("craftingTimeSeconds");

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.LabelField($"Ingredients: {ingredientsProp.arraySize} types");
            EditorGUILayout.LabelField($"Results: {resultsProp.arraySize} items");
            EditorGUILayout.LabelField($"Crafting Time: {craftingTimeProp.floatValue:F1} seconds");

            EditorGUILayout.EndVertical();
        }
    }
}
