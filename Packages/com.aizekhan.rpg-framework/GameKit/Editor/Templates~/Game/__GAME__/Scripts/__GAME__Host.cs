using RPGFramework.ActionRpg;
using RPGFramework.Core.Composition;

namespace __GAME__
{
    /// <summary>
    /// Scene entry point. Everything designers tune (characters, prefabs, waves, items, loot, quests) is in the
    /// Game Setup asset in the 'Setup' field; this class only adds the game's own rules (__GAME__Composition).
    /// </summary>
    public sealed class __GAME__Host : ActionRpgHost
    {
        protected override void ConfigureGameModules(IGameCompositionBuilder builder) => __GAME__Composition.Configure(builder);
    }
}
