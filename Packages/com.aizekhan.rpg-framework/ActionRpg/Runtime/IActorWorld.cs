using System.Collections.Generic;
using RPGFramework.Combat;
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
    }
}
