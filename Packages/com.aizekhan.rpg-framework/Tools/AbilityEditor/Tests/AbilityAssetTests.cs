using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RPGFramework.Abilities;
using UnityEngine;

namespace RPGFramework.Tools.AbilityEditor.Tests
{
    /// <summary>
    /// An ability asset becomes exactly the ability the game runs: it can be used through the real ability executor,
    /// takes its costs, then waits out its cooldown. Its checks catch what would break a game: a missing id, negative
    /// numbers, a cost that names nothing, a resource charged twice, two abilities sharing an id.
    /// </summary>
    public sealed class AbilityAssetTests
    {
        private readonly List<Object> _made = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in _made)
                Object.DestroyImmediate(asset);
            _made.Clear();
        }

        private T Make<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            _made.Add(asset);
            return asset;
        }

        private AbilityAsset Fireball(string id = "fireball")
        {
            var ability = Make<AbilityAsset>();
            ability.abilityId = id;
            ability.abilityName = "Fireball";
            ability.description = "Burns";
            ability.iconId = "icon_fire";
            ability.abilityType = AbilityType.Active;
            ability.minLevel = 3;
            ability.costs = new List<AbilityCostEntry>
            {
                new AbilityCostEntry { resourceType = "MP", amount = 30f },
                new AbilityCostEntry { resourceType = "HP", amount = 0.1f, isPercentage = true }
            };
            ability.cooldown = 5f;
            ability.targetType = TargetType.Enemy;
            ability.range = 20f;
            ability.aoeRadius = 2f;
            return ability;
        }

        private static AbilityExecutor Executor(IAbilityRegistry registry)
        {
            return new AbilityExecutor(registry, new CooldownTracker(), new TargetValidator());
        }

        private static void AssertOneProblem(AbilityAsset ability, string expected)
        {
            var errors = ability.Validate();
            Assert.That(errors.Count, Is.EqualTo(1), string.Join("; ", errors));
            Assert.That(errors[0], Does.Contain(expected));
        }

        // ---- Conversion and use ----

        [Test]
        public void AGoodAbilityAsset_HasNoProblems()
        {
            Assert.That(Fireball().Validate(), Is.Empty);
            Assert.That(Make<AbilityAsset>().Validate(), Is.Empty, "a new asset starts out valid");
        }

        [Test]
        public void AnAbilityAsset_BecomesTheAbilityTheGameRuns()
        {
            var asset = Fireball();
            asset.abilityType = AbilityType.Channeled;
            asset.castTime = 1.5f;
            asset.channelDuration = 4f;
            asset.isInterruptible = false;
            asset.canMoveWhileCasting = true;
            asset.maxCharges = 2;

            var ability = asset.ToAbility();

            Assert.That(ability.AbilityId, Is.EqualTo("fireball"));
            Assert.That(ability.Name, Is.EqualTo("Fireball"));
            Assert.That(ability.Type, Is.EqualTo(AbilityType.Channeled));
            Assert.That(ability.Cooldown, Is.EqualTo(5f));
            Assert.That(ability.Range, Is.EqualTo(20f));
            Assert.That(ability.TargetType, Is.EqualTo(TargetType.Enemy));
            Assert.That(ability.Metadata.Description, Is.EqualTo("Burns"));
            Assert.That(ability.Metadata.IconId, Is.EqualTo("icon_fire"));
            Assert.That(ability.Metadata.AoeRadius, Is.EqualTo(2f));
            Assert.That(ability.Metadata.MinLevel, Is.EqualTo(3));
            Assert.That(ability.Metadata.MaxCharges, Is.EqualTo(2));
            Assert.That(ability.CastInfo.CastTime, Is.EqualTo(1.5f));
            Assert.That(ability.CastInfo.ChannelDuration, Is.EqualTo(4f));
            Assert.That(ability.CastInfo.IsInterruptible, Is.False);
            Assert.That(ability.CastInfo.CanMoveWhileCasting, Is.True);
            Assert.That(ability.ResourceCosts.Select(c => c.ResourceType), Is.EqualTo(new[] { "MP", "HP" }));
            Assert.That(ability.ResourceCosts[0].Amount, Is.EqualTo(30f));
            Assert.That(ability.ResourceCosts[1].IsPercentage, Is.True);
        }

        [Test]
        public void AConvertedAbility_CanBeUsed_TakesItsCost_ThenWaitsOutItsCooldown()
        {
            var registry = new AbilityRegistry();
            registry.RegisterAbility(Fireball().ToAbility());
            var executor = Executor(registry);
            var takenResources = new List<string>();
            var takenAmounts = new List<float>();
            string landed = null;

            var first = executor.ExecuteAbility("fireball", 1, 2, 0f,
                (entity, resource, amount) => true,
                (entity, resource, amount) =>
                {
                    takenResources.Add(resource);
                    takenAmounts.Add(amount);
                },
                (caster, target, abilityId) => landed = abilityId);

            Assert.That(first.Success, Is.True, first.FailureReason);
            Assert.That(landed, Is.EqualTo("fireball"));
            Assert.That(takenResources, Is.EqualTo(new[] { "MP", "HP" }));
            Assert.That(takenAmounts, Is.EqualTo(new[] { 30f, 0.1f }));

            Assert.That(executor.CanExecuteAbility("fireball", 1, 4.9f), Is.False, "still on its 5 second cooldown");
            var second = executor.ExecuteAbility("fireball", 1, 2, 4.9f, null, null, null);
            Assert.That(second.Success, Is.False);

            Assert.That(executor.CanExecuteAbility("fireball", 1, 5f), Is.True, "cooldown over");
        }

        [Test]
        public void AConvertedAbility_IsRefusedWhenTheCasterCannotPay()
        {
            var registry = new AbilityRegistry();
            registry.RegisterAbility(Fireball().ToAbility());

            var result = Executor(registry).ExecuteAbility("fireball", 1, 2, 0f,
                (entity, resource, amount) => resource != "MP", null, null);

            Assert.That(result.Success, Is.False);
            Assert.That(result.FailureReason, Does.Contain("MP"));
        }

        [Test]
        public void AConvertedAbilityWithCharges_RunsOutOfThem()
        {
            var asset = Fireball();
            asset.cooldown = 0f;
            asset.maxCharges = 2;
            var registry = new AbilityRegistry();
            registry.RegisterAbility(asset.ToAbility());
            var executor = Executor(registry);

            Assert.That(executor.ExecuteAbility("fireball", 1, 2, 0f, null, null, null).Success, Is.True);
            Assert.That(executor.ExecuteAbility("fireball", 1, 2, 1f, null, null, null).Success, Is.True);
            Assert.That(executor.ExecuteAbility("fireball", 1, 2, 2f, null, null, null).Success, Is.False);
        }

        [Test]
        public void EmptyCostSlots_AreLeftOut()
        {
            var asset = Fireball();
            asset.costs.Add(null);

            Assert.That(asset.ToAbility().ResourceCosts.Count, Is.EqualTo(2));
            Assert.That(asset.Validate(), Is.Empty);
        }

        // ---- Checks, one per rule ----

        [Test]
        public void AnEmptyId_IsReported()
        {
            var ability = Fireball(" ");
            AssertOneProblem(ability, "Ability ID");
        }

        [Test]
        public void AnEmptyName_IsReported()
        {
            var ability = Fireball();
            ability.abilityName = "";
            AssertOneProblem(ability, "Ability Name");
        }

        [Test]
        public void ANegativeMinLevel_IsReported()
        {
            var ability = Fireball();
            ability.minLevel = -1;
            AssertOneProblem(ability, "Min Level");
        }

        [Test]
        public void ANegativeCooldown_IsReported()
        {
            var ability = Fireball();
            ability.cooldown = -1f;
            AssertOneProblem(ability, "Cooldown");
        }

        [Test]
        public void ANegativeCastTime_IsReported()
        {
            var ability = Fireball();
            ability.castTime = -1f;
            AssertOneProblem(ability, "Cast Time");
        }

        [Test]
        public void ANegativeChannelDuration_IsReported()
        {
            var ability = Fireball();
            ability.channelDuration = -1f;
            AssertOneProblem(ability, "Channel Duration");
        }

        [Test]
        public void NegativeMaxCharges_IsReported()
        {
            var ability = Fireball();
            ability.maxCharges = -1;
            AssertOneProblem(ability, "Max Charges");
        }

        [Test]
        public void ANegativeRange_IsReported()
        {
            var ability = Fireball();
            ability.range = -1f;
            AssertOneProblem(ability, "Range");
        }

        [Test]
        public void ANegativeAreaRadius_IsReported()
        {
            var ability = Fireball();
            ability.aoeRadius = -1f;
            AssertOneProblem(ability, "Area Radius");
        }

        [Test]
        public void ACostNamingNoResource_IsReported()
        {
            var ability = Fireball();
            ability.costs.Add(new AbilityCostEntry { resourceType = " " });
            AssertOneProblem(ability, "Cost 3 names no resource");
        }

        [Test]
        public void ANegativeCost_IsReported()
        {
            var ability = Fireball();
            ability.costs[0].amount = -5f;
            AssertOneProblem(ability, "Cost 1 (MP) cannot be negative");
        }

        [Test]
        public void AResourceChargedTwice_IsReported()
        {
            var ability = Fireball();
            ability.costs.Add(new AbilityCostEntry { resourceType = "MP", amount = 5f });
            AssertOneProblem(ability, "'MP' is charged more than once");
        }

        // ---- Database ----

        [Test]
        public void AnAbilityDatabase_FillsTheRegistryTheExecutorUses()
        {
            var database = Make<AbilityDatabase>();
            var heal = Fireball("heal");
            heal.castTime = 2f;
            database.abilities = new List<AbilityAsset> { Fireball(), heal };

            Assert.That(database.Validate(), Is.Empty);
            Assert.That(database.GetAbility("heal"), Is.SameAs(heal));
            Assert.That(database.GetAbility("missing"), Is.Null);

            var registry = database.ToAbilityRegistry();
            Assert.That(registry.GetAllAbilities().Count, Is.EqualTo(2));
            Assert.That(registry.GetAbility("heal").CastInfo.CastTime, Is.EqualTo(2f));
            Assert.That(Executor(registry).ExecuteAbility("fireball", 1, 2, 0f, null, null, null).Success, Is.True);
        }

        [Test]
        public void AnEmptyDatabase_IsReported()
        {
            Assert.That(Make<AbilityDatabase>().Validate(), Has.Some.Contains("Database is empty"));
        }

        [Test]
        public void EmptySlotsInTheDatabase_AreReported_AndSkipped()
        {
            var database = Make<AbilityDatabase>();
            database.abilities = new List<AbilityAsset> { Fireball(), null };

            Assert.That(database.Validate(), Has.Some.Contains("1 empty slot"));
            Assert.That(database.ToAbilityRegistry().GetAllAbilities().Count, Is.EqualTo(1));

            database.RemoveEmptySlots();
            Assert.That(database.Validate(), Is.Empty);
        }

        [Test]
        public void DuplicateIdsInTheDatabase_AreReported()
        {
            var database = Make<AbilityDatabase>();
            database.abilities = new List<AbilityAsset> { Fireball(), Fireball() };

            Assert.That(database.Validate(), Has.Some.Contains("Duplicate ability IDs: fireball"));
        }

        [Test]
        public void ABrokenAbilityInTheDatabase_IsReportedByName()
        {
            var database = Make<AbilityDatabase>();
            var broken = Fireball("bolt");
            broken.cooldown = -1f;
            database.abilities = new List<AbilityAsset> { broken };

            var errors = database.Validate();

            Assert.That(errors, Has.Some.Contains("(bolt)"));
            Assert.That(errors, Has.Some.Contains("Cooldown cannot be negative"));
        }
    }
}
