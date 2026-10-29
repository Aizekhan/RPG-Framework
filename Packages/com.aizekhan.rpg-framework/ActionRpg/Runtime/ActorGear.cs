using System;
using System.Collections.Generic;
using System.Linq;
using RPGFramework.Events;
using RPGFramework.Items;
using RPGFramework.Stats;

namespace RPGFramework.ActionRpg
{
    /// <summary>
    /// What the players wear: the slots of <see cref="ActionRpgSettings.GearSlots"/>, filled with items from their
    /// backpack that are gear (<see cref="ActionRpgContent.Gear"/>). Putting an item on takes it out of the
    /// backpack, taking it off puts it back; a worn item's bonuses are on the character (the kit's own stats) or
    /// read by the game (<see cref="BonusesOf"/>). Each player can keep several sets (<see cref="UseLoadout"/>: one
    /// per hero of theirs, sharing one backpack); only the set in use counts. Saved with the player's progress and
    /// sent to their machine in co-op, where <see cref="IActorWorld.Equip"/> asks the host.
    /// </summary>
    public sealed class ActorGear
    {
        /// <summary>
        /// The set a player wears until they pick another.
        /// </summary>
        public const string DefaultLoadout = "";

        private readonly ActorWorld _world;
        private readonly IEventBus _bus;
        private readonly List<GearSlot> _slots;
        private readonly Dictionary<string, GearData> _gear = new Dictionary<string, GearData>(StringComparer.Ordinal);
        private readonly Dictionary<int, Wardrobe> _worn = new Dictionary<int, Wardrobe>();

        private sealed class Wardrobe
        {
            public string Active = DefaultLoadout;
            public readonly Dictionary<string, Dictionary<string, IItem>> Sets =
                new Dictionary<string, Dictionary<string, IItem>>(StringComparer.Ordinal);

            public Dictionary<string, IItem> Set(string loadout)
            {
                if (!Sets.TryGetValue(loadout, out var set))
                    Sets[loadout] = set = new Dictionary<string, IItem>(StringComparer.Ordinal);
                return set;
            }
        }

        internal ActorGear(ActorWorld world, IEnumerable<GearSlot> slots, IEventBus bus)
        {
            _world = world;
            _bus = bus;
            _slots = (slots ?? Enumerable.Empty<GearSlot>()).Where(s => s != null && !string.IsNullOrEmpty(s.Id)).ToList();
        }

        public IReadOnlyList<GearSlot> Slots => _slots;

        public GearSlot Slot(string slotId) => _slots.FirstOrDefault(s => s.Id == slotId);

        /// <summary>
        /// Makes an item wearable (the module registers <see cref="ActionRpgContent.Gear"/>).
        /// </summary>
        /// <returns>False when the item already is gear.</returns>
        public bool Register(GearData gear)
        {
            if (gear == null || string.IsNullOrEmpty(gear.ItemId))
                throw new ArgumentException("Gear needs an item id", nameof(gear));
            if (_gear.ContainsKey(gear.ItemId))
                return false;
            _gear[gear.ItemId] = gear;
            return true;
        }

        /// <summary>
        /// The gear an item is, or null when it cannot be worn.
        /// </summary>
        public GearData Of(string itemId)
        {
            if (itemId == null)
                return null;
            if (_gear.TryGetValue(itemId, out var gear))
                return gear;
            // Rolled gear (affixes in the id) is built from the content the first time
            gear = RolledItems.IsRolled(itemId) ? _world.Content?.FindGear(itemId) : null;
            if (gear != null)
                _gear[itemId] = gear;
            return gear;
        }

        /// <summary>
        /// The set a player wears now.
        /// </summary>
        public string LoadoutOf(int playerId) => _worn.TryGetValue(playerId, out var wardrobe) ? wardrobe.Active : DefaultLoadout;

        /// <summary>
        /// The sets a player has items in (the one in use included).
        /// </summary>
        public IReadOnlyList<string> LoadoutsOf(int playerId)
        {
            var sets = new List<string> { LoadoutOf(playerId) };
            if (_worn.TryGetValue(playerId, out var wardrobe))
                sets.AddRange(wardrobe.Sets.Where(s => s.Value.Count > 0 && s.Key != wardrobe.Active).Select(s => s.Key));
            return sets;
        }

        /// <summary>
        /// The item in a slot of a player's set (null: the set in use), or null.
        /// </summary>
        public IItem WornIn(int playerId, string slotId, string loadout = null)
        {
            if (slotId == null || !_worn.TryGetValue(playerId, out var wardrobe))
                return null;
            return wardrobe.Sets.TryGetValue(loadout ?? wardrobe.Active, out var set) && set.TryGetValue(slotId, out var item)
                ? item
                : null;
        }

        /// <summary>
        /// Every worn item of a player's set (null: the set in use), in slot order.
        /// </summary>
        public IReadOnlyList<(GearSlot Slot, IItem Item)> WornBy(int playerId, string loadout = null)
        {
            var worn = new List<(GearSlot, IItem)>();
            foreach (var slot in _slots)
            {
                var item = WornIn(playerId, slot.Id, loadout);
                if (item != null)
                    worn.Add((slot, item));
            }
            return worn;
        }

        /// <summary>
        /// The bonuses of everything in the set a player wears now: what the game adds to its own stats.
        /// </summary>
        public IReadOnlyList<GearBonus> BonusesOf(int playerId) =>
            WornBy(playerId).SelectMany(w => Of(w.Item.ItemId)?.Bonuses ?? new List<GearBonus>()).Where(b => b != null).ToList();

        /// <summary>
        /// A player puts an item from their backpack on: in <paramref name="slotId"/>, or the first free slot that
        /// takes it (the first one that takes it when all are full). What was there goes back to the backpack.
        /// </summary>
        /// <param name="loadout">The set to put it in; null: the set in use.</param>
        /// <returns>False when the player does not have it, it is not gear, they are below its level, no slot takes
        /// it, or the backpack has no room for what comes off.</returns>
        public bool Equip(int playerId, string itemId, string slotId = null, string loadout = null)
        {
            var gear = Of(itemId);
            var backpack = _world.InventoryOf(playerId);
            if (gear == null || backpack == null || _world.LevelOf(playerId) < gear.MinimumLevel)
                return false;
            var item = backpack.GetAllItems().FirstOrDefault(i => i != null && i.ItemId == itemId);
            if (item == null)
                return false;
            var wardrobe = WardrobeOf(playerId);
            string setId = loadout ?? wardrobe.Active;
            var set = wardrobe.Set(setId);
            var slot = slotId != null ? Slot(slotId) : _slots.FirstOrDefault(s => s.Takes(gear) && !set.ContainsKey(s.Id))
                                                       ?? _slots.FirstOrDefault(s => s.Takes(gear));
            if (slot == null || !slot.Takes(gear))
                return false;

            set.TryGetValue(slot.Id, out var previous);
            if (!backpack.Remove(item))
                return false;
            if (previous != null && !backpack.Add(previous))
            {
                backpack.Add(item);
                return false;
            }
            bool inUse = setId == wardrobe.Active;
            if (inUse && previous != null)
                TakeOff(playerId, slot.Id, Of(previous.ItemId));
            set[slot.Id] = item;
            if (inUse)
                PutOn(playerId, slot.Id, gear);
            _bus.Publish(new GearChangedEvent(playerId, setId, slot.Id, item.ItemId, previous?.ItemId ?? ""));
            return true;
        }

        /// <summary>
        /// A player takes off what is in a slot; it goes back to their backpack.
        /// </summary>
        /// <returns>False when the slot is empty or the backpack is full.</returns>
        public bool Unequip(int playerId, string slotId, string loadout = null)
        {
            var backpack = _world.InventoryOf(playerId);
            if (backpack == null || slotId == null || !_worn.TryGetValue(playerId, out var wardrobe))
                return false;
            string setId = loadout ?? wardrobe.Active;
            if (!wardrobe.Sets.TryGetValue(setId, out var set) || !set.TryGetValue(slotId, out var item) || !backpack.Add(item))
                return false;
            set.Remove(slotId);
            if (setId == wardrobe.Active)
                TakeOff(playerId, slotId, Of(item.ItemId));
            _bus.Publish(new GearChangedEvent(playerId, setId, slotId, "", item.ItemId));
            return true;
        }

        /// <summary>
        /// A player wears another of their sets (another hero): the old set's bonuses come off, the new one's go on.
        /// A new id starts an empty set.
        /// </summary>
        public bool UseLoadout(int playerId, string loadout)
        {
            if (loadout == null || _world.ProgressOwnerOf(playerId) < 0)
                return false;
            var wardrobe = WardrobeOf(playerId);
            string previous = wardrobe.Active;
            if (previous == loadout)
                return true;
            SetBonuses(playerId, wardrobe, on: false);
            wardrobe.Active = loadout;
            wardrobe.Set(loadout);
            SetBonuses(playerId, wardrobe, on: true);
            _bus.Publish(new LoadoutChangedEvent(playerId, loadout, previous));
            return true;
        }

        /// <summary>
        /// A player's worn items, every set, for a save.
        /// </summary>
        public List<GearRecord> RecordsOf(int playerId)
        {
            var records = new List<GearRecord>();
            if (!_worn.TryGetValue(playerId, out var wardrobe))
                return records;
            foreach (var set in wardrobe.Sets.OrderBy(s => s.Key, StringComparer.Ordinal))
            {
                foreach (var slot in _slots)
                {
                    if (set.Value.TryGetValue(slot.Id, out var item))
                        records.Add(new GearRecord { Loadout = set.Key, SlotId = slot.Id, ItemId = item.ItemId });
                }
            }
            return records;
        }

        /// <summary>
        /// Puts saved gear back on (not from the backpack: the save keeps them apart). Items that are no longer gear,
        /// and slots that no longer exist or no longer take them, are skipped.
        /// </summary>
        public void Restore(int playerId, IEnumerable<GearRecord> records, string loadout)
        {
            var wardrobe = WardrobeOf(playerId);
            SetBonuses(playerId, wardrobe, on: false);
            wardrobe.Sets.Clear();
            wardrobe.Active = loadout ?? DefaultLoadout;
            foreach (var record in records ?? Enumerable.Empty<GearRecord>())
            {
                var gear = Of(record?.ItemId);
                var slot = Slot(record?.SlotId);
                var item = gear != null ? _world.ItemOf(gear.ItemId) : null;
                if (item == null || slot == null || !slot.Takes(gear))
                    continue;
                wardrobe.Set(record.Loadout ?? DefaultLoadout)[slot.Id] = item;
            }
            SetBonuses(playerId, wardrobe, on: true);
        }

        /// <summary>
        /// A player leaves: their gear goes back into the backpack they shared, or with them.
        /// </summary>
        internal void Forget(int playerId, bool backToBackpack)
        {
            if (!_worn.TryGetValue(playerId, out var wardrobe))
                return;
            _worn.Remove(playerId);
            if (!backToBackpack)
                return;
            var backpack = _world.InventoryOf(_world.PlayerId);
            foreach (var item in wardrobe.Sets.Values.SelectMany(s => s.Values))
                backpack?.Add(item);
        }

        private Wardrobe WardrobeOf(int playerId)
        {
            if (!_worn.TryGetValue(playerId, out var wardrobe))
                _worn[playerId] = wardrobe = new Wardrobe();
            return wardrobe;
        }

        private void SetBonuses(int playerId, Wardrobe wardrobe, bool on)
        {
            if (!wardrobe.Sets.TryGetValue(wardrobe.Active, out var set))
                return;
            foreach (var worn in set)
            {
                if (on)
                    PutOn(playerId, worn.Key, Of(worn.Value.ItemId));
                else
                    TakeOff(playerId, worn.Key, Of(worn.Value.ItemId));
            }
        }

        private static string ModifierId(string slotId, int index) => "gear:" + slotId + ":" + index;

        private void PutOn(int playerId, string slotId, GearData gear)
        {
            var actor = _world.Get(playerId);
            if (actor == null || gear == null)
                return;
            for (int i = 0; i < gear.Bonuses.Count; i++)
            {
                var bonus = gear.Bonuses[i];
                if (bonus != null && Enum.TryParse(bonus.Stat, false, out ActorStat stat) && Enum.IsDefined(typeof(ActorStat), stat))
                    actor.AddModifier(new StatModifier<ActorStat, float>(ModifierId(slotId, i), stat, bonus.Value, bonus.Type));
            }
        }

        private void TakeOff(int playerId, string slotId, GearData gear)
        {
            var actor = _world.Get(playerId);
            if (actor == null || gear == null)
                return;
            for (int i = 0; i < gear.Bonuses.Count; i++)
                actor.RemoveModifier(ModifierId(slotId, i));
        }
    }
}
