namespace RPGFramework.Combat
{
    /// <summary>
    /// Standard RPG resource types for resource pool components.
    /// </summary>
    public enum ResourceType
    {
        /// <summary>Health/Hit Points - primary life resource</summary>
        Health = 0,

        /// <summary>Mana/Magic Points - spell casting resource</summary>
        Mana = 1,

        /// <summary>Stamina/Endurance - physical action resource</summary>
        Stamina = 2,

        /// <summary>Energy - generic action resource</summary>
        Energy = 3,

        /// <summary>Rage - combat resource (typically gained from damage)</summary>
        Rage = 4,

        /// <summary>Focus/Concentration - precision/accuracy resource</summary>
        Focus = 5
    }
}
