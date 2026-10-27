using System;
using RPGFramework.Events;

namespace RPGFramework.Companions.Runtime
{
    /// <summary>
    /// Default implementation of ICompanion.
    /// With an event publisher, raises CompanionSummoned/CompanionDismissed/CompanionDeath/CompanionLevelUp
    /// when those changes happen, and LoyaltyChanged when the loyalty level (not just the value) changes.
    /// </summary>
    public class Companion : ICompanion
    {
        private CompanionData _data;
        private readonly IEventPublisher _events;

        public string CompanionId => _data.CompanionId;
        public int EntityId { get; private set; }
        public string Name => _data.Name;
        public CompanionType CompanionType => _data.CompanionType;
        public CompanionState CurrentState => _data.CurrentState;
        public int OwnerId => _data.OwnerId;
        public LoyaltyLevel Loyalty => _data.GetLoyaltyLevel();
        public int Level => _data.Level;
        public bool IsActive => _data.IsActive;
        public bool IsAlive => _data.IsAlive;

        public Companion(int entityId, string companionId, string name, CompanionType type, int ownerId,
            IEventPublisher events = null)
        {
            _events = events;
            EntityId = entityId;
            _data = new CompanionData
            {
                CompanionId = companionId,
                Name = name,
                CompanionType = type,
                OwnerId = ownerId,
                CurrentState = CompanionState.Dismissed,
                LoyaltyValue = 0, // Neutral
                Level = 1,
                IsActive = false,
                IsAlive = true,
                CreatedAt = DateTime.UtcNow,
                LastFedAt = DateTime.UtcNow
            };
        }

        public void Summon()
        {
            if (!_data.IsAlive)
            {
                throw new InvalidOperationException("Cannot summon a dead companion. Revive it first.");
            }

            bool wasActive = _data.IsActive;
            _data.IsActive = true;
            _data.CurrentState = CompanionState.Idle;

            if (!wasActive)
            {
                _events?.Publish(new CompanionSummonedEvent
                {
                    CompanionId = _data.CompanionId,
                    EntityId = EntityId,
                    OwnerId = _data.OwnerId,
                    CompanionType = _data.CompanionType,
                    SummonedAt = DateTime.UtcNow
                });
            }
        }

        public void Dismiss()
        {
            bool wasActive = _data.IsActive;
            _data.IsActive = false;
            _data.CurrentState = CompanionState.Dismissed;

            if (wasActive)
            {
                _events?.Publish(new CompanionDismissedEvent
                {
                    CompanionId = _data.CompanionId,
                    EntityId = EntityId,
                    OwnerId = _data.OwnerId,
                    DismissedAt = DateTime.UtcNow
                });
            }
        }

        public void IssueCommand(CompanionCommand command)
        {
            if (!_data.IsActive)
            {
                throw new InvalidOperationException("Cannot issue commands to an inactive companion.");
            }

            // Update state based on command
            _data.CurrentState = command switch
            {
                CompanionCommand.Follow => CompanionState.Following,
                CompanionCommand.Stay => CompanionState.Idle,
                CompanionCommand.Attack => CompanionState.Fighting,
                CompanionCommand.Defend => CompanionState.Defending,
                CompanionCommand.Patrol => CompanionState.Patrolling,
                CompanionCommand.Return => CompanionState.Following,
                CompanionCommand.Stop => CompanionState.Idle,
                _ => _data.CurrentState
            };
        }

        public void ModifyLoyalty(int amount)
        {
            var oldLoyalty = _data.GetLoyaltyLevel();
            _data.LoyaltyValue = Math.Clamp(_data.LoyaltyValue + amount, -100, 100);
            var newLoyalty = _data.GetLoyaltyLevel();

            if (newLoyalty != oldLoyalty)
            {
                _events?.Publish(new LoyaltyChangedEvent
                {
                    CompanionId = _data.CompanionId,
                    EntityId = EntityId,
                    OldLoyalty = oldLoyalty,
                    NewLoyalty = newLoyalty,
                    LoyaltyValue = _data.LoyaltyValue,
                    ChangedAt = DateTime.UtcNow
                });
            }
        }

        public void SetState(CompanionState state)
        {
            _data.CurrentState = state;
        }

        public void Kill()
        {
            bool wasAlive = _data.IsAlive;
            _data.IsAlive = false;
            _data.CurrentState = CompanionState.Dead;

            if (wasAlive)
            {
                _events?.Publish(new CompanionDeathEvent
                {
                    CompanionId = _data.CompanionId,
                    EntityId = EntityId,
                    OwnerId = _data.OwnerId,
                    KillerEntityId = -1, // Kill() is not told who did it
                    DeathTime = DateTime.UtcNow
                });
            }
        }

        public void Revive()
        {
            _data.IsAlive = true;
            _data.CurrentState = CompanionState.Idle;
        }

        public void LevelUp()
        {
            int oldLevel = _data.Level;
            _data.Level++;

            _events?.Publish(new CompanionLevelUpEvent
            {
                CompanionId = _data.CompanionId,
                EntityId = EntityId,
                OldLevel = oldLevel,
                NewLevel = _data.Level,
                LeveledUpAt = DateTime.UtcNow
            });
        }

        public void UpdateLastFedTime()
        {
            _data.LastFedAt = DateTime.UtcNow;
        }

        public CompanionData GetData() => _data;
    }
}
