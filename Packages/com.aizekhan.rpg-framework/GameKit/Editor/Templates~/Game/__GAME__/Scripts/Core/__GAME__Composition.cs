using System;
using RPGFramework.ActionRpg;
using RPGFramework.Core.Composition;

namespace __GAME__
{
    /// <summary>
    /// The game's own modules, on top of the action RPG (combat, progression, quests, items, loot, waves) that the
    /// host builds from the Game Setup asset. The scene host and the tests both call this, so they run the same game.
    /// </summary>
    public static class __GAME__Composition
    {
        public static void Configure(IGameCompositionBuilder builder)
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            builder
                // rpg-framework:modules (RPG Framework > Game Kit > New Feature adds its module above this line)
                .UseModule(new __GAME__Module());
        }

        /// <summary>
        /// The whole game from settings and content (what the host does with the Game Setup asset).
        /// </summary>
        public static ActionRpgModule ConfigureWithActionRpg(IGameCompositionBuilder builder, ActionRpgSettings settings,
            ActionRpgContent content)
        {
            var actionRpg = ActionRpgComposition.Configure(builder, settings, content);
            Configure(builder);
            return actionRpg;
        }
    }
}
