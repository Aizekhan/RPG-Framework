using RPGFramework.Stats;

namespace RPGFramework.Characters
{
    /// <summary>
    /// Base interface for all characters (Player, NPC, Enemy).
    /// Provides unified access to character data and systems.
    /// </summary>
    public interface ICharacter
    {
        /// <summary>
        /// Character's display name.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Character's current level.
        /// </summary>
        int Level { get; }

        /// <summary>
        /// Character's class/archetype (e.g., "Warrior", "Mage").
        /// </summary>
        string CharacterClass { get; }

        /// <summary>
        /// Character's stats (health, mana, strength, etc.).
        /// Game layer provides concrete implementation with specific stat properties.
        /// </summary>
        IStats Stats { get; }

        /// <summary>
        /// Character's inventory system.
        /// Game layer provides concrete implementation.
        /// </summary>
        object Inventory { get; }

        /// <summary>
        /// Character's progression data (XP, skill points).
        /// Game layer provides concrete implementation.
        /// </summary>
        object Progression { get; }

        /// <summary>
        /// Character's unique entity ID in ECS.
        /// </summary>
        int EntityId { get; }

        /// <summary>
        /// Whether character is alive.
        /// </summary>
        bool IsAlive { get; }

        /// <summary>
        /// Whether character is player-controlled.
        /// </summary>
        bool IsPlayer { get; }
    }
}
