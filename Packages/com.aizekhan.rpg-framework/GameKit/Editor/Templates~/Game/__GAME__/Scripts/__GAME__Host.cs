using RPGFramework.Bootstrap.Unity;
using RPGFramework.Core.Composition;

namespace __GAME__
{
    /// <summary>
    /// Scene entry point: starts the framework with the game's modules and ticks its systems. One per game, in the
    /// first scene. Scene objects the views need (camera, UI roots, config assets) are [SerializeField] fields here.
    /// </summary>
    public sealed class __GAME__Host : GameHostBehaviour
    {
        protected override void ConfigureModules(IGameCompositionBuilder builder) => __GAME__Composition.Configure(builder);
    }
}
