using System.Collections.Generic;
using RPGFramework.Combat;
using RPGFramework.Interaction;
using RPGFramework.Inventory.Runtime;
using RPGFramework.Items;
using RPGFramework.Quests;

namespace RPGFramework.ActionRpg
{
    /// <summary>
    /// The game as the screen sees it: who is in the world, the party's level, backpack and quests, and the two
    /// actions a player takes. <see cref="ActorWorld"/> is the game itself (single player or co-op host);
    /// <c>RemoteWorld</c> mirrors a host's game on a joined client. Views and the HUD use only this.
    /// </summary>
    public interface IActorWorld
    {
        ActionRpgSettings Settings { get; }
        bool IsStarted { get; }

        /// <summary>
        /// The player this machine controls.
        /// </summary>
        int PlayerId { get; }

        Actor Player { get; }

        /// <summary>
        /// Everyone in the world, players included.
        /// </summary>
        IEnumerable<Actor> Actors { get; }

        IEnumerable<int> LivingEnemies { get; }

        /// <summary>
        /// Every player in the party (this one and, in co-op, the others) who is alive.
        /// </summary>
        IEnumerable<int> LivingPlayers { get; }

        Actor Get(int entityId);

        /// <summary>
        /// The first may attack the second (both exist, differ, and the hostility rules say they fight).
        /// </summary>
        bool AreHostile(int attackerId, int targetId);

        int Kills { get; }
        int Level { get; }
        long TotalExperience { get; }
        long ExperienceForNextLevel { get; }
        int CurrentWave { get; }
        IReadOnlyList<string> Log { get; }

        /// <summary>
        /// <see cref="Log"/> as lines to translate (<see cref="LogLine.In"/>); empty where only the English lines are
        /// known (a friend's game seen from a joined machine).
        /// </summary>
        IReadOnlyList<LogLine> LogLines { get; }
        IEnumerable<(IQuest Quest, IQuestProgress Progress)> ActiveQuests { get; }
        IReadOnlyList<string> CompletedQuests { get; }
        IInventoryContainer<IItem> Inventory { get; }

        /// <summary>
        /// The PvP match: sides, score, rounds, time; null when the game is not PvP.
        /// </summary>
        PvpStatus PvpStatus { get; }

        /// <summary>
        /// An attack, with what kind of hit it is (tags such as a hit zone, the ability or weapon that dealt it).
        /// On a co-op client it is sent to the host, which decides the outcome.
        /// </summary>
        /// <returns>Damage dealt; 0 when nothing was hit, or on a client where the host deals it.</returns>
        float Attack(int attackerId, int targetId, AttackContext context = default);

        /// <summary>
        /// Brings this machine's player back after a fall. Refused in PvP, where the match brings players back.
        /// </summary>
        bool RevivePlayer();

        /// <summary>
        /// Status effects on a character now (empty when the game has none).
        /// </summary>
        IEnumerable<ActorEffect> EffectsOn(int entityId);

        /// <summary>
        /// This machine's player uses an ability (on a target, or -1). On a co-op client it is sent to the host.
        /// </summary>
        /// <returns>False when it was refused here; a client cannot know yet and returns true once sent.</returns>
        bool UseAbility(string abilityId, int targetId = -1);

        /// <summary>
        /// Seconds until this machine's player can use the ability again (0 when it can).
        /// </summary>
        float CooldownLeft(string abilityId);

        /// <summary>
        /// Seconds until a character (a friend's hero, an enemy) can use the ability again (0 when it can).
        /// </summary>
        float CooldownLeft(int entityId, string abilityId);

        /// <summary>
        /// How much of a currency this machine's player has (their own, or the party's).
        /// </summary>
        long CurrencyOf(string currency = RPGFramework.Trading.Currencies.Gold);

        /// <summary>
        /// This machine's player takes a trader's offer (buys, sells). On a co-op client it is sent to the host.
        /// </summary>
        /// <returns>False when it was refused here; a client cannot know yet and returns true once sent.</returns>
        bool TakeOffer(string offerId);

        /// <summary>
        /// The places players use (chests, doors, traders); on a co-op client the host's, as it last reported them.
        /// </summary>
        IInteractionService Interactions { get; }

        /// <summary>
        /// The open place nearest to this machine's player within reach (for the prompt), or null.
        /// </summary>
        InteractionPoint NearestInteraction { get; }

        /// <summary>
        /// This machine's player uses a place (the nearest when <paramref name="pointId"/> is null). On a co-op
        /// client it is sent to the host, which decides.
        /// </summary>
        /// <returns>False when it was refused here; a client cannot know yet and returns true once sent.</returns>
        bool Interact(string pointId = null);

        /// <summary>
        /// A rule paused the game (<c>IGamePause</c>; on a co-op client, the host's): the kit's controls and enemy
        /// brains stand still until it resumes.
        /// </summary>
        bool IsPaused { get; }

        /// <summary>
        /// The set this machine's player wears (<see cref="ActorGear.UseLoadout"/>; "" for the default).
        /// </summary>
        string Loadout { get; }

        /// <summary>
        /// What this machine's player wears in a slot of <see cref="ActionRpgSettings.GearSlots"/>, or null.
        /// </summary>
        IItem WornIn(string slotId);

        /// <summary>
        /// This machine's player puts an item from their backpack on (in a slot, or the first that takes it). On a
        /// co-op client it is sent to the host.
        /// </summary>
        /// <returns>False when it was refused here; a client cannot know yet and returns true once sent.</returns>
        bool Equip(string itemId, string slotId = null);

        /// <summary>
        /// This machine's player takes off what is in a slot (back to the backpack). On a co-op client it is sent
        /// to the host.
        /// </summary>
        bool Unequip(string slotId);

        /// <summary>
        /// This machine's player wears another of their sets (another hero). On a co-op client it is sent to the host.
        /// </summary>
        bool UseLoadout(string loadout);
    }
}
