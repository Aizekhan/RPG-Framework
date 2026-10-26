using System;
using RPGFramework.Core.Composition;

namespace __GAME__
{
    /// <summary>
    /// The modules that make up __GAME__. The scene host and the tests both call this, so they run the same game.
    /// Framework modules (RPGFramework.Bootstrap.Gameplay: combat, progression, quests, items, loot) are added here too.
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
    }
}
