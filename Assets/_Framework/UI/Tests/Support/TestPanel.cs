namespace RPGFramework.UI.Tests
{
    // MonoBehaviours cannot live in an editor-only test assembly, hence this runtime support assembly
    public sealed class TestPanel : UIViewBase
    {
        public int ShowCount { get; private set; }
        public int HideCount { get; private set; }

        protected override void OnShow() => ShowCount++;

        protected override void OnHide() => HideCount++;
    }
}
