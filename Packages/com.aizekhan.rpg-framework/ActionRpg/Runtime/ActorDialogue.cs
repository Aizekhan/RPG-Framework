using System;
using System.Collections.Generic;
using System.Linq;
using RPGFramework.Dialogue;
using RPGFramework.Events;
using RPGFramework.Quests;

namespace RPGFramework.ActionRpg
{
    /// <summary>
    /// What a dialogue line or choice does in the world (<see cref="ActionRpgContent.DialogueEffects"/>).
    /// </summary>
    public enum DialogueEffectKind
    {
        GiveItem,
        TakeItem,
        GiveCurrency,
        TakeCurrency,
        AcceptQuest,
        CompleteQuest,
        SetVariable,
        GrantSkillPoints
    }

    /// <summary>
    /// A dialogue effect: when the player reaches the node (<see cref="NodeId"/>) or picks the choice
    /// (<see cref="ChoiceId"/>) in the dialogue, it happens to them. Target: the item, currency ("" is gold), quest
    /// or variable id; Amount: how many, or the variable's value.
    /// </summary>
    public sealed class DialogueEffect
    {
        public DialogueEffect(string dialogueId, DialogueEffectKind kind, string targetId = "", int amount = 1,
            string nodeId = null, string choiceId = null)
        {
            if (string.IsNullOrEmpty(dialogueId))
                throw new ArgumentException("A dialogue effect needs its dialogue id.", nameof(dialogueId));
            if (string.IsNullOrEmpty(nodeId) == string.IsNullOrEmpty(choiceId))
                throw new ArgumentException("A dialogue effect is on a node or on a choice, one of them.");
            DialogueId = dialogueId;
            Kind = kind;
            TargetId = targetId ?? "";
            Amount = amount;
            NodeId = nodeId;
            ChoiceId = choiceId;
        }

        public string DialogueId { get; }
        public DialogueEffectKind Kind { get; }
        public string TargetId { get; }
        public int Amount { get; }
        public string NodeId { get; }
        public string ChoiceId { get; }

        public static DialogueEffect OnNode(string dialogueId, string nodeId, DialogueEffectKind kind, string targetId = "", int amount = 1) =>
            new DialogueEffect(dialogueId, kind, targetId, amount, nodeId: nodeId);

        public static DialogueEffect OnChoice(string dialogueId, string choiceId, DialogueEffectKind kind, string targetId = "", int amount = 1) =>
            new DialogueEffect(dialogueId, kind, targetId, amount, choiceId: choiceId);
    }

    /// <summary>
    /// Conversations for the players of an <see cref="ActorWorld"/> (<see cref="ActionRpgContent.Dialogues"/>,
    /// Dialogue module). Conditions are checked against the player: completed quests, items in the backpack (and how
    /// many), stats (<see cref="ActorStat"/> names, "Level", "Gold" or any currency id) and the player's dialogue
    /// variables. Effects (<see cref="ActionRpgContent.DialogueEffects"/>) give and take items and currency, accept and
    /// hand in quests, set variables and grant skill points. Starting a dialogue counts as an Interact quest objective
    /// with the dialogue's id. The variables are part of the player's save.
    /// </summary>
    public sealed class ActorDialogue : IDisposable
    {
        private readonly ActorWorld _world;
        private readonly DialogueRegistry _dialogues = new DialogueRegistry();
        private readonly Dictionary<int, DialogueContext> _contexts = new Dictionary<int, DialogueContext>();
        private readonly List<DialogueEffect> _effects = new List<DialogueEffect>();
        private readonly SubscriptionBag _subscriptions = new SubscriptionBag();
        private readonly DialogueManager _manager;

        internal ActorDialogue(ActorWorld world, RPGFramework.ECS.IEntityManager entities, IEventBus bus)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _manager = new DialogueManager(entities, _dialogues, bus, ContextOf);
            bus.Subscribe<LineSpokenEvent>(OnLine).AddTo(_subscriptions);
            bus.Subscribe<ChoiceSelectedEvent>(OnChoice).AddTo(_subscriptions);
        }

        /// <summary>
        /// Answers a Custom condition (condition id, value) for a player; null: Custom conditions fail.
        /// </summary>
        public Func<int, string, int, bool> CustomCondition { get; set; }

        public IDialogueRegistry Dialogues => _dialogues;

        internal void Register(IDialogue dialogue)
        {
            if (dialogue != null)
                _dialogues.RegisterDialogue(dialogue);
        }

        internal void Register(DialogueEffect effect)
        {
            if (effect != null)
                _effects.Add(effect);
        }

        /// <summary>
        /// Starts a dialogue (talking to an NPC); false when it is unknown or its first line's condition fails.
        /// </summary>
        public bool Start(int playerId, string dialogueId)
        {
            if (_world.ProgressOwnerOf(playerId) < 0 || !_manager.StartDialogue(playerId, dialogueId))
                return false;
            _world.ReportQuestProgress(playerId, ObjectiveType.Interact, dialogueId);
            return true;
        }

        /// <summary>
        /// Goes on to the next line, or takes a choice at a choice line.
        /// </summary>
        public bool Advance(int playerId, string choiceId = null) => _manager.AdvanceDialogue(playerId, choiceId);

        public void End(int playerId) => _manager.EndDialogue(playerId);

        public bool IsTalking(int playerId) => _manager.GetDialogueProgress(playerId) != null;

        public IDialogueNode Current(int playerId) => _manager.GetCurrentNode(playerId);

        /// <summary>
        /// The choices the player can take now (the ones whose condition passes).
        /// </summary>
        public IReadOnlyList<IDialogueChoice> Choices(int playerId) => _manager.GetAvailableChoices(playerId);

        public int VariableOf(int playerId, string variable) =>
            _contexts.TryGetValue(playerId, out var context) && context.Variables.TryGetValue(variable, out int value) ? value : 0;

        public void SetVariable(int playerId, string variable, int value)
        {
            if (!string.IsNullOrEmpty(variable))
                Context(playerId).Variables[variable] = value;
        }

        internal List<VariableRecord> RecordsOf(int playerId) =>
            _contexts.TryGetValue(playerId, out var context)
                ? context.Variables.OrderBy(v => v.Key, StringComparer.Ordinal).Select(v => new VariableRecord { Name = v.Key, Value = v.Value }).ToList()
                : new List<VariableRecord>();

        internal void Restore(int playerId, IEnumerable<VariableRecord> records)
        {
            if (IsTalking(playerId))
                End(playerId);
            var variables = Context(playerId).Variables;
            variables.Clear();
            foreach (var record in records ?? Enumerable.Empty<VariableRecord>())
            {
                if (!string.IsNullOrEmpty(record?.Name))
                    variables[record.Name] = record.Value;
            }
        }

        internal void Forget(int playerId)
        {
            if (IsTalking(playerId))
                End(playerId);
            _contexts.Remove(playerId);
        }

        public void Dispose() => _subscriptions.Dispose();

        private IDialogueContext ContextOf(int playerId) => _world.ProgressOwnerOf(playerId) >= 0 ? Context(playerId) : null;

        private DialogueContext Context(int playerId)
        {
            if (_contexts.TryGetValue(playerId, out var context))
                return context;
            context = new DialogueContext(playerId)
            {
                QuestDone = quest => _world.CompletedQuestsOf(playerId).Contains(quest),
                ItemCount = item => _world.InventoryOf(playerId)?.GetAllItems().Count(i => i != null && i.ItemId == item) ?? 0,
                Stat = stat => StatOf(playerId, stat),
                Custom = (id, value) => CustomCondition != null && CustomCondition(playerId, id, value)
            };
            _contexts[playerId] = context;
            return context;
        }

        private int StatOf(int playerId, string stat)
        {
            if (string.IsNullOrEmpty(stat))
                return 0;
            if (stat == "Level")
                return _world.LevelOf(playerId);
            var actor = _world.Get(playerId);
            if (actor != null && Enum.TryParse(stat, false, out ActorStat parsed) && Enum.IsDefined(typeof(ActorStat), parsed))
            {
                float value = actor.Stat(parsed);
                // Chances (0..1) read as percent, so "CriticalChance >= 25" works
                return (int)Math.Round(parsed == ActorStat.CriticalChance || parsed == ActorStat.Evasion || parsed == ActorStat.LifeSteal
                    ? value * 100f : value);
            }
            long held = _world.CurrencyOf(playerId, stat == "Gold" ? RPGFramework.Trading.Currencies.Gold : stat);
            return (int)Math.Min(int.MaxValue, held);
        }

        private void OnLine(LineSpokenEvent line)
        {
            foreach (var effect in _effects)
            {
                if (effect.NodeId != null && effect.DialogueId == line.DialogueId && effect.NodeId == line.NodeId)
                    Apply(line.EntityId, effect);
            }
        }

        private void OnChoice(ChoiceSelectedEvent choice)
        {
            foreach (var effect in _effects)
            {
                if (effect.ChoiceId != null && effect.DialogueId == choice.DialogueId && effect.ChoiceId == choice.ChoiceId)
                    Apply(choice.EntityId, effect);
            }
        }

        private void Apply(int playerId, DialogueEffect effect)
        {
            if (_world.ProgressOwnerOf(playerId) < 0)
                return;
            string currency = string.IsNullOrEmpty(effect.TargetId) ? RPGFramework.Trading.Currencies.Gold : effect.TargetId;
            switch (effect.Kind)
            {
                case DialogueEffectKind.GiveItem:
                    _world.GiveItem(playerId, effect.TargetId, Math.Max(1, effect.Amount));
                    break;
                case DialogueEffectKind.TakeItem:
                    _world.TakeItem(playerId, effect.TargetId, Math.Max(1, effect.Amount));
                    break;
                case DialogueEffectKind.GiveCurrency:
                    _world.GiveCurrency(playerId, Math.Max(0, effect.Amount), currency, "dialogue:" + effect.DialogueId);
                    break;
                case DialogueEffectKind.TakeCurrency:
                    _world.SpendCurrency(playerId, Math.Max(0, effect.Amount), currency, "dialogue:" + effect.DialogueId);
                    break;
                case DialogueEffectKind.AcceptQuest:
                    _world.AcceptQuest(playerId, effect.TargetId);
                    break;
                case DialogueEffectKind.CompleteQuest:
                    _world.CompleteQuest(playerId, effect.TargetId);
                    break;
                case DialogueEffectKind.SetVariable:
                    SetVariable(playerId, effect.TargetId, effect.Amount);
                    break;
                case DialogueEffectKind.GrantSkillPoints:
                    _world.Skills.GrantPoints(playerId, effect.Amount);
                    break;
            }
        }
    }

    /// <summary>
    /// A dialogue variable in a save.
    /// </summary>
    public sealed class VariableRecord
    {
        public string Name { get; set; }
        public int Value { get; set; }
    }
}
