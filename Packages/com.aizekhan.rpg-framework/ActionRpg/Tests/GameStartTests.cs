using NUnit.Framework;

namespace RPGFramework.ActionRpg.Tests
{
    /// <summary>
    /// A start menu's choice carries how to play together, so a menu scene can host or join over the internet (FG-34).
    /// </summary>
    public sealed class GameStartTests
    {
        [Test]
        public void Host_Via_KeepsTheKind_AndNamesTheProvider()
        {
            var start = GameStart.Host.Via(" unity-relay ");
            Assert.That(start.Kind, Is.EqualTo(GameStartKind.Host));
            Assert.That(start.Provider, Is.EqualTo("unity-relay"));
            Assert.That(start.ToString(), Is.EqualTo("Host via unity-relay"));
            Assert.That(GameStart.Host.Provider, Is.Empty, "no provider: the host's own choice");
        }

        [Test]
        public void Join_WithAProvider_KeepsTheCode()
        {
            var start = GameStart.Join(" AB12CD ", "unity-relay");
            Assert.That(start.Kind, Is.EqualTo(GameStartKind.Join));
            Assert.That(start.JoinCode, Is.EqualTo("AB12CD"));
            Assert.That(start.Provider, Is.EqualTo("unity-relay"));
            Assert.That(GameStart.Join("10.0.0.2").Via("lan").JoinCode, Is.EqualTo("10.0.0.2"));
            Assert.That(default(GameStart).Provider, Is.Empty);
        }
    }
}
