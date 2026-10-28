using System.Collections.Generic;
using RPGFramework.Events;
using RPGFramework.Stats;

namespace RPGFramework.ActionRpg
{
    /// <summary>
    /// A place on a character where an item is worn: "head", "weapon_main", "ring_left". It takes gear of its
    /// <see cref="Kind"/>, so two ring slots both take "ring".
    /// </summary>
    [System.Serializable]
    public sealed class GearSlot
    {
        public string Id = "";
        public string Kind = "";

        /// <summary>
        /// The name shown for it; empty shows the id.
        /// </summary>
        public string DisplayName = "";

        public GearSlot()
        {
        }

        public GearSlot(string id, string kind = null, string displayName = null)
        {
            Id = id ?? "";
            Kind = kind ?? Id;
            DisplayName = displayName ?? "";
        }

        public bool Takes(GearData gear) => gear != null && string.Equals(Kind, gear.Kind, System.StringComparison.Ordinal);
    }

    /// <summary>
    /// What a stat becomes while an item is worn: <see cref="Stat"/> is an <see cref="ActorStat"/> name (MaxHealth,
    /// Attack, Defense), which the kit changes itself, or any stat of the game's own, which the game reads from
    /// <see cref="ActorGear.BonusesOf"/> when <see cref="GearChangedEvent"/> or <see cref="LoadoutChangedEvent"/> comes.
    /// </summary>
    public sealed class GearBonus
    {
        public string Stat = "";
        public float Value;
        public StatModifierType Type = StatModifierType.Additive;

        public GearBonus()
        {
        }

        public GearBonus(string stat, float value, StatModifierType type = StatModifierType.Additive)
        {
            Stat = stat ?? "";
            Value = value;
            Type = type;
        }
    }

    /// <summary>
    /// An item that can be worn: which slots take it (<see cref="Kind"/>), the level it needs and the stats it gives.
    /// The item itself is an ordinary item of the content (<see cref="ItemId"/>).
    /// </summary>
    public sealed class GearData
    {
        public string ItemId = "";
        public string Kind = "";
        public int MinimumLevel;
        public List<GearBonus> Bonuses = new List<GearBonus>();

        public GearData()
        {
        }

        public GearData(string itemId, string kind, params GearBonus[] bonuses)
        {
            ItemId = itemId ?? "";
            Kind = kind ?? "";
            if (bonuses != null)
                Bonuses.AddRange(bonuses);
        }
    }

    /// <summary>
    /// One worn item in a save: in which set (<see cref="Loadout"/>, "" for the default), in which slot.
    /// </summary>
    public sealed class GearRecord
    {
        public string Loadout { get; set; } = "";
        public string SlotId { get; set; } = "";
        public string ItemId { get; set; } = "";
    }

    /// <summary>
    /// A player put an item on, took one off or swapped one (<see cref="ItemId"/> empty: the slot is free now).
    /// </summary>
    public readonly struct GearChangedEvent : IEvent
    {
        public GearChangedEvent(int playerId, string loadout, string slotId, string itemId, string previousItemId)
        {
            PlayerId = playerId;
            Loadout = loadout;
            SlotId = slotId;
            ItemId = itemId;
            PreviousItemId = previousItemId;
        }

        public int PlayerId { get; }
        public string Loadout { get; }
        public string SlotId { get; }
        public string ItemId { get; }
        public string PreviousItemId { get; }
    }

    /// <summary>
    /// A player wears another set now (another hero of theirs): the stats of the old set are off, the new one's on.
    /// </summary>
    public readonly struct LoadoutChangedEvent : IEvent
    {
        public LoadoutChangedEvent(int playerId, string loadout, string previous)
        {
            PlayerId = playerId;
            Loadout = loadout;
            Previous = previous;
        }

        public int PlayerId { get; }
        public string Loadout { get; }
        public string Previous { get; }
    }
}
