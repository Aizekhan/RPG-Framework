using System;
using System.Collections.Generic;
using System.Linq;
using RPGFramework.ECS;
using RPGFramework.Events;

namespace RPGFramework.Dialogue
{
    /// <summary>
    /// Implementation of IDialogueManager.
    /// ECS-based manager for dialogue progression and state.
    /// Supports multiple concurrent dialogues per entity.
    /// <para>
    /// How the conversation moves on:
    /// - Conditions are checked against the context the game hands in (contextProvider, asked per entity).
    ///   Without a context, or when it returns null, every condition fails; things without a condition always pass.
    /// - Moving on without a choice goes to the first next node whose own condition passes.
    ///   This also applies to the start node: if its condition fails, the dialogue does not start.
    /// - A Branch node is never shown: the conversation passes through it at once.
    ///   If the Branch has its own condition it works as if/else: when it passes, all its next nodes are candidates;
    ///   when it fails, the first next node is skipped. Then the first candidate whose condition passes is taken.
    /// - If no way is open (or Branch nodes loop), the advance fails and nothing changes; the game can call EndDialogue.
    /// - Choices are offered and can be selected only when enabled and their condition passes.
    ///   The node a choice leads to is not checked again (the choice's condition is the gate).
    /// </para>
    /// <para>
    /// With an event publisher, raises DialogueStarted, LineSpoken (for every node arrived at except Branch nodes,
    /// with that node's speaker and text), ChoiceSelected (before the line it leads to) and DialogueEnded
    /// (Completed on reaching an End node, Interrupted from EndDialogue). A failed call raises nothing.
    /// </para>
    /// </summary>
    public class DialogueManager : IDialogueManager
    {
        private readonly IEntityManager _entityManager;
        private readonly IDialogueRegistry _dialogueRegistry;
        private readonly IEventPublisher _events;
        private readonly Func<int, IDialogueContext> _contextProvider;

        /// <param name="entityManager">Where each entity's dialogue state is stored.</param>
        /// <param name="dialogueRegistry">The dialogue definitions.</param>
        /// <param name="events">Optional; receives the dialogue events.</param>
        /// <param name="contextProvider">Optional; gives the context conditions are checked against for an entity.</param>
        public DialogueManager(
            IEntityManager entityManager,
            IDialogueRegistry dialogueRegistry,
            IEventPublisher events = null,
            Func<int, IDialogueContext> contextProvider = null)
        {
            _entityManager = entityManager;
            _dialogueRegistry = dialogueRegistry;
            _events = events;
            _contextProvider = contextProvider;
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

            if (activeDialogues.ActiveDialogues == null)
                activeDialogues.ActiveDialogues = new Dictionary<string, DialogueProgressData>();

            // Don't start if dialogue is already active
            if (activeDialogues.ActiveDialogues.ContainsKey(dialogueId))
                return false;

            // The start node must be open, and a Branch there is passed through (its own condition picks the way)
            var root = FindNode(dialogue, dialogue.RootNodeId);
            var context = GetContext(entityId);
            if (root == null || (root.NodeType != DialogueNodeType.Branch && !Passes(root.Condition, context)))
                return false;

            var path = new List<IDialogueNode>();
            var landing = Follow(dialogue, root, context, path);
            if (landing == null)
                return false;

            // Create new dialogue progress
            var progress = new DialogueProgress(dialogueId, entityId, dialogue.RootNodeId);
            foreach (var node in path)
                progress.AddVisitedNode(node.Id);
            var progressData = progress.ToData();
            progressData.CurrentNodeId = landing.Id;

            _events?.Publish(new DialogueStartedEvent
            {
                EntityId = entityId,
                DialogueId = dialogueId,
                StartedAt = DateTime.UtcNow
            });

            Arrive(entityId, dialogueId, ref activeDialogues, progressData, landing);
            return true;
        }

        /// <summary>
        /// Advance the dialogue to the next node.
        /// Without a choice, goes to the first next node whose condition passes.
        /// With a choice, the choice must be enabled and its condition must pass.
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

            var context = GetContext(entityId);
            var path = new List<IDialogueNode>();
            IDialogueNode landing;
            IDialogueChoice choice = null;

            if (choiceId != null)
            {
                // Only a choice the player could have been offered can be taken
                choice = currentNode.Choices.FirstOrDefault(c => c.Id == choiceId);
                if (choice == null || !IsAvailable(choice, context))
                    return false;

                var target = FindNode(dialogue, choice.TargetNodeId);
                if (target == null)
                    return false;

                landing = Follow(dialogue, target, context, path);
            }
            else
            {
                var next = PickNext(dialogue, currentNode, context);
                if (next == null)
                    return false;

                landing = Follow(dialogue, next, context, path);
            }

            if (landing == null)
                return false;

            // The move is valid: record it, then tell listeners
            progressData.CurrentNodeId = landing.Id;
            if (progressData.VisitedNodeIds == null)
                progressData.VisitedNodeIds = new List<string>();
            foreach (var node in path)
            {
                if (!progressData.VisitedNodeIds.Contains(node.Id))
                    progressData.VisitedNodeIds.Add(node.Id);
            }

            if (choice != null)
            {
                _events?.Publish(new ChoiceSelectedEvent
                {
                    EntityId = entityId,
                    DialogueId = dialogueId,
                    NodeId = currentNode.Id,
                    ChoiceId = choice.Id,
                    ChoiceText = choice.Text,
                    TargetNodeId = choice.TargetNodeId,
                    SelectedAt = DateTime.UtcNow
                });
            }

            Arrive(entityId, dialogueId, ref activeDialogues, progressData, landing);
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

            _events?.Publish(new DialogueEndedEvent
            {
                EntityId = entityId,
                DialogueId = dialogueId,
                EndState = DialogueState.Interrupted,
                EndedAt = DateTime.UtcNow
            });
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
        /// Get the available choices for the current node: enabled, and their condition passes.
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

            var context = GetContext(entityId);
            var availableChoices = currentNode.Choices.Where(c => IsAvailable(c, context)).ToList();
            return availableChoices.AsReadOnly();
        }

        /// <summary>
        /// Stores the new position, publishes the line arrived at, and finishes the dialogue on an End node.
        /// </summary>
        private void Arrive(int entityId, string dialogueId, ref ActiveDialoguesData activeDialogues,
            DialogueProgressData progressData, IDialogueNode landing)
        {
            bool finished = landing.NodeType == DialogueNodeType.End;

            if (finished)
            {
                progressData.State = DialogueState.Completed;
                progressData.EndTime = DateTime.UtcNow;
                activeDialogues.ActiveDialogues.Remove(dialogueId);

                if (activeDialogues.CompletedDialogueIds == null)
                    activeDialogues.CompletedDialogueIds = new List<string>();

                activeDialogues.CompletedDialogueIds.Add(dialogueId);
                activeDialogues.TotalDialoguesCompleted++;
            }
            else
            {
                activeDialogues.ActiveDialogues[dialogueId] = progressData;
            }

            _entityManager.AddComponent(entityId, activeDialogues);

            _events?.Publish(new LineSpokenEvent
            {
                EntityId = entityId,
                DialogueId = dialogueId,
                NodeId = landing.Id,
                SpeakerId = landing.SpeakerId,
                Text = landing.Text,
                SpokenAt = DateTime.UtcNow
            });

            if (finished)
            {
                _events?.Publish(new DialogueEndedEvent
                {
                    EntityId = entityId,
                    DialogueId = dialogueId,
                    EndState = DialogueState.Completed,
                    EndedAt = DateTime.UtcNow
                });
            }
        }

        /// <summary>
        /// Passes through Branch nodes starting at <paramref name="node"/> and returns the node the conversation
        /// stops at, or null when a Branch has no open way or Branch nodes loop. Every node passed is added to path.
        /// </summary>
        private IDialogueNode Follow(IDialogue dialogue, IDialogueNode node, IDialogueContext context, List<IDialogueNode> path)
        {
            int branchesPassed = 0;
            while (node != null && node.NodeType == DialogueNodeType.Branch)
            {
                // More Branch hops than there are nodes means they go round in a circle
                if (++branchesPassed > dialogue.Nodes.Count)
                    return null;

                path.Add(node);
                node = PickNext(dialogue, node, context);
            }

            if (node != null)
                path.Add(node);
            return node;
        }

        /// <summary>
        /// The first next node of <paramref name="node"/> whose condition passes, or null.
        /// A Branch whose own condition fails skips its first next node (if/else).
        /// </summary>
        private static IDialogueNode PickNext(IDialogue dialogue, IDialogueNode node, IDialogueContext context)
        {
            int first = 0;
            if (node.NodeType == DialogueNodeType.Branch && node.Condition != null && !Passes(node.Condition, context))
                first = 1;

            for (int i = first; i < node.NextNodeIds.Count; i++)
            {
                var candidate = FindNode(dialogue, node.NextNodeIds[i]);
                if (candidate != null && Passes(candidate.Condition, context))
                    return candidate;
            }

            return null;
        }

        /// <summary>
        /// The node with this id, or null (also for an empty id, which a dictionary lookup would throw on).
        /// </summary>
        private static IDialogueNode FindNode(IDialogue dialogue, string nodeId)
        {
            return string.IsNullOrEmpty(nodeId) ? null : dialogue.GetNode(nodeId);
        }

        private static bool IsAvailable(IDialogueChoice choice, IDialogueContext context)
        {
            return choice.IsEnabled && Passes(choice.Condition, context);
        }

        /// <summary>
        /// No condition always passes; a condition with no context to check against fails.
        /// </summary>
        private static bool Passes(IDialogueCondition condition, IDialogueContext context)
        {
            if (condition == null)
                return true;
            return context != null && condition.Evaluate(context);
        }

        private IDialogueContext GetContext(int entityId)
        {
            return _contextProvider?.Invoke(entityId);
        }
    }
}
