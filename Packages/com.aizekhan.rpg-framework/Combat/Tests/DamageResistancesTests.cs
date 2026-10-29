using NUnit.Framework;

namespace RPGFramework.Combat.Tests
{
    /// <summary>
    /// Resistances from several sources add up within the limits, a weakness takes more, an immunity takes all,
    /// a source taken away takes its part with it, and mixed damage is split between its elements.
    /// </summary>
    public sealed class DamageResistancesTests
    {
        [Test]
        public void SourcesAddUp_WithinTheLimits_AndGoAwayOnTheirOwn()
        {
            var resistances = new DamageResistances();
            resistances.SetBase(1, new[] { new ElementResistance("fire", 0.3f), new ElementResistance("ice", -0.5f) });
            resistances.Set(1, "fire", 0.4f, "gear:ring");
            resistances.Set(1, "fire", 0.2f, "buff");

            Assert.That(resistances.Of(1, "fire"), Is.EqualTo(0.75f), "0.9 held at the 0.75 limit");
            Assert.That(resistances.Apply(1, "fire", 100f), Is.EqualTo(25f).Within(1e-4f));
            Assert.That(resistances.Apply(1, "ice", 100f), Is.EqualTo(150f).Within(1e-4f), "a weakness");
            Assert.That(resistances.Apply(1, "poison", 100f), Is.EqualTo(100f));
            Assert.That(resistances.Apply(2, "fire", 100f), Is.EqualTo(100f), "another entity");

            Assert.That(resistances.RemoveSource(1, "gear:ring"), Is.True);
            Assert.That(resistances.Of(1, "fire"), Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(resistances.RemoveSource(1, "gear:ring"), Is.False);

            resistances.Set(1, "ice", -3f, "curse");
            Assert.That(resistances.Of(1, "ice"), Is.EqualTo(-1f), "at most double");
            resistances.MaxResistance = 0.9f;
            resistances.Set(1, "fire", 0f, "buff");
            Assert.That(resistances.Of(1, "fire"), Is.EqualTo(0.3f).Within(1e-4f), "0 takes a source away");
        }

        [Test]
        public void Immunity_TakesAll_AndMixedDamageIsSplit()
        {
            var resistances = new DamageResistances();
            resistances.SetBase(1, new[] { new ElementResistance("poison", 0f, immune: true) });
            resistances.Set(1, "fire", 0.5f);
            resistances.SetImmune(1, "ice", true, "potion");

            Assert.That(resistances.IsImmune(1, "poison"), Is.True);
            Assert.That(resistances.Apply(1, "poison", 50f), Is.EqualTo(0f));
            Assert.That(resistances.Apply(1, new[] { "fire", "physical" }, 100f), Is.EqualTo(75f).Within(1e-4f));
            Assert.That(resistances.Apply(1, new string[0], 100f), Is.EqualTo(100f));
            Assert.That(resistances.ElementsOf(1), Is.EqualTo(new[] { "fire", "ice", "poison" }));

            resistances.RemoveSource(1, "potion");
            Assert.That(resistances.IsImmune(1, "ice"), Is.False);
            resistances.Forget(1);
            Assert.That(resistances.ElementsOf(1), Is.Empty);
            Assert.That(() => resistances.Set(1, "", 0.1f), Throws.ArgumentException);
        }
    }
}
