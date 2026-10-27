using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using RPGFramework.Dialogue;

namespace RPGFramework.Tools.DialogueEditor
{
    /// <summary>
    /// All the conversations of a game in one asset. <see cref="RegisterAll"/> hands them to the Dialogue module's
    /// registry so a dialogue manager can start any of them by id.
    /// </summary>
    [CreateAssetMenu(fileName = "DialogueDatabase", menuName = "RPG Framework/Dialogue Database", order = 105)]
    public class DialogueDatabase : ScriptableObject
    {
        [Header("Dialogue Registry")]
        [Tooltip("All conversations in the game")]
        public List<DialogueAsset> dialogues = new List<DialogueAsset>();

        /// <summary>
        /// The dialogue asset with this id, or null.
        /// </summary>
        public DialogueAsset GetDialogue(string dialogueId)
        {
            return dialogues?.FirstOrDefault(d => d != null && d.dialogueId == dialogueId);
        }

        /// <summary>
        /// Registers every filled slot with <paramref name="registry"/> and returns how many were registered. Empty slots
        /// and dialogues with problems (see <see cref="DialogueAsset.Validate"/>) are skipped; the registry keeps the
        /// first dialogue of an id.
        /// </summary>
        public int RegisterAll(IDialogueRegistry registry)
        {
            if (registry == null || dialogues == null)
                return 0;

            int registered = 0;
            foreach (var dialogue in dialogues)
            {
                if (dialogue == null || dialogue.Validate().Count > 0)
                    continue;

                if (registry.GetDialogue(dialogue.dialogueId) != null)
                    continue;

                registry.RegisterDialogue(dialogue.ToDialogue());
                registered++;
            }

            return registered;
        }

        /// <summary>
        /// Problems with the database and with each dialogue in it, in plain words. Empty when everything is ready.
        /// </summary>
        public List<string> ValidateDatabase()
        {
            var errors = new List<string>();

            if (dialogues == null || dialogues.Count == 0)
            {
                errors.Add("Database is empty");
                return errors;
            }

            int emptySlots = dialogues.Count(d => d == null);
            if (emptySlots > 0)
                errors.Add($"{emptySlots} empty dialogue slot(s) in the database");

            var duplicateIds = dialogues
                .Where(d => d != null)
                .GroupBy(d => d.dialogueId)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key);

            foreach (var id in duplicateIds)
                errors.Add($"Two dialogues use the ID '{id}': only the first one can be started");

            foreach (var dialogue in dialogues.Where(d => d != null))
            {
                foreach (var problem in dialogue.Validate())
                    errors.Add($"{dialogue.name}: {problem}");
            }

            return errors;
        }
    }
}
