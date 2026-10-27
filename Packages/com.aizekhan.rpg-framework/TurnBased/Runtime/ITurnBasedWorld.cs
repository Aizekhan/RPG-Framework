using System.Collections.Generic;
using RPGFramework.Inventory.Runtime;
using RPGFramework.Items;

namespace RPGFramework.TurnBased
{
    /// <summary>
    /// The run as the screen sees it: the party, the inventory and gold, where the run stands, the current battle, and
    /// the few actions a player takes outside a battle (inside one the screen calls <see cref="Battle.Perform"/>).
    /// Views and the HUD use only this.
    /// </summary>
    public interface ITurnBasedWorld
    {
        TurnBasedSettings Settings { get; }
        bool IsStarted { get; }

        /// <summary>
        /// The heroes in party order, the fallen included.
        /// </summary>
        IReadOnlyList<Combatant> Party { get; }

        IInventoryContainer<IItem> Inventory { get; }
        int Gold { get; }

        /// <summary>
        /// Encounters won so far (also the index of the next one).
        /// </summary>
        int EncounterIndex { get; }

        /// <summary>
        /// The fight that comes next; null when the run is complete.
        /// </summary>
        EncounterData NextEncounter { get; }

        /// <summary>
        /// The current battle, or the last one once it is over (until the next starts); null before the first.
        /// </summary>
        Battle Battle { get; }

        bool IsInBattle { get; }

        /// <summary>
        /// The party was defeated: nothing more can be fought until <see cref="Restart"/>.
        /// </summary>
        bool IsRunOver { get; }

        /// <summary>
        /// Every encounter was won.
        /// </summary>
        bool IsRunComplete { get; }

        IReadOnlyList<string> Log { get; }

        long ExperienceOf(int memberId);

        /// <summary>
        /// Total experience at which the member reaches its next level.
        /// </summary>
        long ExperienceForNextLevel(int memberId);

        /// <summary>
        /// Why <see cref="StartBattle"/> would fail, in plain words; null when it can start.
        /// </summary>
        string WhyCannotStartBattle();

        /// <summary>
        /// Starts the next encounter.
        /// </summary>
        Battle StartBattle();

        /// <summary>
        /// A new run from the first encounter: the party back at level 1 with full health, the starting items and gold.
        /// </summary>
        void Restart();
    }
}
