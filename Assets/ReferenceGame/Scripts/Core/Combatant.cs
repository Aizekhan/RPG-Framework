using System;
using RPGFramework.Combat;
using RPGFramework.Stats;

namespace ReferenceGame
{
    public enum CombatStat
    {
        MaxHealth,
        Attack,
        Defense
    }

    /// <summary>
    /// A fighter in Goblin Valley: stats in the framework's <see cref="StatContainer{TStatType,TValue}"/>, current health,
    /// and the life-state callbacks the framework's <see cref="ILifeStateManager"/> drives.
    /// </summary>
    public sealed class Combatant : ILifeStateHandler
    {
        private StatContainer<CombatStat, float> _stats;

        public Combatant(int entityId, string kind, float maxHealth, float attack, float defense)
        {
            EntityId = entityId;
            Kind = kind ?? throw new ArgumentNullException(nameof(kind));
            _stats.Initialize();
            _stats.SetBaseStat(CombatStat.MaxHealth, maxHealth);
            _stats.SetBaseStat(CombatStat.Attack, attack);
            _stats.SetBaseStat(CombatStat.Defense, defense);
            Health = maxHealth;
        }

        public int EntityId { get; }

        /// <summary>
        /// What the entity counts as for quests and loot ("player", "goblin").
        /// </summary>
        public string Kind { get; }

        public float Health { get; private set; }
        public float MaxHealth => _stats.GetFinalStat(CombatStat.MaxHealth);
        public float Attack => _stats.GetFinalStat(CombatStat.Attack);
        public float Defense => _stats.GetFinalStat(CombatStat.Defense);

        public LifeState CurrentState { get; private set; } = LifeState.Alive;
        public bool CanDie => true;
        public bool CanResurrect => true;

        public float GetBaseStat(CombatStat stat) => _stats.GetBaseStat(stat);

        /// <summary>
        /// Sets a base stat; raising max health raises current health by the same amount.
        /// </summary>
        public void SetBaseStat(CombatStat stat, float value)
        {
            float oldMax = MaxHealth;
            _stats.SetBaseStat(stat, value);
            if (stat == CombatStat.MaxHealth)
                Health = Math.Min(MaxHealth, Health + Math.Max(0f, MaxHealth - oldMax));
        }

        /// <returns>Health actually removed.</returns>
        public float ApplyDamage(float amount)
        {
            float removed = Math.Min(Health, Math.Max(0f, amount));
            Health -= removed;
            return removed;
        }

        public void RestoreFullHealth() => Health = MaxHealth;

        public void OnDeath(int killerId, string reason) => Health = 0f;

        public void OnResurrect(int resurrectorId, string resurrectType) => RestoreFullHealth();

        public void OnLifeStateChanged(LifeState oldState, LifeState newState) => CurrentState = newState;

        public bool IsAlive() => CurrentState == LifeState.Alive || CurrentState == LifeState.Invulnerable;
        public bool IsDead() => CurrentState == LifeState.Dead;
        public bool IsInvulnerable() => CurrentState == LifeState.Invulnerable;
    }
}
