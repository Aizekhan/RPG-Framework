using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using RPGFramework.Dialogue;

namespace RPGFramework.Tools.DialogueEditor
{
    /// <summary>
    /// A conversation written in the inspector: a list of nodes (lines, player choices, branches and ends) that point at
    /// each other by id. <see cref="ToDialogue"/> turns it into the dialogue the Dialogue module plays, and
    /// <see cref="Validate"/> lists what would stop it from playing to the end.
    /// </summary>
    [CreateAssetMenu(fileName = "NewDialogue", menuName = "RPG Framework/Dialogue", order = 104)]
    public class DialogueAsset : ScriptableObject
    {
        [Header("Dialogue Identity")]
        [Tooltip("Unique id the game uses to start this conversation (e.g. 'dialogue_blacksmith_greeting')")]
        public string dialogueId = "dialogue_new";

        [Tooltip("Name of the conversation, for menus and logs")]
        public string title = "New Dialogue";

        [Tooltip("What this conversation is about (notes for the team)")]
        [TextArea(2, 4)]
        public string description = "";

        [Header("Flow")]
        [Tooltip("Id of the node the conversation starts with")]
        public string startNodeId = "start";

        [Tooltip("Every line, choice, branch and ending of the conversation")]
        public List<DialogueNodeEntry> nodes = new List<DialogueNodeEntry>();

        /// <summary>
        /// The conversation as the Dialogue module's plain data. Empty node, choice and next-node slots are left out.
        /// </summary>
        public DialogueData ToDialogueData()
        {
            return new DialogueData
            {
                Id = dialogueId,
                Title = title,
                Description = description,
                RootNodeId = startNodeId,
                Nodes = (nodes ?? new List<DialogueNodeEntry>())
                    .Where(n => n != null)
                    .Select(n => n.ToNodeData())
                    .ToArray()
            };
        }

        /// <summary>
        /// The conversation ready to register with an <see cref="IDialogueRegistry"/> and play with a dialogue manager.
        /// Check <see cref="Validate"/> first: two nodes with the same id cannot be turned into a dialogue.
        /// </summary>
        public IDialogue ToDialogue()
        {
            // Written in full: inside RPGFramework.* the bare name "Dialogue" means the RPGFramework.Dialogue namespace
            return global::RPGFramework.Dialogue.Dialogue.FromData(ToDialogueData());
        }

        /// <summary>
        /// Problems that would break the conversation, in plain words. Empty when the dialogue is ready to play.
        /// </summary>
        public List<string> Validate()
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(dialogueId))
                errors.Add("Dialogue ID is empty: the game starts a conversation by its id");

            var entries = nodes ?? new List<DialogueNodeEntry>();
            if (entries.Count == 0)
            {
                errors.Add("The dialogue has no nodes");
                return errors;
            }

            // Node ids: every node needs one, and no two nodes may share one
            var byId = new Dictionary<string, DialogueNodeEntry>();
            for (int i = 0; i < entries.Count; i++)
            {
                var node = entries[i];
                if (node == null)
                {
                    errors.Add($"Node #{i + 1} is empty");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(node.nodeId))
                {
                    errors.Add($"Node #{i + 1} has no ID");
                    continue;
                }

                if (byId.ContainsKey(node.nodeId))
                    errors.Add($"Two nodes use the ID '{node.nodeId}': every node needs its own ID");
                else
                    byId.Add(node.nodeId, node);
            }

            // Start node
            if (string.IsNullOrWhiteSpace(startNodeId))
                errors.Add("Start node is not set");
            else if (!byId.ContainsKey(startNodeId))
                errors.Add($"Start node '{startNodeId}' does not exist");
            else if (byId[startNodeId].nodeType == DialogueNodeType.End)
                errors.Add($"Start node '{startNodeId}' is an End node: the conversation would stop before it starts");

            // Each node's own links
            foreach (var node in byId.Values)
                CheckNode(node, byId, errors);

            // Reachability: only meaningful once the start node exists
            if (!string.IsNullOrWhiteSpace(startNodeId) && byId.ContainsKey(startNodeId))
            {
                var reached = Reachable(startNodeId, byId);

                foreach (var node in byId.Values)
                {
                    if (!reached.Contains(node.nodeId))
                        errors.Add($"Node '{node.nodeId}' can never be reached from the start node");
                }

                if (!reached.Any(id => byId[id].nodeType == DialogueNodeType.End))
                    errors.Add("No End node can be reached from the start: the conversation never finishes");
            }

            return errors;
        }

        private static void CheckNode(DialogueNodeEntry node, Dictionary<string, DialogueNodeEntry> byId, List<string> errors)
        {
            var next = node.nextNodeIds ?? new List<string>();
            var choices = node.choices ?? new List<DialogueChoiceEntry>();

            if (node.nodeType == DialogueNodeType.End)
            {
                if (next.Count > 0 || choices.Count > 0)
                    errors.Add($"End node '{node.nodeId}' has next nodes or choices that are never used: the conversation stops here");
                return;
            }

            if (node.nodeType == DialogueNodeType.Choice && choices.Count == 0)
                errors.Add($"Choice node '{node.nodeId}' has no choices");

            if (next.Count == 0 && choices.Count == 0)
                errors.Add($"Node '{node.nodeId}' is a dead end: give it a next node or a choice, or make it an End node");

            foreach (var target in next)
            {
                if (string.IsNullOrWhiteSpace(target))
                    errors.Add($"Node '{node.nodeId}' has an empty next node slot");
                else if (!byId.ContainsKey(target))
                    errors.Add($"Node '{node.nodeId}' continues to '{target}', which does not exist");
            }

            if (node.hasCondition && string.IsNullOrWhiteSpace(node.condition?.parameterId))
                errors.Add($"Node '{node.nodeId}' has a condition with nothing to check (empty parameter)");

            var choiceIds = new HashSet<string>();
            for (int i = 0; i < choices.Count; i++)
            {
                var choice = choices[i];
                if (choice == null)
                {
                    errors.Add($"Node '{node.nodeId}': choice #{i + 1} is empty");
                    continue;
                }

                string label = string.IsNullOrWhiteSpace(choice.choiceId) ? $"#{i + 1}" : $"'{choice.choiceId}'";

                if (string.IsNullOrWhiteSpace(choice.choiceId))
                    errors.Add($"Node '{node.nodeId}': choice #{i + 1} has no ID");
                else if (!choiceIds.Add(choice.choiceId))
                    errors.Add($"Node '{node.nodeId}': two choices use the ID '{choice.choiceId}'");

                if (string.IsNullOrWhiteSpace(choice.text))
                    errors.Add($"Node '{node.nodeId}': choice {label} has no text for the player");

                if (string.IsNullOrWhiteSpace(choice.targetNodeId))
                    errors.Add($"Node '{node.nodeId}': choice {label} does not lead anywhere");
                else if (!byId.ContainsKey(choice.targetNodeId))
                    errors.Add($"Node '{node.nodeId}': choice {label} leads to '{choice.targetNodeId}', which does not exist");

                if (choice.hasCondition && string.IsNullOrWhiteSpace(choice.condition?.parameterId))
                    errors.Add($"Node '{node.nodeId}': choice {label} has a condition with nothing to check (empty parameter)");
            }
        }

        /// <summary>
        /// Ids of the nodes the conversation can get to from the start, following next nodes and choices.
        /// </summary>
        private static HashSet<string> Reachable(string startId, Dictionary<string, DialogueNodeEntry> byId)
        {
            var reached = new HashSet<string> { startId };
            var toVisit = new Stack<string>();
            toVisit.Push(startId);

            while (toVisit.Count > 0)
            {
                var node = byId[toVisit.Pop()];
                if (node.nodeType == DialogueNodeType.End)
                    continue;

                var targets = new List<string>();
                if (node.nextNodeIds != null)
                    targets.AddRange(node.nextNodeIds);
                if (node.choices != null)
                    targets.AddRange(node.choices.Where(c => c != null).Select(c => c.targetNodeId));

                foreach (var target in targets)
                {
                    if (!string.IsNullOrWhiteSpace(target) && byId.ContainsKey(target) && reached.Add(target))
                        toVisit.Push(target);
                }
            }

            return reached;
        }
    }

    /// <summary>
    /// One step of a conversation, as written in the inspector.
    /// </summary>
    [System.Serializable]
    public class DialogueNodeEntry
    {
        [Tooltip("Unique id of this node inside the dialogue (other nodes and choices point here by this id)")]
        public string nodeId = "node";

        [Tooltip("Line: someone speaks, then the conversation moves on. Choice: the player picks an answer. " +
                 "Branch: the game picks the way on. End: the conversation finishes here")]
        public DialogueNodeType nodeType = DialogueNodeType.Line;

        [Tooltip("Who speaks (e.g. 'npc_blacksmith', 'player', 'narrator')")]
        public string speakerId = "";

        [Tooltip("What is said")]
        [TextArea(2, 5)]
        public string text = "";

        [Tooltip("Where the conversation goes after this node when the player does not pick a choice (the first one whose condition passes)")]
        public List<string> nextNodeIds = new List<string>();

        [Tooltip("Answers the player can pick at this node")]
        public List<DialogueChoiceEntry> choices = new List<DialogueChoiceEntry>();

        [Tooltip("Tick to attach a condition the game can check before showing this node")]
        public bool hasCondition;

        [Tooltip("The condition, used only when 'Has Condition' is ticked")]
        public DialogueConditionEntry condition = new DialogueConditionEntry();

        /// <summary>
        /// This node as the Dialogue module's plain data. Empty next-node and choice slots are left out.
        /// </summary>
        public DialogueNodeData ToNodeData()
        {
            return new DialogueNodeData
            {
                Id = nodeId,
                NodeType = nodeType,
                SpeakerId = speakerId,
                Text = text,
                NextNodeIds = (nextNodeIds ?? new List<string>())
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .ToArray(),
                Choices = (choices ?? new List<DialogueChoiceEntry>())
                    .Where(c => c != null)
                    .Select(c => c.ToChoiceData())
                    .ToArray(),
                Condition = hasCondition && condition != null ? condition.ToConditionData() : (DialogueConditionData?)null
            };
        }
    }

    /// <summary>
    /// An answer the player can pick, as written in the inspector.
    /// </summary>
    [System.Serializable]
    public class DialogueChoiceEntry
    {
        [Tooltip("Unique id of this answer inside its node (the game picks an answer by this id)")]
        public string choiceId = "choice";

        [Tooltip("The answer as the player sees it")]
        public string text = "";

        [Tooltip("Id of the node the conversation goes to when the player picks this answer")]
        public string targetNodeId = "";

        [Tooltip("Tick to attach a condition the game can check before offering this answer")]
        public bool hasCondition;

        [Tooltip("The condition, used only when 'Has Condition' is ticked")]
        public DialogueConditionEntry condition = new DialogueConditionEntry();

        /// <summary>
        /// This answer as the Dialogue module's plain data.
        /// </summary>
        public DialogueChoiceData ToChoiceData()
        {
            return new DialogueChoiceData
            {
                Id = choiceId,
                Text = text,
                TargetNodeId = targetNodeId,
                Condition = hasCondition && condition != null ? condition.ToConditionData() : (DialogueConditionData?)null
            };
        }
    }

    /// <summary>
    /// A check on the player (finished quest, stat, item, dialogue variable), as written in the inspector.
    /// </summary>
    [System.Serializable]
    public class DialogueConditionEntry
    {
        [Tooltip("What to check: a finished quest, a stat, an owned item, a dialogue variable, or a check your game adds")]
        public ConditionType type = ConditionType.QuestComplete;

        [Tooltip("What is checked: the quest id, stat name, item id or variable name")]
        public string parameterId = "";

        [Tooltip("The number to compare with: minimum stat value, or the value the variable must have")]
        public int requiredValue = 1;

        /// <summary>
        /// This condition as the Dialogue module's plain data.
        /// </summary>
        public DialogueConditionData ToConditionData()
        {
            return new DialogueConditionData
            {
                Type = type,
                ParameterId = parameterId,
                RequiredValue = requiredValue
            };
        }
    }
}
