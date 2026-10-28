using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RPGFramework.Bootstrap;
using RPGFramework.Cards;
using RPGFramework.Cards.Unity;
using RPGFramework.Events;
using RPGFramework.Logging;
using UnityEngine;
using Logger = RPGFramework.Logging.Logger;
using Object = UnityEngine.Object;

namespace RPGFramework.Dice.Tests
{
    /// <summary>
    /// Dice: a seed gives the same rolls, every roll is logged and announced (shuffles only logged), scripted dice
    /// for tests, the core module registers them with the seed of its options. Decks and hands: shuffle by the dice,
    /// draw, take, peek, put on top or under, limits; a deck asset with copies.
    /// </summary>
    public sealed class DiceAndDecksTests
    {
        private sealed class CardAsset : ScriptableObject
        {
        }

        private sealed class TestDeck : DeckAsset<CardAsset>
        {
        }

        [Test]
        public void TheSameSeed_GivesTheSameRolls_AndEveryRollIsLogged()
        {
            var a = new DiceService(42);
            var b = new DiceService(42);
            var first = Enumerable.Range(0, 50).Select(_ => a.Roll(6, "move")).ToList();
            var second = Enumerable.Range(0, 50).Select(_ => b.Roll(6, "move")).ToList();

            Assert.That(first, Is.EqualTo(second));
            Assert.That(first.All(v => v >= 1 && v <= 6), Is.True);
            Assert.That(first.Distinct().Count(), Is.EqualTo(6));
            Assert.That(a.Log, Has.Count.EqualTo(50));
            Assert.That(a.Log[0], Is.EqualTo(new DieRoll(6, first[0], "move")));
            Assert.That(a.NextSeed(), Is.EqualTo(b.NextSeed()), "seeds for other services come from the dice too");

            a.Reseed(7);
            b.Reseed(7);
            Assert.That(a.Roll(20), Is.EqualTo(b.Roll(20)));
            Assert.That(a.Seed, Is.EqualTo(7));
            Assert.That(() => a.Roll(0), Throws.InstanceOf<ArgumentOutOfRangeException>());

            a.LogLimit = 3;
            a.Roll(6);
            Assert.That(a.Log, Has.Count.EqualTo(3), "the oldest go");
            a.ClearLog();
            Assert.That(a.Log, Is.Empty);
        }

        [Test]
        public void Rolls_AreAnnounced_ButShufflesAreNot()
        {
            using (var bus = new EventBus())
            {
                var rolled = new List<DieRolledEvent>();
                bus.Subscribe<DieRolledEvent>(rolled.Add);
                var dice = new DiceService(1, bus);

                dice.Roll(6, "fight");
                new Deck<int>(Enumerable.Range(0, 10)).Shuffle(dice);

                Assert.That(rolled.Select(r => r.Roll.Purpose), Is.EqualTo(new[] { "fight" }));
                Assert.That(dice.Log.Count(r => r.Purpose == DicePurposes.Shuffle), Is.EqualTo(9));
            }
        }

        [Test]
        public void ScriptedDice_RollWhatTheTestSays()
        {
            var dice = new ScriptedDice(6, 1).Then(3);
            Assert.That(dice.Roll(6), Is.EqualTo(6));
            Assert.That(dice.Roll(6), Is.EqualTo(1));
            Assert.That(dice.Remaining, Is.EqualTo(1));
            Assert.That(() => dice.Roll(2), Throws.InvalidOperationException, "3 does not fit a D2");

            var free = new ScriptedDice();
            Assert.That(free.Roll(6), Is.InRange(1, 6), "out of script: rolls from its seed");
            free.StrictlyScripted = true;
            Assert.That(() => free.Roll(6), Throws.InvalidOperationException);
        }

        [Test]
        public void ScriptedDice_ScriptedForAPurpose_GoOnlyToRollsForIt()
        {
            var dice = new ScriptedDice(2, 3).Then("fight", 5, 6);
            Assert.That(dice.Remaining, Is.EqualTo(4));
            Assert.That(dice.Roll(6, "move"), Is.EqualTo(2));
            Assert.That(dice.Roll(6, "fight"), Is.EqualTo(5));
            Assert.That(dice.Roll(6), Is.EqualTo(3));
            Assert.That(dice.RemainingFor("fight"), Is.EqualTo(1));
            Assert.That(dice.Roll(6, "fight"), Is.EqualTo(6));
            dice.StrictlyScripted = true;
            Assert.That(() => dice.Roll(6, "fight"), Throws.InvalidOperationException, "both scripts are used up");
            Assert.That(dice.Log[1].Purpose, Is.EqualTo("fight"));
        }

        [Test]
        public void TheCoreModule_RegistersDice_WithTheSeedOfItsOptions()
        {
            using (var logger = new Logger(LogSeverity.Warning, new MemoryLogSink()))
            {
                int RollWith(int seed)
                {
                    using (var host = new GameHost(logger, new FrameworkCoreOptions { DiceSeed = seed }))
                    {
                        host.Start(b => { });
                        var dice = host.Container.Resolve<IDiceService>();
                        Assert.That(dice.Seed, Is.EqualTo(seed));
                        return dice.Roll(1000);
                    }
                }

                Assert.That(RollWith(5), Is.EqualTo(RollWith(5)));
            }
        }

        [Test]
        public void ADeck_ShufflesByTheDice_AndIsDrawnFromTheTop()
        {
            var deck = new Deck<string>(new[] { "a", "b", "c", "d", "e" });
            var same = new Deck<string>(deck.Cards);
            deck.Shuffle(new DiceService(9));
            same.Shuffle(new DiceService(9));
            Assert.That(deck.Cards, Is.EqualTo(same.Cards), "a seed gives the same order");
            Assert.That(deck.Cards, Is.EquivalentTo(new[] { "a", "b", "c", "d", "e" }));

            var pile = new Deck<string>(new[] { "top", "second", "third", "bottom" });
            Assert.That(pile.Peek(2), Is.EqualTo(new[] { "top", "second" }));
            Assert.That(pile.Peek(9), Has.Count.EqualTo(4));
            Assert.That(pile.TryDraw(out var drawn) && drawn == "top", Is.True);
            Assert.That(pile.TryTake(1, out var taken) && taken == "third", Is.True);
            pile.PutUnder("again");
            pile.PutOnTop("first");
            pile.Insert(1, "between");
            Assert.That(pile.Cards, Is.EqualTo(new[] { "first", "between", "second", "bottom", "again" }));
            Assert.That(pile.Remove("bottom"), Is.True);
            Assert.That(pile.TryTake(9, out _), Is.False);

            var discard = new Deck<string>(new[] { "used" });
            pile.PutUnder(discard);
            Assert.That(pile.Cards.Last(), Is.EqualTo("used"));
            Assert.That(discard.IsEmpty, Is.True);
            pile.Clear();
            Assert.That(pile.TryDraw(out _), Is.False, "an empty deck has nothing to draw");
        }

        [Test]
        public void AHand_KeepsItsLimit()
        {
            var deck = new Deck<int>(new[] { 1, 2, 3 });
            var hand = new Hand<int>(limit: 2);

            Assert.That(hand.TryDrawFrom(deck), Is.True);
            Assert.That(hand.TryAdd(9), Is.True);
            Assert.That(hand.IsFull, Is.True);
            Assert.That(hand.TryDrawFrom(deck), Is.False, "full");
            Assert.That(deck.Count, Is.EqualTo(2), "a full hand leaves the card in the deck");
            Assert.That(hand.Cards, Is.EqualTo(new[] { 1, 9 }));
            Assert.That(hand.TryTake(0, out int played) && played == 1, Is.True);
            Assert.That(hand.Contains(9), Is.True);
            Assert.That(new Hand<int>().IsFull, Is.False, "no limit");
        }

        [Test]
        public void ADeckAsset_BuildsADeckWithEveryCopy()
        {
            var wolf = ScriptableObject.CreateInstance<CardAsset>();
            var bat = ScriptableObject.CreateInstance<CardAsset>();
            var asset = ScriptableObject.CreateInstance<TestDeck>();
            try
            {
                asset.Add(wolf, 3);
                asset.Add(bat);
                asset.Add(bat, 0);

                var deck = asset.Build();
                Assert.That(asset.Count, Is.EqualTo(4));
                Assert.That(deck.Cards.Count(c => c == wolf), Is.EqualTo(3));
                Assert.That(deck.Cards.Count(c => c == bat), Is.EqualTo(1));
                Assert.That(asset.Build(), Is.Not.SameAs(deck), "a new deck each time");
            }
            finally
            {
                Object.DestroyImmediate(wolf);
                Object.DestroyImmediate(bat);
                Object.DestroyImmediate(asset);
            }
        }
    }
}
