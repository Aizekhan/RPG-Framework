using System;
using System.Collections.Generic;
using RPGFramework.Combat;
using RPGFramework.Stats;

namespace RPGFramework.ActionRpg
{
    /// <summary>
    /// A character in the world: its definition, stats (in the framework's <see cref="StatContainer{TStatType,TValue}"/>),
    /// current health, and the life-state callbacks <see cref="ILifeStateManager"/> drives.
    /// </summary>
    public sealed class Actor : ILifeStateHandler
    {
        private StatContainer<ActorStat, float> _stats;

        public Actor(int entityId, CharacterData character, bool isPlayer, int level = 1)
        {
            EntityId = entityId;
            Character = character ?? throw new ArgumentNullException(nameof(character));
            IsPlayer = isPlayer;
            Level = Math.Max(1, level);
            _stats.Initialize();
            foreach (ActorStat stat in Enum.GetValues(typeof(ActorStat)))
                _stats.SetBaseStat(stat, character.StatAt(stat, level));
            Health = MaxHealth;
        }

        public int EntityId { get; }

        /// <summary>
        /// The level it was made at (an enemy's, from <see cref="ActionRpgSettings.EnemyLevels"/>).
        /// </summary>
        public int Level { get; }

        public CharacterData Character { get; }
        public bool IsPlayer { get; }

        /// <summary>
        /// The character id ("goblin"): prefab key, quest target, loot owner.
        /// </summary>
        public string CharacterId => Character.Id;

        /// <summary>
        /// The name shown for it: a co-op player's own name, otherwise the character's.
        /// </summary>
        public string DisplayName => PlayerName ?? EliteName ?? Character.DisplayName;

        /// <summary>
        /// An elite enemy's name with its affixes ("Frenzied Goblin of Flame"); null for an ordinary one.
        /// </summary>
        public string EliteName { get; internal set; }

        /// <summary>
        /// An elite enemy's affixes (<see cref="ActionRpgContent.EliteAffixes"/>); empty for an ordinary one.
        /// </summary>
        public IReadOnlyList<RPGFramework.Items.RolledAffix> EliteAffixes { get; internal set; } =
            System.Array.Empty<RPGFramework.Items.RolledAffix>();

        public bool IsElite => EliteAffixes.Count > 0;

        /// <summary>
        /// A player's own name in co-op; null uses the character's name.
        /// </summary>
        public string PlayerName { get; internal set; }

        /// <summary>
        /// A player's place in the party (0 = the first player, then 1, 2, 3), for their colour; -1 for enemies.
        /// Kept for as long as the player stays.
        /// </summary>
        public int Seat { get; internal set; } = -1;

        /// <summary>
        /// The side it fights for in PvP (a team name, a faction id, "P1" in free-for-all); null = none.
        /// </summary>
        public string Team { get; internal set; }

        public float Health { get; private set; }
        public float MaxHealth => _stats.GetFinalStat(ActorStat.MaxHealth);
        public float Attack => _stats.GetFinalStat(ActorStat.Attack);
        public float Defense => _stats.GetFinalStat(ActorStat.Defense);
        public float HealthFraction => MaxHealth > 0f ? Math.Max(0f, Math.Min(1f, Health / MaxHealth)) : 0f;

        /// <summary>
        /// How fast it may move compared with its character (status effects: 0.6 slowed, 0 rooted, 1 normal).
        /// </summary>
        public float MoveMultiplier { get; internal set; } = 1f;

        private readonly HashSet<string> _controls = new HashSet<string>();

        /// <summary>
        /// Controls on it from status effects ("Stun", "Silence"): the presentation stops input, abilities refuse.
        /// </summary>
        public IReadOnlyCollection<string> Controls => _controls;

        public bool HasControl(string controlId) => controlId != null && _controls.Contains(controlId);

        /// <summary>
        /// The control that stops a character from moving, striking and using abilities.
        /// </summary>
        public const string StunControl = "Stun";

        public bool IsStunned => _controls.Contains(StunControl);

        internal void SetControl(string controlId, bool active)
        {
            if (active)
                _controls.Add(controlId);
            else
                _controls.Remove(controlId);
        }

        internal void SetControls(IEnumerable<string> controls)
        {
            _controls.Clear();
            foreach (var control in controls)
                _controls.Add(control);
        }

        /// <summary>
        /// Puts a modifier on a stat (a status effect); raising max health keeps the health share.
        /// </summary>
        internal void AddModifier(StatModifier<ActorStat, float> modifier)
        {
            float oldMax = MaxHealth;
            _stats.AddModifier(modifier);
            KeepHealthWithin(oldMax);
        }

        internal void RemoveModifier(string modifierId)
        {
            float oldMax = MaxHealth;
            _stats.RemoveModifier(modifierId);
            KeepHealthWithin(oldMax);
        }

        // A higher max health adds the difference, a lower one only caps
        private void KeepHealthWithin(float oldMax)
        {
            if (MaxHealth > oldMax && CurrentState != LifeState.Dead)
                Health += MaxHealth - oldMax;
            Health = Math.Min(Health, MaxHealth);
        }

        /// <returns>Health actually restored.</returns>
        public float Heal(float amount)
        {
            if (!IsAlive() || !(amount > 0f))
                return 0f;
            float restored = Math.Min(MaxHealth - Health, amount);
            Health += restored;
            return restored;
        }

        public LifeState CurrentState { get; private set; } = LifeState.Alive;
        public bool CanDie => true;
        public bool CanResurrect => true;

        public float GetBaseStat(ActorStat stat) => _stats.GetBaseStat(stat);

        /// <summary>
        /// Sets a base stat; raising max health raises current health by the same amount.
        /// </summary>
        public void SetBaseStat(ActorStat stat, float value)
        {
            float oldMax = MaxHealth;
            _stats.SetBaseStat(stat, value);
            if (stat == ActorStat.MaxHealth)
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

        /// <summary>
        /// Sets health as it is elsewhere (a co-op client mirroring the host), kept within 0..max.
        /// </summary>
        public void SetHealth(float health) => Health = Math.Max(0f, Math.Min(MaxHealth, health));

        public void OnDeath(int killerId, string reason) => Health = 0f;

        public void OnResurrect(int resurrectorId, string resurrectType) => RestoreFullHealth();

        public void OnLifeStateChanged(LifeState oldState, LifeState newState) => CurrentState = newState;

        public bool IsAlive() => CurrentState == LifeState.Alive || CurrentState == LifeState.Invulnerable;
        public bool IsDead() => CurrentState == LifeState.Dead;
        public bool IsInvulnerable() => CurrentState == LifeState.Invulnerable;
    }
}
