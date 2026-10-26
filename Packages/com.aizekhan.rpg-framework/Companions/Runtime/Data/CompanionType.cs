namespace RPGFramework.Companions.Runtime
{
    /// <summary>
    /// Defines the category/type of companion.
    /// </summary>
    public enum CompanionType
    {
        /// <summary>
        /// Combat-focused pet that fights alongside the player.
        /// Examples: Wolf, Bear, Combat Drone.
        /// </summary>
        CombatPet = 0,

        /// <summary>
        /// Rideable creature for faster travel.
        /// Examples: Horse, Dragon, Flying Mount.
        /// </summary>
        Mount = 1,

        /// <summary>
        /// Non-combat pet that provides passive bonuses or cosmetic value.
        /// Examples: Cat (luck bonus), Parrot (cosmetic), Dog (item finding).
        /// </summary>
        NonCombatPet = 2,

        /// <summary>
        /// Temporary magical summon with limited duration.
        /// Examples: Fire Elemental, Demon, Spirit Guardian.
        /// </summary>
        Summon = 3,

        /// <summary>
        /// Magical familiar bound to the player, often with special abilities.
        /// Examples: Raven (scouting), Owl (wisdom bonus), Snake (poison immunity).
        /// </summary>
        Familiar = 4
    }
}
