using System;
using System.Collections.Generic;
using System.Linq;
using RPGFramework.Combat;
using RPGFramework.Events;
using RPGFramework.Inventory.Runtime;
using RPGFramework.Items;
using RPGFramework.Quests;

namespace RPGFramework.ActionRpg.Coop
{
    /// <summary>
    /// The host's game as a joined player sees it: the same characters with the host's numbers, the party's level,
    /// backpack and quests. Characters entering, leaving and getting hit are published on this machine's bus like in
    /// a game of its own, so views and the HUD work unchanged. Attacks and revives go to the host, which decides.
    /// </summary>
    public sealed class RemoteWorld : IActorWorld
    {
        private readonly ActionRpgContent _content;
        private readonly IEventBus _bus;
        private readonly Action<CoopIntentMessage> _send;
        private readonly Dictionary<int, Actor> _actors = new Dictionary<int, Actor>();
        private readonly InventoryContainer<IItem> _inventory;
        private PartyStatus _party = new PartyStatus();
        private List<(IQuest, IQuestProgress)> _activeQuests = new List<(IQuest, IQuestProgress)>();

        public RemoteWorld(ActionRpgSettings settings, ActionRpgContent content, IEventBus bus, Action<CoopIntentMessage> send)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _content = content ?? new ActionRpgContent();
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
            _send = send ?? throw new ArgumentNullException(nameof(send));
            _inventory = new InventoryContainer<IItem>(settings.InventoryCapacity, name: "Backpack");
        }

        public ActionRpgSettings Settings { get; }
        public bool IsStarted => PlayerId >= 0;
        public int PlayerId { get; private set; } = -1;
        public Actor Player => Get(PlayerId);
        public IEnumerable<Actor> Actors => _actors.Values;
        public IEnumerable<int> LivingEnemies => _actors.Values.Where(a => !a.IsPlayer && a.IsAlive()).Select(a => a.EntityId);
        public IEnumerable<int> LivingPlayers => _actors.Values.Where(a => a.IsPlayer && a.IsAlive()).Select(a => a.EntityId);
        public Actor Get(int entityId) => _actors.TryGetValue(entityId, out var actor) ? actor : null;

        public bool AreHostile(int attackerId, int targetId)
        {
            var attacker = Get(attackerId);
            var target = Get(targetId);
            return attacker != null && target != null && attacker != target
                   && HostilityRules.For(Settings).AreHostile(attacker, target);
        }

        public int Kills => _party.Kills;
        public int Level => _party.Level;
        public long TotalExperience => _party.TotalExperience;
        public long ExperienceForNextLevel => _party.ExperienceForNextLevel;
        public int CurrentWave => _party.Wave;
        public IReadOnlyList<string> Log => _party.Log;
        public IEnumerable<(IQuest Quest, IQuestProgress Progress)> ActiveQuests => _activeQuests;
        public IReadOnlyList<string> CompletedQuests => _party.CompletedQuests;
        public IInventoryContainer<IItem> Inventory => _inventory;
        public PvpStatus PvpStatus => _party.Pvp;

        public float Attack(int attackerId, int targetId)
        {
            if (IsStarted && attackerId == PlayerId && Player?.IsAlive() == true && Get(targetId)?.IsAlive() == true
                && AreHostile(attackerId, targetId))
                _send(new CoopIntentMessage { Kind = CoopIntentMessage.IntentKind.Attack, TargetId = targetId });
            return 0f;
        }

        public bool RevivePlayer()
        {
            if (!IsStarted || Player == null || Player.IsAlive() || Settings.Pvp != null)
                return false;
            _send(new CoopIntentMessage { Kind = CoopIntentMessage.IntentKind.Revive });
            return true;
        }

        internal void Begin(int playerId) => PlayerId = playerId;

        /// <summary>
        /// The host is gone: everyone leaves the screen.
        /// </summary>
        internal void End()
        {
            foreach (int id in _actors.Keys.ToList())
                Despawn(id);
            PlayerId = -1;
            _party = new PartyStatus();
            _activeQuests.Clear();
            _inventory.Clear();
        }

        internal void Apply(CoopWorldMessage message)
        {
            foreach (var change in message.Changes)
            {
                switch (change.Kind)
                {
                    case CoopWorldMessage.ChangeKind.Spawn:
                        Despawn(change.Id);
                        var character = Settings.FindCharacter(change.CharacterId)
                                        ?? new CharacterData { Id = change.CharacterId, DisplayName = change.CharacterId };
                        _actors[change.Id] = new Actor(change.Id, character, change.IsPlayer)
                        {
                            PlayerName = string.IsNullOrEmpty(change.Name) ? null : change.Name, Seat = change.Seat
                        };
                        _bus.Publish(new ActorSpawnedEvent(change.Id, change.CharacterId, change.IsPlayer));
                        break;
                    case CoopWorldMessage.ChangeKind.Despawn:
                        Despawn(change.Id);
                        break;
                    case CoopWorldMessage.ChangeKind.Hit:
                        var target = Get(change.Id);
                        if (target == null)
                            break;
                        target.SetHealth(change.HealthLeft);
                        _bus.Publish(new ActorDamagedEvent(change.Id, change.AttackerId, change.Amount, change.HealthLeft));
                        break;
                }
            }

            foreach (var state in message.States)
            {
                var actor = Get(state.Id);
                if (actor == null)
                    continue;
                actor.SetBaseStat(ActorStat.MaxHealth, state.MaxHealth);
                actor.SetBaseStat(ActorStat.Attack, state.Attack);
                actor.SetBaseStat(ActorStat.Defense, state.Defense);
                actor.SetHealth(state.Health);
                if (state.Alive != actor.IsAlive())
                    actor.OnLifeStateChanged(actor.CurrentState, state.Alive ? LifeState.Alive : LifeState.Dead);
                if (actor.Team != state.Team)
                {
                    actor.Team = state.Team;
                    _bus.Publish(new ActorTeamChangedEvent(actor.EntityId, state.Team));
                }
            }

            if (message.Party != null)
                ApplyParty(message.Party);
        }

        private void ApplyParty(PartyStatus party)
        {
            _party = party;

            _inventory.Clear();
            foreach (var itemId in party.Items)
            {
                var item = _content.Items.FirstOrDefault(i => i != null && i.ItemId == itemId);
                if (item != null && !_inventory.Add(item))
                    break;
            }

            _activeQuests = new List<(IQuest, IQuestProgress)>();
            foreach (var record in party.ActiveQuests)
            {
                var quest = _content.Quests.FirstOrDefault(q => q != null && q.Id == record.QuestId);
                if (quest == null)
                    continue;
                var progress = new QuestProgress(record.QuestId);
                foreach (var objective in record.Objectives)
                    progress.UpdateObjectiveProgress(objective.ObjectiveId, objective.Progress);
                _activeQuests.Add((quest, progress));
            }
        }

        private void Despawn(int entityId)
        {
            if (!_actors.TryGetValue(entityId, out var actor))
                return;
            _actors.Remove(entityId);
            _bus.Publish(new ActorDespawnedEvent(entityId, actor.CharacterId, actor.IsPlayer));
        }
    }
}
