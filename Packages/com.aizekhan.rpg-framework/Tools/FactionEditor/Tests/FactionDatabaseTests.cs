using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RPGFramework.Factions;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RPGFramework.Tools.FactionEditor.Tests
{
    /// <summary>
    /// The faction asset becomes exactly the registry the game runs on (relations answer in either order, so PvP
    /// faction hostility sees them), its checks catch everything that would make that conversion fail or silently
    /// change a value, and the relationship grid keeps one entry per pair.
    /// </summary>
    public sealed class FactionDatabaseTests
    {
        private readonly List<Object> _made = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in _made)
                Object.DestroyImmediate(asset);
            _made.Clear();
        }

        private FactionDatabase Database(params string[] ids)
        {
            var database = ScriptableObject.CreateInstance<FactionDatabase>();
            _made.Add(database);
            foreach (var id in ids)
                database.factions.Add(new FactionEntry { factionId = id, factionName = id });
            return database;
        }

        private static FactionRelationEntry Relation(string a, string b, FactionRelationType type, float modifier = 0f)
        {
            return new FactionRelationEntry { factionA = a, factionB = b, relationType = type, reputationModifier = modifier };
        }

        // ---- Conversion ----

        [Test]
        public void AGoodAsset_HasNoProblems_AndBecomesTheRegistryTheGameRuns()
        {
            var database = Database("red", "blue", "merchants");
            database.factions[0].description = "The red team";
            database.factions[0].factionType = FactionType.Military;
            database.factions[0].defaultReputation = -20;
            database.factions[2].isActive = false;
            database.relationships.Add(Relation("red", "blue", FactionRelationType.Enemy, -0.75f));
            database.relationships.Add(Relation("blue", "merchants", FactionRelationType.TradePartner, 0.25f));

            Assert.That(database.Validate(), Is.Empty);
            var registry = database.ToRegistry();

            Assert.That(registry.GetAllFactions().Select(f => f.FactionId), Is.EquivalentTo(new[] { "red", "blue", "merchants" }));
            var red = registry.GetFaction("red");
            Assert.That(red.Description, Is.EqualTo("The red team"));
            Assert.That(red.FactionType, Is.EqualTo(FactionType.Military));
            Assert.That(red.DefaultReputation, Is.EqualTo(-20));
            Assert.That(registry.GetFaction("merchants").IsActive, Is.False);

            Assert.That(registry.GetRelationship("red", "blue").RelationType, Is.EqualTo(FactionRelationType.Enemy));
            Assert.That(registry.GetRelationship("blue", "red").RelationType, Is.EqualTo(FactionRelationType.Enemy));
            Assert.That(registry.GetRelationship("blue", "red").ReputationModifier, Is.EqualTo(-0.75f));
            Assert.That(registry.GetRelationship("merchants", "blue").RelationType, Is.EqualTo(FactionRelationType.TradePartner));
            Assert.That(registry.GetRelationship("red", "merchants"), Is.Null, "an unlisted pair is neutral");
            Assert.That(registry.GetRelationshipsForFaction("blue").Count, Is.EqualTo(2));
        }

        [Test]
        public void ABrokenAsset_DoesNotBecomeARegistry_AndSaysWhy()
        {
            var database = Database("red", "red");

            var error = Assert.Throws<InvalidOperationException>(() => database.ToRegistry());
            Assert.That(error.Message, Does.Contain("Duplicate faction ID: red"));
        }

        // ---- Checks ----

        [Test]
        public void AnEmptyAsset_IsReported()
        {
            Assert.That(Database().Validate(), Has.Some.Contains("no factions"));
        }

        [Test]
        public void AFactionWithoutAnId_IsReported()
        {
            Assert.That(Database("red", " ").Validate(), Has.Some.Contains("Faction 2 has no ID"));
        }

        [Test]
        public void ADuplicateFactionId_IsReported()
        {
            Assert.That(Database("red", "blue", "red").Validate(), Has.Some.Contains("Duplicate faction ID: red"));
        }

        [Test]
        public void AFactionWithoutAName_IsReported()
        {
            var database = Database("red");
            database.factions[0].factionName = "";

            Assert.That(database.Validate(), Has.Some.Contains("'red' has no name"));
        }

        [Test]
        public void AnEmptyFactionSlot_IsReported()
        {
            var database = Database("red");
            database.factions.Add(null);

            Assert.That(database.Validate(), Has.Some.Contains("Faction 2 is empty"));
        }

        [Test]
        public void AStartingReputationTheGameWouldChange_IsReported()
        {
            var database = Database("red");
            database.factions[0].defaultReputation = 150;

            Assert.That(database.Validate(), Has.Some.Contains("starting reputation 150"));
        }

        [Test]
        public void ARelationshipNamingAMissingFaction_IsReported()
        {
            var database = Database("red");
            database.relationships.Add(Relation("red", "ghosts", FactionRelationType.Enemy));

            Assert.That(database.Validate(), Has.Some.Contains("'ghosts', which is not in the faction list"));
        }

        [Test]
        public void ARelationshipWithAnEmptyId_IsReported()
        {
            var database = Database("red");
            database.relationships.Add(Relation("red", "", FactionRelationType.Enemy));

            Assert.That(database.Validate(), Has.Some.Contains("needs two faction IDs"));
        }

        [Test]
        public void AFactionRelatedToItself_IsReported()
        {
            var database = Database("red");
            database.relationships.Add(Relation("red", "red", FactionRelationType.Allied));

            Assert.That(database.Validate(), Has.Some.Contains("relates a faction to itself"));
        }

        [Test]
        public void AnEmptyRelationshipSlot_IsReported()
        {
            var database = Database("red", "blue");
            database.relationships.Add(null);

            Assert.That(database.Validate(), Has.Some.Contains("Relationship 1 is empty"));
        }

        [Test]
        public void AModifierTheGameWouldChange_IsReported()
        {
            var database = Database("red", "blue");
            database.relationships.Add(Relation("red", "blue", FactionRelationType.Allied, 2f));

            Assert.That(database.Validate(), Has.Some.Contains("reputation modifier 2"));
        }

        [Test]
        public void ThePairListedTwiceInEitherOrder_IsReported()
        {
            var database = Database("red", "blue");
            database.relationships.Add(Relation("red", "blue", FactionRelationType.Allied));
            database.relationships.Add(Relation("blue", "red", FactionRelationType.Enemy));

            Assert.That(database.Validate(), Has.Some.Contains("different relations (Allied and Enemy)"));

            database.relationships[1].relationType = FactionRelationType.Allied;
            Assert.That(database.Validate(), Has.Some.Contains("listed more than once"));
        }

        // ---- Relationship grid ----

        [Test]
        public void SettingACell_AddsThePair_WithTheUsualModifier_AndReadsInEitherOrder()
        {
            var database = Database("red", "blue");

            database.SetRelation("red", "blue", FactionRelationType.Enemy);

            Assert.That(database.relationships.Count, Is.EqualTo(1));
            Assert.That(database.relationships[0].reputationModifier, Is.EqualTo(-0.75f));
            Assert.That(database.GetRelation("red", "blue"), Is.EqualTo(FactionRelationType.Enemy));
            Assert.That(database.GetRelation("blue", "red"), Is.EqualTo(FactionRelationType.Enemy));
            Assert.That(database.Validate(), Is.Empty);
        }

        [Test]
        public void ChangingACellFromTheOtherSide_KeepsOneEntryPerPair()
        {
            var database = Database("red", "blue");
            database.SetRelation("red", "blue", FactionRelationType.Enemy);

            database.SetRelation("blue", "red", FactionRelationType.Allied);

            Assert.That(database.relationships.Count, Is.EqualTo(1));
            Assert.That(database.GetRelation("red", "blue"), Is.EqualTo(FactionRelationType.Allied));
            Assert.That(database.relationships[0].reputationModifier, Is.EqualTo(0.5f));
        }

        [Test]
        public void SettingACellToTheSameRelation_KeepsAFineTunedModifier()
        {
            var database = Database("red", "blue");
            database.relationships.Add(Relation("red", "blue", FactionRelationType.Enemy, -0.3f));

            database.SetRelation("blue", "red", FactionRelationType.Enemy);

            Assert.That(database.relationships.Single().reputationModifier, Is.EqualTo(-0.3f));
        }

        [Test]
        public void SettingACellToNeutral_ClearsThePair()
        {
            var database = Database("red", "blue");
            database.SetRelation("red", "blue", FactionRelationType.Rival);

            database.SetRelation("blue", "red", FactionRelationType.Neutral);

            Assert.That(database.relationships, Is.Empty);
            Assert.That(database.GetRelation("red", "blue"), Is.EqualTo(FactionRelationType.Neutral));
        }

        [Test]
        public void SettingACell_RemovesExtraCopiesOfThePair()
        {
            var database = Database("red", "blue");
            database.relationships.Add(Relation("red", "blue", FactionRelationType.Allied));
            database.relationships.Add(Relation("blue", "red", FactionRelationType.Enemy));
            Assert.That(database.GetRelation("red", "blue"), Is.EqualTo(FactionRelationType.Enemy), "the last one counts, as in the game");

            database.SetRelation("red", "blue", FactionRelationType.Rival);

            Assert.That(database.relationships.Single().relationType, Is.EqualTo(FactionRelationType.Rival));
            Assert.That(database.Validate(), Is.Empty);
        }

        [Test]
        public void TheDiagonal_CannotBeSet()
        {
            var database = Database("red");

            database.SetRelation("red", "red", FactionRelationType.Enemy);

            Assert.That(database.relationships, Is.Empty);
        }

        [Test]
        public void TheGrid_ListsEachNamedFactionOnce()
        {
            var database = Database("red", "blue", "red", " ");
            database.factions.Add(null);

            Assert.That(database.GetFactionIds(), Is.EqualTo(new[] { "red", "blue" }));
        }
    }
}
