using System;
using System.Collections.Generic;
using System.Linq;
using RPGFramework.Events;

namespace RPGFramework.Companions.Runtime
{
    /// <summary>
    /// Default implementation of ICompanionManager.
    /// Manages all companions in the game.
    /// With an event publisher, raises CompanionFed when a companion is fed and hands the publisher to every
    /// companion it creates, so those raise their own summon/dismiss/death/level-up/loyalty events.
    /// </summary>
    public class CompanionManager : ICompanionManager
    {
        private const int FeedHunger = 30;
        private const int FeedHappiness = 10;
        private const int FeedLoyalty = 2;

        private readonly Dictionary<string, ICompanion> _companions;
        private readonly Dictionary<string, ICompanionStats> _companionStats;
        private readonly Dictionary<string, ICompanionBehavior> _companionBehaviors;
        private readonly Dictionary<string, ICompanionInventory> _companionInventories;
        private readonly IEventPublisher _events;
        private int _nextEntityId;

        public IReadOnlyList<ICompanion> ActiveCompanions =>
            _companions.Values.Where(c => c.IsActive).ToList();

        public CompanionManager(IEventPublisher events = null)
        {
            _events = events;
            _companions = new Dictionary<string, ICompanion>();
            _companionStats = new Dictionary<string, ICompanionStats>();
            _companionBehaviors = new Dictionary<string, ICompanionBehavior>();
            _companionInventories = new Dictionary<string, ICompanionInventory>();
            _nextEntityId = 1000; // Start companion entity IDs at 1000
        }

        public ICompanion CreateCompanion(string companionId, int ownerId, CompanionType type, string name)
        {
            if (string.IsNullOrEmpty(companionId))
                throw new ArgumentException("Companion ID cannot be null or empty", nameof(companionId));

            if (_companions.ContainsKey(companionId))
                throw new InvalidOperationException($"Companion with ID {companionId} already exists");

            int entityId = _nextEntityId++;
            var companion = new Companion(entityId, companionId, name, type, ownerId, _events);

            // Create default stats based on companion type
            var stats = CreateDefaultStats(type);
            var behavior = new CompanionBehavior();
            var inventory = new CompanionInventory(GetInventoryCapacity(type));

            _companions[companionId] = companion;
            _companionStats[companionId] = stats;
            _companionBehaviors[companionId] = behavior;
            _companionInventories[companionId] = inventory;

            return companion;
        }

        public ICompanion GetCompanion(string companionId)
        {
            return _companions.TryGetValue(companionId, out var companion) ? companion : null;
        }

        public IReadOnlyList<ICompanion> GetCompanionsByOwner(int ownerId)
        {
            return _companions.Values.Where(c => c.OwnerId == ownerId).ToList();
        }

        public IReadOnlyList<ICompanion> GetCompanionsByType(CompanionType type)
        {
            return _companions.Values.Where(c => c.CompanionType == type).ToList();
        }

        public bool RemoveCompanion(string companionId)
        {
            if (!_companions.ContainsKey(companionId))
                return false;

            _companions.Remove(companionId);
            _companionStats.Remove(companionId);
            _companionBehaviors.Remove(companionId);
            _companionInventories.Remove(companionId);
            return true;
        }

        public void UpdateCompanions(float deltaTime)
        {
            foreach (var companion in ActiveCompanions)
            {
                if (_companionStats.TryGetValue(companion.CompanionId, out var stats))
                {
                    stats.RegenerateStats(deltaTime);
                }

                if (_companionBehaviors.TryGetValue(companion.CompanionId, out var behavior))
                {
                    behavior.Update(deltaTime);
                }
            }
        }

        public bool FeedCompanion(string companionId, string foodItemId)
        {
            if (!_companions.TryGetValue(companionId, out var companion))
                return false;

            if (!_companionStats.TryGetValue(companionId, out var stats))
                return false;

            int hungerBefore = stats.Hunger;
            int happinessBefore = stats.Happiness;
            var concreteCompanion = companion as Companion;
            int loyaltyBefore = concreteCompanion != null ? concreteCompanion.GetData().LoyaltyValue : 0;

            // Feeding logic: restore hunger, increase happiness, boost loyalty
            stats.ModifyHunger(FeedHunger); // Restore 30 hunger
            stats.ModifyHappiness(FeedHappiness); // Increase happiness
            companion.ModifyLoyalty(FeedLoyalty); // Small loyalty boost

            if (concreteCompanion != null)
            {
                concreteCompanion.UpdateLastFedTime();
            }

            _events?.Publish(new CompanionFedEvent
            {
                CompanionId = companionId,
                EntityId = companion.EntityId,
                FoodItemId = foodItemId,
                HungerRestored = stats.Hunger - hungerBefore,
                HappinessGained = stats.Happiness - happinessBefore,
                // Other ICompanion implementations do not expose the loyalty value: report the requested boost
                LoyaltyGained = concreteCompanion != null
                    ? concreteCompanion.GetData().LoyaltyValue - loyaltyBefore
                    : FeedLoyalty,
                FedAt = DateTime.UtcNow
            });

            return true;
        }

        public bool TransferOwnership(string companionId, int newOwnerId)
        {
            var companion = GetCompanion(companionId);
            if (companion == null)
                return false;

            // Loyalty should decrease when ownership changes
            companion.ModifyLoyalty(-25);

            // Would need to update the underlying data structure
            // This is a simplified implementation
            return true;
        }

        public ICompanionStats GetCompanionStats(string companionId)
        {
            return _companionStats.TryGetValue(companionId, out var stats) ? stats : null;
        }

        public ICompanionBehavior GetCompanionBehavior(string companionId)
        {
            return _companionBehaviors.TryGetValue(companionId, out var behavior) ? behavior : null;
        }

        public ICompanionInventory GetCompanionInventory(string companionId)
        {
            return _companionInventories.TryGetValue(companionId, out var inventory) ? inventory : null;
        }

        private ICompanionStats CreateDefaultStats(CompanionType type)
        {
            return type switch
            {
                CompanionType.CombatPet => new CompanionStats(maxHealth: 100, attack: 15, defense: 10, speed: 1.2f),
                CompanionType.Mount => new CompanionStats(maxHealth: 150, attack: 5, defense: 15, speed: 2.0f),
                CompanionType.NonCombatPet => new CompanionStats(maxHealth: 50, attack: 1, defense: 5, speed: 1.0f),
                CompanionType.Summon => new CompanionStats(maxHealth: 80, attack: 25, defense: 5, speed: 1.5f),
                CompanionType.Familiar => new CompanionStats(maxHealth: 60, attack: 10, defense: 8, speed: 1.3f),
                _ => new CompanionStats(maxHealth: 100, attack: 10, defense: 10, speed: 1.0f)
            };
        }

        private int GetInventoryCapacity(CompanionType type)
        {
            return type switch
            {
                CompanionType.Mount => 20, // Mounts can carry lots
                CompanionType.CombatPet => 5,
                CompanionType.Familiar => 10,
                _ => 0 // Non-combat pets and summons typically don't carry items
            };
        }
    }
}
