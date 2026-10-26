using System.Collections.Generic;
using System.Linq;

namespace RPGFramework.Dialogue
{
    /// <summary>
    /// Implementation of IDialogueRegistry.
    /// Simple dictionary-based registry for dialogue definitions.
    /// </summary>
    public class DialogueRegistry : IDialogueRegistry
    {
        private readonly Dictionary<string, IDialogue> _dialogues = new();

        /// <summary>
        /// Get a dialogue by ID.
        /// </summary>
        public IDialogue? GetDialogue(string dialogueId)
        {
            return _dialogues.TryGetValue(dialogueId, out var dialogue) ? dialogue : null;
        }

        /// <summary>
        /// Get all registered dialogues.
        /// </summary>
        public IReadOnlyList<IDialogue> GetAllDialogues()
        {
            return _dialogues.Values.ToList();
        }

        /// <summary>
        /// Register a dialogue definition.
        /// </summary>
        public void RegisterDialogue(IDialogue dialogue)
        {
            if (dialogue != null && !_dialogues.ContainsKey(dialogue.Id))
            {
                _dialogues[dialogue.Id] = dialogue;
            }
        }
    }
}
