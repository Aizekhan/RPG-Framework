using System;

namespace RPGFramework.Companions.Runtime
{
    /// <summary>
    /// Default implementation of ICompanion.
    /// </summary>
    public class Companion : ICompanion
    {
        private CompanionData _data;

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

        public Companion(int entityId, string companionId, string name, CompanionType type, int ownerId)
        {
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

            _data.IsActive = true;
            _data.CurrentState = CompanionState.Idle;
        }

        public void Dismiss()
        {
            _data.IsActive = false;
            _data.CurrentState = CompanionState.Dismissed;
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
            _data.LoyaltyValue = Math.Clamp(_data.LoyaltyValue + amount, -100, 100);
        }

        public void SetState(CompanionState state)
        {
            _data.CurrentState = state;
        }

        public void Kill()
        {
            _data.IsAlive = false;
            _data.CurrentState = CompanionState.Dead;
        }

        public void Revive()
        {
            _data.IsAlive = true;
            _data.CurrentState = CompanionState.Idle;
        }

        public void LevelUp()
        {
            _data.Level++;
        }

        public void UpdateLastFedTime()
        {
            _data.LastFedAt = DateTime.UtcNow;
        }

        public CompanionData GetData() => _data;
    }
}
