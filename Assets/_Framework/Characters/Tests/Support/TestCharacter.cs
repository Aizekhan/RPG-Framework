namespace RPGFramework.Characters.Tests
{
    /// <summary>
    /// Concrete character for tests (MonoBehaviours cannot live in editor-only test assemblies).
    /// </summary>
    public sealed class TestCharacter : CharacterBase
    {
        public int Deaths { get; private set; }
        public int Revives { get; private set; }

        protected override void OnDeath() => Deaths++;

        protected override void OnRevive() => Revives++;
    }
}
