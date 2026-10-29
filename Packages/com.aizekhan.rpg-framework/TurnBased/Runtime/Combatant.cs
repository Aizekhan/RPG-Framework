using System;
using System.Collections.Generic;
using System.Linq;
using RPGFramework.StatusEffects;

namespace RPGFramework.TurnBased
{
    public enum BattleSide
    {
        Party,
        Enemies
    }

    /// <summary>
    /// A fighter: a party member (kept by the world from fight to fight, with its level, health and mana) or an enemy
    /// (made for one battle). Its id is an ECS entity, so the StatusEffects and Abilities modules track it.
    /// Stats are the character's stats at its level, changed by the status effects on it.
    /// </summary>
    public sealed class Combatant
    {
        /// <summary>
        /// Control (TargetId of a Control modifier) that makes a combatant lose its turns.
        /// </summary>
        public const string Stun = "Stun";

        /// <summary>
        /// Control that keeps a combatant from using abilities.
        /// </summary>
        public const string Silence = "Silence";

        private readonly Dictionary<string, StatModifier> _modifiers = new Dictionary<string, StatModifier>();
        private readonly Dictionary<string, StatModifier> _lasting = new Dictionary<string, StatModifier>();
        private readonly HashSet<string> _controls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        internal Combatant(int id, CombatantData data, BattleSide side, int slot, string name, int level)
        {
            Id = id;
            Data = data ?? throw new ArgumentNullException(nameof(data));
            Side = side;
            Slot = slot;
            Name = string.IsNullOrEmpty(name) ? data.DisplayName : name;
            Level = Math.Max(1, level);
            Health = MaxHealth;
            Mana = MaxMana;
        }

        /// <summary>
        /// Entity id; also how events name it.
        /// </summary>
        public int Id { get; }

        public CombatantData Data { get; }
        public string CharacterId => Data.Id;
        public BattleSide Side { get; }

        /// <summary>
        /// Place on its side (party order, or the enemy's place in the encounter); breaks speed ties.
        /// </summary>
        public int Slot { get; }

        /// <summary>
        /// Name shown ("Goblin B" when an encounter has several goblins).
        /// </summary>
        public string Name { get; }

        public int Level { get; private set; }
        public int Health { get; private set; }
        public int Mana { get; private set; }

        public int MaxHealth => Math.Max(1, Round(Stat(CombatStat.MaxHealth)));
        public int MaxMana => Math.Max(0, Round(Stat(CombatStat.MaxMana)));
        public float Attack => Stat(CombatStat.Attack);
        public float Defense => Stat(CombatStat.Defense);
        public float Magic => Stat(CombatStat.Magic);
        public float Speed => Stat(CombatStat.Speed);

        public bool IsAlive => Health > 0;
        public bool IsParty => Side == BattleSide.Party;

        /// <summary>
        /// Took the Defend action: takes less damage until its next turn starts.
        /// </summary>
        public bool IsDefending { get; internal set; }

        /// <summary>
        /// Turns it has started in the current battle (the clock its status effects run on).
        /// </summary>
        public int TurnsTaken { get; internal set; }

        public bool IsStunned => _controls.Contains(Stun);
        public bool IsSilenced => _controls.Contains(Silence);

        /// <summary>
        /// Controls status effects put on it ("Stun", "Silence", or the game's own).
        /// </summary>
        public IReadOnlyCollection<string> Controls => _controls;

        public IReadOnlyList<string> AbilityIds => Data.AbilityIds ?? (IReadOnlyList<string>)Array.Empty<string>();

        public bool Knows(string abilityId) => Data.AbilityIds != null && Data.AbilityIds.Contains(abilityId);

        public float HealthFraction => (float)Health / MaxHealth;

        /// <summary>
        /// The stat at its level before status effects.
        /// </summary>
        public float BaseStat(CombatStat stat) => Data.StatAt(stat, Level);

        /// <summary>
        /// The stat with status effects: base, plus every Add, times every Multiply; an Override replaces it all
        /// (the latest one wins). Never below 0.
        /// </summary>
        public float Stat(CombatStat stat)
        {
            float added = 0f;
            float multiplied = 1f;
            float? overridden = null;
            foreach (var modifier in _lasting.Values.Concat(_modifiers.Values))
            {
                if (modifier.Stat != stat)
                    continue;
                switch (modifier.Operation)
                {
                    case ModifierOperation.Add: added += modifier.Value; break;
                    case ModifierOperation.Multiply: multiplied *= modifier.Value; break;
                    case ModifierOperation.Override: overridden = modifier.Value; break;
                }
            }
            float value = overridden ?? (BaseStat(stat) + added) * multiplied;
            return Math.Max(0f, value);
        }

        public bool HasControl(string controlId) => _controls.Contains(controlId);

        public override string ToString() => $"{Name} ({Health}/{MaxHealth} HP, {Mana}/{MaxMana} MP)";

        // ---- Changed by the battle and the world ----

        /// <returns>Damage actually taken (no more than the health left).</returns>
        internal int TakeDamage(int amount)
        {
            if (amount <= 0 || !IsAlive)
                return 0;
            int taken = Math.Min(amount, Health);
            Health -= taken;
            return taken;
        }

        /// <returns>Health actually restored (a fallen combatant is not healed).</returns>
        internal int Heal(int amount)
        {
            if (amount <= 0 || !IsAlive)
                return 0;
            int healed = Math.Min(amount, MaxHealth - Health);
            Health += healed;
            return healed;
        }

        /// <returns>Health it comes back with (0 when it was not down).</returns>
        internal int Revive(int health)
        {
            if (IsAlive)
                return 0;
            Health = Math.Max(1, Math.Min(health, MaxHealth));
            return Health;
        }

        internal int RestoreMana(int amount)
        {
            if (amount <= 0)
                return 0;
            int restored = Math.Min(amount, MaxMana - Mana);
            Mana += restored;
            return restored;
        }

        internal void SpendMana(int amount) => Mana = Math.Max(0, Mana - Math.Max(0, amount));

        internal void SpendHealth(int amount) => Health = Math.Max(0, Health - Math.Max(0, amount));

        /// <summary>
        /// Sets health and mana as they were saved (kept within the maximums).
        /// </summary>
        internal void SetVitals(int health, int mana)
        {
            Health = Math.Max(0, Math.Min(health, MaxHealth));
            Mana = Math.Max(0, Math.Min(mana, MaxMana));
        }

        internal void RestoreFully()
        {
            Health = MaxHealth;
            Mana = MaxMana;
        }

        /// <summary>
        /// A new level: the maximums grow, and health and mana grow by as much.
        /// </summary>
        internal void SetLevel(int level)
        {
            int maxHealth = MaxHealth;
            int maxMana = MaxMana;
            Level = Math.Max(1, level);
            if (IsAlive)
                Health = Math.Min(MaxHealth, Health + Math.Max(0, MaxHealth - maxHealth));
            Mana = Math.Min(MaxMana, Mana + Math.Max(0, MaxMana - maxMana));
            ClampVitals();
        }

        internal void SetModifier(string modifierId, CombatStat stat, ModifierOperation operation, float value)
        {
            _modifiers[modifierId] = new StatModifier(stat, operation, value);
            ClampVitals();
        }

        internal void RemoveModifier(string modifierId)
        {
            if (_modifiers.Remove(modifierId))
                ClampVitals();
        }

        // Modifiers that outlast battles and effects (learned skills); ClearEffects leaves them
        internal void SetLastingModifier(string modifierId, CombatStat stat, ModifierOperation operation, float value)
        {
            _lasting[modifierId] = new StatModifier(stat, operation, value);
            ClampVitals();
        }

        internal void RemoveLastingModifier(string modifierId)
        {
            if (_lasting.Remove(modifierId))
                ClampVitals();
        }

        internal void SetControl(string controlId, bool active)
        {
            if (active)
                _controls.Add(controlId);
            else
                _controls.Remove(controlId);
        }

        /// <summary>
        /// Back to no effects at all (the StatusEffects module took them off; this only makes sure).
        /// </summary>
        internal void ClearEffects()
        {
            _modifiers.Clear();
            _controls.Clear();
            ClampVitals();
        }

        internal IEnumerable<string> ModifierIds => _modifiers.Keys.ToList();

        private void ClampVitals()
        {
            Health = Math.Min(Health, MaxHealth);
            Mana = Math.Min(Mana, MaxMana);
        }

        internal static int Round(float value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);

        private readonly struct StatModifier
        {
            public StatModifier(CombatStat stat, ModifierOperation operation, float value)
            {
                Stat = stat;
                Operation = operation;
                Value = value;
            }

            public CombatStat Stat { get; }
            public ModifierOperation Operation { get; }
            public float Value { get; }
        }
    }
}
