using NUnit.Framework;
using RPGFramework.UI.HUD;

namespace RPGFramework.UI.Tests
{
    /// <summary>
    /// A health bar's trail: the chunk a blow took holds, then drains to the health; another blow holds it again,
    /// healing snaps it, and every bar keeps its own.
    /// </summary>
    public sealed class HealthTrailTests
    {
        [Test]
        public void TheChunk_Holds_ThenDrains_AndABlowOrAHealResetsIt()
        {
            var trail = new HealthTrail(hold: 0.5f, drainPerSecond: 1f);
            Assert.That(trail.Update(1, 1f, 0f), Is.EqualTo(1f));

            Assert.That(trail.Update(1, 0.6f, 1f), Is.EqualTo(1f), "the chunk stays whole");
            Assert.That(trail.ChunkOf(1), Is.EqualTo(0.4f).Within(1e-5f));
            Assert.That(trail.Update(1, 0.6f, 1.4f), Is.EqualTo(1f), "still holding");
            Assert.That(trail.Update(1, 0.6f, 1.7f), Is.EqualTo(0.8f).Within(1e-5f), "0.2 s of draining");
            Assert.That(trail.Update(1, 0.6f, 1.7f), Is.EqualTo(0.8f).Within(1e-5f), "drawn twice: drains once");

            Assert.That(trail.Update(1, 0.5f, 1.8f), Is.EqualTo(0.7f).Within(1e-5f), "another blow holds what is left");
            Assert.That(trail.Update(1, 0.5f, 2.2f), Is.EqualTo(0.7f).Within(1e-5f));
            Assert.That(trail.Update(1, 0.5f, 5f), Is.EqualTo(0.5f), "drained down to the health");

            trail.Update(2, 0.3f, 5f);
            Assert.That(trail.Update(2, 0.9f, 5.1f), Is.EqualTo(0.9f), "healing snaps it");
            Assert.That(trail.Count, Is.EqualTo(2));
            Assert.That(trail.Forget(2), Is.True);
            trail.Clear();
            Assert.That(trail.Count, Is.Zero);
        }
    }
}
