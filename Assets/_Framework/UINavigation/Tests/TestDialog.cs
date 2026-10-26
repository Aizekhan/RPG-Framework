using RPGFramework.UINavigation.Unity;

namespace RPGFramework.UINavigation.Tests
{
    public sealed class TestDialog : ModalView<bool>
    {
        public void Answer(bool yes) => Complete(yes);
    }
}
