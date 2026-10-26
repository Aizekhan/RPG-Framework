using RPGFramework.UINavigation.Unity;

namespace RPGFramework.UINavigation.Tests
{
    // MonoBehaviours cannot live in an editor-only test assembly, hence this runtime support assembly
    public sealed class TestScreen : NavigableView
    {
    }
}
