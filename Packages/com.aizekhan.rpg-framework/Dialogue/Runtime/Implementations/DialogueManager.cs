using System;
using System.Collections.Generic;
using System.Linq;
using RPGFramework.ECS;

namespace RPGFramework.Dialogue
{
    /// <summary>
    /// Implementation of IDialogueManager.
    /// ECS-based manager for dialogue progression and state.
    /// Supports multiple concurrent dialogues per entity.
    /// </summary>
    public class DialogueManager : IDialogueManager
    {
        private readonly IEntityManager _entityManager;
        private readonly IDialogueRegistry _dialogueRegistry;

        public DialogueManager(IEntityManager entityManager, IDialogueRegistry dialogueRegistry)
        {
            _entityManager = entityManager;
            _dialogueRegistry = dialogueRegistry;
        }

        /// <summary>
        /// Start a new dialogue for an entity.
        /// </summary>
        public bool StartDialogue(int entityId, string dialogueId)
        {
            // Get the dialogue definition
            var dialogue = _dialogueRegistry.GetDialogue(dialogueId);
            if (dialogue == null)
                return false;

            // Get or create ActiveDialoguesData
            if (!_entityManager.TryGetComponent<ActiveDialoguesData>(entityId, out var activeDialogues))
            {
                activeDialogues = new ActiveDialoguesData
                {
                    ActiveDialogues = new Dictionary<string, DialogueProgressData>(),
                    CompletedDialogueIds = new List<string>(),
                    TotalDialoguesCompleted = 0
                };
            }

            // Don't start if dialogue is already active
            if (activeDialogues.ActiveDialogues != null && activeDialogues.ActiveDialogues.ContainsKey(dialogueId))
                return false;

            // Create new dialogue progress
            var progress = new DialogueProgress(dialogueId, entityId, dialogue.RootNodeId);
            activeDialogues.ActiveDialogues[dialogueId] = progress.ToData();
            _entityManager.AddComponent(entityId, activeDialogues);

            // Emit dialogue started event
            var startEvent = new DialogueStartedEvent
            {
                EntityId = entityId,
                DialogueId = dialogueId,
                StartedAt = DateTime.UtcNow
            };
            // Note: Event publishing through ECS not yet implemented
            // Game layer can use C# events (DialogueStarted, etc.) instead

            return true;
        }

        /// <summary>
        /// Advance the dialogue to the next node.
        /// </summary>
        public bool AdvanceDialogue(int entityId, string? choiceId = null)
        {
            if (!_entityManager.TryGetComponent<ActiveDialoguesData>(entityId, out var activeDialogues))
                return false;

            if (activeDialogues.ActiveDialogues == null || activeDialogues.ActiveDialogues.Count == 0)
                return false;

            // Get the first active dialogue (simple implementation for single dialogue)
            var dialogueEntry = activeDialogues.ActiveDialogues.FirstOrDefault();
            if (dialogueEntry.Value.State != DialogueState.Active)
                return false;

            var dialogueId = dialogueEntry.Key;
            var progressData = dialogueEntry.Value;

            var dialogue = _dialogueRegistry.GetDialogue(dialogueId);
            if (dialogue == null)
                return false;

            var currentNode = dialogue.GetNode(progressData.CurrentNodeId);
            if (currentNode == null)
                return false;

            string nextNodeId = null;

            // If choice provided, use its target node
            if (choiceId != null)
            {
                var choice = currentNode.Choices.FirstOrDefault(c => c.Id == choiceId);
                if (choice == null)
                    return false;

                nextNodeId = choice.TargetNodeId;

                // Emit choice selected event
                var choiceEvent = new ChoiceSelectedEvent
                {
                    EntityId = entityId,
                    DialogueId = dialogueId,
                    NodeId = progressData.CurrentNodeId,
                    ChoiceId = choice.Id,
                    ChoiceText = choice.Text,
                    TargetNodeId = nextNodeId,
                    SelectedAt = DateTime.UtcNow
                };
                // Note: Event publishing through ECS not yet implemented
            }
            else
            {
                // Use first next node
                if (currentNode.NextNodeIds.Count == 0)
                    return false;

                nextNodeId = currentNode.NextNodeIds[0];

                // Emit line spoken event
                var lineEvent = new LineSpokenEvent
                {
                    EntityId = entityId,
                    DialogueId = dialogueId,
                    NodeId = nextNodeId,
                    SpeakerId = currentNode.SpeakerId,
                    Text = currentNode.Text,
                    SpokenAt = DateTime.UtcNow
                };
                // Note: Event publishing through ECS not yet implemented
            }

            // Move to next node
            var nextNode = dialogue.GetNode(nextNodeId);
            if (nextNode == null)
                return false;

            progressData.CurrentNodeId = nextNodeId;
            if (!progressData.VisitedNodeIds.Contains(nextNodeId))
            {
                progressData.VisitedNodeIds.Add(nextNodeId);
            }

            // Check if we reached an end node
            if (nextNode.NodeType == DialogueNodeType.End)
            {
                progressData.State = DialogueState.Completed;
                progressData.EndTime = DateTime.UtcNow;
                activeDialogues.ActiveDialogues.Remove(dialogueId);

                if (activeDialogues.CompletedDialogueIds == null)
                    activeDialogues.CompletedDialogueIds = new List<string>();

                activeDialogues.CompletedDialogueIds.Add(dialogueId);
                activeDialogues.TotalDialoguesCompleted++;

                // Emit dialogue ended event
                var endEvent = new DialogueEndedEvent
                {
                    EntityId = entityId,
                    DialogueId = dialogueId,
                    EndState = DialogueState.Completed,
                    EndedAt = DateTime.UtcNow
                };
                // Note: Event publishing through ECS not yet implemented
            }
            else
            {
                activeDialogues.ActiveDialogues[dialogueId] = progressData;
            }

            _entityManager.AddComponent(entityId, activeDialogues);
            return true;
        }

        /// <summary>
        /// End a dialogue for an entity.
        /// </summary>
        public void EndDialogue(int entityId)
        {
            if (!_entityManager.TryGetComponent<ActiveDialoguesData>(entityId, out var activeDialogues))
                return;

            if (activeDialogues.ActiveDialogues == null || activeDialogues.ActiveDialogues.Count == 0)
                return;

            // End the first active dialogue
            var dialogueEntry = activeDialogues.ActiveDialogues.FirstOrDefault();
            var dialogueId = dialogueEntry.Key;
            var progressData = dialogueEntry.Value;

            progressData.State = DialogueState.Interrupted;
            progressData.EndTime = DateTime.UtcNow;

            activeDialogues.ActiveDialogues.Remove(dialogueId);
            _entityManager.AddComponent(entityId, activeDialogues);

            // Emit dialogue ended event
            var endEvent = new DialogueEndedEvent
            {
                EntityId = entityId,
                DialogueId = dialogueId,
                EndState = DialogueState.Interrupted,
                EndedAt = DateTime.UtcNow
            };
            // Note: Event publishing through ECS not yet implemented
        }

        /// <summary>
        /// Get the current progress of a dialogue for an entity.
        /// </summary>
        public IDialogueProgress? GetDialogueProgress(int entityId)
        {
            if (!_entityManager.TryGetComponent<ActiveDialoguesData>(entityId, out var activeDialogues))
                return null;

            if (activeDialogues.ActiveDialogues == null || activeDialogues.ActiveDialogues.Count == 0)
                return null;

            var progressData = activeDialogues.ActiveDialogues.Values.FirstOrDefault();
            if (progressData.State != DialogueState.Active)
                return null;

            return new DialogueProgress(progressData);
        }

        /// <summary>
        /// Get the current node being displayed in a dialogue.
        /// </summary>
        public IDialogueNode? GetCurrentNode(int entityId)
        {
            var progress = GetDialogueProgress(entityId);
            if (progress == null)
                return null;

            var dialogue = _dialogueRegistry.GetDialogue(progress.DialogueId);
            if (dialogue == null)
                return null;

            return dialogue.GetNode(progress.CurrentNodeId);
        }

        /// <summary>
        /// Get the available choices for the current node.
        /// </summary>
        public IReadOnlyList<IDialogueChoice> GetAvailableChoices(int entityId)
        {
            var progress = GetDialogueProgress(entityId);
            if (progress == null)
                return new List<IDialogueChoice>();

            var dialogue = _dialogueRegistry.GetDialogue(progress.DialogueId);
            if (dialogue == null)
                return new List<IDialogueChoice>();

            var currentNode = dialogue.GetNode(progress.CurrentNodeId);
            if (currentNode == null || currentNode.Choices.Count == 0)
                return new List<IDialogueChoice>();

            // Filter to enabled choices
            var availableChoices = currentNode.Choices.Where(c => c.IsEnabled).ToList();
            return availableChoices.AsReadOnly();
        }
    }
}
