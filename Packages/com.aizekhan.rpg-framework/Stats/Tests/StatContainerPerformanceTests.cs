using System.Diagnostics;
using NUnit.Framework;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace RPGFramework.Stats.Tests
{
    /// <summary>
    /// Whole-number stats do not overflow (they stop at the type's limit), and recalculating a stat allocates nothing, so a thousand characters changing stats every frame cost no garbage.
    /// The benchmark prints its numbers to the test output (budgets are generous: they catch regressions, not jitter).
    /// </summary>
    public sealed class StatContainerPerformanceTests
    {
        private enum Stat
        {
            Health,
            Attack,
            Speed
        }

        [Test]
        public void WholeNumberStats_DoNotOverflow()
        {
            var stats = new StatContainer<Stat, int>();
            stats.Initialize();
            stats.SetBaseStat(Stat.Health, int.MaxValue - 5);
            stats.AddModifier(new StatModifier<Stat, int>("huge", Stat.Health, 100, StatModifierType.Additive));

            Assert.That(stats.GetFinalStat(Stat.Health), Is.EqualTo(int.MaxValue));

            stats.SetBaseStat(Stat.Health, int.MinValue + 5);
            stats.AddModifier(new StatModifier<Stat, int>("curse", Stat.Health, -100, StatModifierType.Additive));
            stats.RemoveModifier("huge");
            Assert.That(stats.GetFinalStat(Stat.Health), Is.EqualTo(int.MinValue));
        }

        [Test]
        public void Recalculating_AllocatesNothing()
        {
            var stats = new StatContainer<Stat, float>();
            stats.Initialize();
            stats.SetBaseStat(Stat.Attack, 10f);
            AddTypicalModifiers(ref stats);
            float value = 10f;
            stats.GetFinalStat(Stat.Attack); // warm up

            Assert.That(() =>
            {
                value += 1f;
                stats.SetBaseStat(Stat.Attack, value);
                stats.GetFinalStat(Stat.Attack);
            }, Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void Benchmark_AThousandCharacters_RecalculateEveryFrame()
        {
            const int characters = 1000;
            const int frames = 100;
            var all = new StatContainer<Stat, float>[characters];
            for (int i = 0; i < characters; i++)
            {
                all[i].Initialize();
                all[i].SetBaseStat(Stat.Attack, 10f + i);
                AddTypicalModifiers(ref all[i]);
            }

            var clock = Stopwatch.StartNew();
            float sum = 0f;
            for (int frame = 0; frame < frames; frame++)
            {
                for (int i = 0; i < characters; i++)
                {
                    all[i].SetBaseStat(Stat.Attack, 10f + i + frame);
                    sum += all[i].GetFinalStat(Stat.Attack);
                }
            }
            clock.Stop();

            double perFrame = clock.Elapsed.TotalMilliseconds / frames;
            TestContext.WriteLine($"[Benchmark] StatContainer: {characters} characters x 6 modifiers recalculated: {perFrame:0.000} ms per frame (sum {sum})");
            Assert.That(perFrame, Is.LessThan(4.0), "a quarter of a 60 fps frame");
        }

        private static void AddTypicalModifiers(ref StatContainer<Stat, float> stats)
        {
            // Gear, buffs and a debuff: two of each kind a game typically stacks
            stats.AddModifier(new StatModifier<Stat, float>("sword", Stat.Attack, 5f, StatModifierType.Additive));
            stats.AddModifier(new StatModifier<Stat, float>("ring", Stat.Attack, 2f, StatModifierType.Additive, priority: 1));
            stats.AddModifier(new StatModifier<Stat, float>("rage", Stat.Attack, 0.2f, StatModifierType.Multiplicative));
            stats.AddModifier(new StatModifier<Stat, float>("blessing", Stat.Attack, 0.1f, StatModifierType.Multiplicative, priority: 2));
            stats.AddModifier(new StatModifier<Stat, float>("weakness", Stat.Attack, -0.15f, StatModifierType.Multiplicative, priority: 1));
            stats.AddModifier(new StatModifier<Stat, float>("speed", Stat.Speed, 1f, StatModifierType.Additive));
        }
    }
}
