using System;

namespace RPGFramework.Guilds.Runtime
{
    /// <summary>
    /// Flags enum for guild privileges (benefits members receive).
    /// Privileges are passive benefits granted to members based on rank.
    /// </summary>
    [Flags]
    public enum GuildPrivilege
    {
        /// <summary>
        /// No privileges.
        /// </summary>
        None = 0,

        /// <summary>
        /// Can access the guild bank/storage.
        /// </summary>
        AccessBank = 1 << 0,

        /// <summary>
        /// Receives discount on repair costs.
        /// </summary>
        RepairDiscount = 1 << 1,

        /// <summary>
        /// Receives discount on travel/teleportation costs.
        /// </summary>
        TravelDiscount = 1 << 2,

        /// <summary>
        /// Can use guild hall facilities.
        /// </summary>
        UseGuildHall = 1 << 3,

        /// <summary>
        /// Receives bonus experience from guild buffs.
        /// </summary>
        ExperienceBonus = 1 << 4,

        /// <summary>
        /// Receives bonus loot from guild buffs.
        /// </summary>
        LootBonus = 1 << 5,

        /// <summary>
        /// Can participate in guild raids/dungeons.
        /// </summary>
        ParticipateInRaids = 1 << 6,

        /// <summary>
        /// Can participate in guild PvP activities.
        /// </summary>
        ParticipateInPvP = 1 << 7,

        /// <summary>
        /// Can use guild chat channels.
        /// </summary>
        UseGuildChat = 1 << 8,

        /// <summary>
        /// Can use guild officer chat channels.
        /// </summary>
        UseOfficerChat = 1 << 9,

        /// <summary>
        /// Receives priority in guild event signups.
        /// </summary>
        EventPriority = 1 << 10,

        /// <summary>
        /// Can use guild mounts/cosmetics.
        /// </summary>
        GuildCosmetics = 1 << 11,

        /// <summary>
        /// Receives share of guild quest rewards.
        /// </summary>
        QuestRewards = 1 << 12,

        /// <summary>
        /// Can summon/be summoned to guild locations.
        /// </summary>
        GuildSummon = 1 << 13,

        /// <summary>
        /// Receives periodic salary from guild treasury.
        /// </summary>
        GuildSalary = 1 << 14,

        /// <summary>
        /// Can access exclusive guild vendor.
        /// </summary>
        AccessGuildVendor = 1 << 15,

        /// <summary>
        /// All privileges (maximum benefits).
        /// </summary>
        All = ~0
    }
}
