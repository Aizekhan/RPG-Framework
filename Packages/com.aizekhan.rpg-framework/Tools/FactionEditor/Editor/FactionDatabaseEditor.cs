using UnityEngine;
using UnityEditor;
using RPGFramework.Factions;

namespace RPGFramework.Tools.FactionEditor.Editor
{
    /// <summary>
    /// Inspector for a <see cref="FactionDatabase"/>: shows its problems first, then the faction list, the relationship
    /// list, and a grid where each cell sets how the row faction and the column faction feel about each other.
    /// </summary>
    [CustomEditor(typeof(FactionDatabase))]
    public class FactionDatabaseEditor : UnityEditor.Editor
    {
        private const float NameColumnWidth = 110f;
        private const float CellWidth = 95f;

        private bool showRelationshipList;
        private Vector2 matrixScroll;

        public override void OnInspectorGUI()
        {
            var database = (FactionDatabase)target;
            serializedObject.Update();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Faction Designer", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            DrawValidation(database);

            EditorGUILayout.Space(10);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("factions"), true);

            EditorGUILayout.Space(5);
            showRelationshipList = EditorGUILayout.Foldout(showRelationshipList, "Relationship List", true);
            if (showRelationshipList)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedObject.FindProperty("relationships"), true);
                EditorGUI.indentLevel--;
            }

            // Write list edits first: the grid below changes the asset directly
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(10);
            DrawMatrix(database);
        }

        private static void DrawValidation(FactionDatabase database)
        {
            var problems = database.Validate();

            if (problems.Count == 0)
            {
                EditorGUILayout.HelpBox("Ready", MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox(string.Join("\n", problems), MessageType.Error);
        }

        private void DrawMatrix(FactionDatabase database)
        {
            EditorGUILayout.LabelField("Relationships", EditorStyles.boldLabel);

            var ids = database.GetFactionIds();
            if (ids.Count < 2)
            {
                EditorGUILayout.HelpBox("Add at least two factions with IDs to set how they feel about each other.", MessageType.None);
                return;
            }

            EditorGUILayout.HelpBox("Each cell is how the row faction and the column faction feel about each other. Empty pairs are Neutral; Enemy and Rival factions fight in PvP.", MessageType.None);

            matrixScroll = EditorGUILayout.BeginScrollView(matrixScroll);

            // Header row: column faction IDs
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(NameColumnWidth + 4f);
            foreach (var column in ids)
                GUILayout.Label(new GUIContent(column, column), EditorStyles.miniBoldLabel, GUILayout.Width(CellWidth));
            EditorGUILayout.EndHorizontal();

            for (int row = 0; row < ids.Count; row++)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(new GUIContent(ids[row], ids[row]), EditorStyles.miniBoldLabel, GUILayout.Width(NameColumnWidth));

                for (int column = 0; column < ids.Count; column++)
                {
                    if (row == column)
                    {
                        GUILayout.Space(CellWidth + 4f);
                        continue;
                    }

                    var current = database.GetRelation(ids[row], ids[column]);
                    var chosen = (FactionRelationType)EditorGUILayout.EnumPopup(current, GUILayout.Width(CellWidth));
                    if (chosen != current)
                    {
                        Undo.RecordObject(database, "Set Faction Relation");
                        database.SetRelation(ids[row], ids[column], chosen);
                        EditorUtility.SetDirty(database);
                    }
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
        }
    }
}
