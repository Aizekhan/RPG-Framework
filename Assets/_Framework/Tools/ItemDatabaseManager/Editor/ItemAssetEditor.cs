using UnityEngine;
using UnityEditor;
using RPGFramework.Tools.ItemDatabaseManager;
using RPGFramework.Items;

namespace RPGFramework.Tools.ItemDatabaseManager.Editor
{
    [CustomEditor(typeof(ItemAsset))]
    public class ItemAssetEditor : UnityEditor.Editor
    {
        private bool showStatModifiers = true;
        private bool showValidation = true;

        public override void OnInspectorGUI()
        {
            var item = (ItemAsset)target;
            serializedObject.Update();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Item Designer", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            // Identity
            DrawProperty("itemId");
            DrawProperty("itemName");
            DrawProperty("description");
            DrawProperty("iconId");

            EditorGUILayout.Space(10);

            // Properties
            EditorGUILayout.LabelField("Item Properties", EditorStyles.boldLabel);
            DrawProperty("itemType");
            DrawProperty("rarity");
            DrawProperty("quality");
            DrawProperty("maxStackSize");
            DrawProperty("itemLevel");

            EditorGUILayout.Space(10);

            // Type-specific properties
            if (item.itemType == ItemType.Equipment)
            {
                EditorGUILayout.LabelField("Equipment Properties", EditorStyles.boldLabel);
                DrawProperty("equipmentSlot");

                showStatModifiers = EditorGUILayout.Foldout(showStatModifiers, "Stat Modifiers", true);
                if (showStatModifiers)
                {
                    EditorGUI.indentLevel++;
                    DrawStatModifiers(item);
                    EditorGUI.indentLevel--;
                }
            }
            else if (item.itemType == ItemType.Consumable)
            {
                EditorGUILayout.LabelField("Consumable Properties", EditorStyles.boldLabel);
                DrawProperty("useDuration");
                DrawProperty("cooldown");
                DrawProperty("consumeOnUse");
            }

            EditorGUILayout.Space(10);

            // Validation
            showValidation = EditorGUILayout.Foldout(showValidation, "Validation", true);
            if (showValidation)
            {
                EditorGUI.indentLevel++;
                DrawValidation(item);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(10);
            DrawActions(item);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawProperty(string propertyName)
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty(propertyName));
        }

        private void DrawStatModifiers(ItemAsset item)
        {
            var statsProp = serializedObject.FindProperty("statModifiers");

            for (int i = 0; i < item.statModifiers.Length; i++)
            {
                EditorGUILayout.BeginHorizontal("box");
                var statProp = statsProp.GetArrayElementAtIndex(i);
                EditorGUILayout.PropertyField(statProp.FindPropertyRelative("statName"), GUIContent.none, GUILayout.Width(120));
                EditorGUILayout.PropertyField(statProp.FindPropertyRelative("value"), GUIContent.none, GUILayout.Width(60));

                if (GUILayout.Button("×", GUILayout.Width(25)))
                {
                    Undo.RecordObject(item, "Remove Stat");
                    var list = new System.Collections.Generic.List<StatModifier>(item.statModifiers);
                    list.RemoveAt(i);
                    item.statModifiers = list.ToArray();
                    EditorUtility.SetDirty(item);
                    GUIUtility.ExitGUI();
                }
                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("+ Add Stat Modifier"))
            {
                Undo.RecordObject(item, "Add Stat");
                var list = new System.Collections.Generic.List<StatModifier>(item.statModifiers);
                list.Add(new StatModifier());
                item.statModifiers = list.ToArray();
                EditorUtility.SetDirty(item);
            }
        }

        private void DrawValidation(ItemAsset item)
        {
            var errors = item.Validate();

            if (errors.Count == 0)
            {
                EditorGUILayout.HelpBox("✓ Item is valid", MessageType.Info);
            }
            else
            {
                foreach (var error in errors)
                {
                    EditorGUILayout.HelpBox(error, MessageType.Error);
                }
            }
        }

        private void DrawActions(ItemAsset item)
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Generate Item ID"))
            {
                Undo.RecordObject(item, "Generate Item ID");
                item.itemId = "item_" + item.itemName.ToLower().Replace(" ", "_");
                EditorUtility.SetDirty(item);
            }

            if (GUILayout.Button("Preview IItem"))
            {
                var itemInterface = item.ToItem();
                Debug.Log($"Item Preview:\n  ID: {itemInterface.ItemId}\n  Name: {itemInterface.Name}\n  Type: {itemInterface.ItemType}\n  Rarity: {itemInterface.Rarity}");
            }

            EditorGUILayout.EndHorizontal();
        }
    }
}
