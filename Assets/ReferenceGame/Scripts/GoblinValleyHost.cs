using RPGFramework.ActionRpg;
using RPGFramework.Core.Composition;
using UnityEngine;

namespace ReferenceGame
{
    /// <summary>
    /// Scene entry point of Goblin Valley. Characters, prefabs, waves, items, loot and quests are in the Game Setup
    /// asset (Content/GoblinValleySetup); this class only adds the game's own rule, the kill streak.
    /// </summary>
    public sealed class GoblinValleyHost : ActionRpgHost
    {
        [Tooltip("Kills in a row without being hurt that earn bonus loot")]
        [SerializeField] private KillStreakSettings _killStreak = new KillStreakSettings();

        /// <summary>
        /// The kill streak rule (null when the game did not start).
        /// </summary>
        public KillStreak KillStreak => World != null ? Host.Container.Resolve<KillStreak>() : null;

        protected override void ConfigureGameModules(IGameCompositionBuilder builder) =>
            GoblinValleyComposition.Configure(builder, _killStreak);
    }
}
