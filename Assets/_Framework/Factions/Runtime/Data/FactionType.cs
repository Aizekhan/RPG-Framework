namespace RPGFramework.Factions
{
    /// <summary>
    /// Types/categories of factions in the game world.
    /// </summary>
    public enum FactionType
    {
        /// <summary>
        /// Professional guild (Mages Guild, Thieves Guild, etc.).
        /// </summary>
        Guild = 0,

        /// <summary>
        /// Political entity (Kingdom, Empire, City-State).
        /// </summary>
        Kingdom = 1,

        /// <summary>
        /// Tribal or family-based organization.
        /// </summary>
        Clan = 2,

        /// <summary>
        /// Religious organization (Church, Cult, Order).
        /// </summary>
        Religion = 3,

        /// <summary>
        /// Criminal organization (Thieves, Assassins, Smugglers).
        /// </summary>
        Criminal = 4,

        /// <summary>
        /// Military faction (Army, Mercenaries, Guards).
        /// </summary>
        Military = 5,

        /// <summary>
        /// Merchant or trade organization.
        /// </summary>
        Merchant = 6,

        /// <summary>
        /// General/other faction type.
        /// </summary>
        Other = 99
    }
}
