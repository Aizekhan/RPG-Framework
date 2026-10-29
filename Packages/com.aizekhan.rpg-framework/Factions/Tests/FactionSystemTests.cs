using NUnit.Framework;
using RPGFramework.Factions;
using RPGFramework.ECS;
using System;

namespace RPGFramework.Factions.Tests
{
    [TestFixture]
    public class FactionSystemTests
    {
        #region Faction Tests

        [Test]
        public void Faction_Create_ValidProperties()
        {
            var faction = new Faction("guild_mages", "Mages Guild", "A guild of magic users", FactionType.Guild);

            Assert.That(faction.FactionId, Is.EqualTo("guild_mages"));
            Assert.That(faction.Name, Is.EqualTo("Mages Guild"));
            Assert.That(faction.FactionType, Is.EqualTo(FactionType.Guild));
            Assert.That(faction.DefaultReputation, Is.EqualTo(0));
            Assert.That(faction.IsActive, Is.True);
        }

        [Test]
        public void Faction_Create_WithDefaultReputation()
        {
            var faction = new Faction("thieves_guild", "Thieves", "Criminal organization",
                FactionType.Criminal, -25);

            Assert.That(faction.DefaultReputation, Is.EqualTo(-25));
        }

        [Test]
        public void Faction_Create_ThrowsOnEmptyId()
        {
            Assert.Throws<ArgumentException>(() =>
                new Faction("", "Name", "Desc", FactionType.Guild));
        }

        #endregion

        #region FactionReputation Tests

        [Test]
        public void FactionReputation_CalculateStanding_Hated()
        {
            var rep = new FactionReputation("test_faction", -80);
            Assert.That(rep.Standing, Is.EqualTo(FactionStanding.Hated));
        }

        [Test]
        public void FactionReputation_CalculateStanding_Neutral()
        {
            var rep = new FactionReputation("test_faction", 0);
            Assert.That(rep.Standing, Is.EqualTo(FactionStanding.Neutral));
        }

        [Test]
        public void FactionReputation_CalculateStanding_Revered()
        {
            var rep = new FactionReputation("test_faction", 90);
            Assert.That(rep.Standing, Is.EqualTo(FactionStanding.Revered));
        }

        [Test]
        public void FactionReputation_ModifyReputation_Clamps()
        {
            var rep = new FactionReputation("test_faction", 90);
            rep.ModifyReputation(20); // Should clamp to 100

            Assert.That(rep.Reputation, Is.EqualTo(100));
        }

        #endregion

        #region FactionRelationship Tests

        [Test]
        public void FactionRelationship_Create_ValidProperties()
        {
            var rel = new FactionRelationship("faction_a", "faction_b",
                FactionRelationType.Allied, 0.5f);

            Assert.That(rel.FactionId1, Is.EqualTo("faction_a"));
            Assert.That(rel.FactionId2, Is.EqualTo("faction_b"));
            Assert.That(rel.RelationType, Is.EqualTo(FactionRelationType.Allied));
            Assert.That(rel.ReputationModifier, Is.EqualTo(0.5f));
        }

        [Test]
        public void FactionRelationship_GetOtherFaction_ReturnsCorrect()
        {
            var rel = new FactionRelationship("faction_a", "faction_b",
                FactionRelationType.Allied);

            Assert.That(rel.GetOtherFaction("faction_a"), Is.EqualTo("faction_b"));
            Assert.That(rel.GetOtherFaction("faction_b"), Is.EqualTo("faction_a"));
            Assert.That(rel.GetOtherFaction("faction_c"), Is.Null);
        }

        [Test]
        public void FactionRelationship_Create_ThrowsOnSameFaction()
        {
            Assert.Throws<ArgumentException>(() =>
                new FactionRelationship("faction_a", "faction_a", FactionRelationType.Allied));
        }

        #endregion

        #region FactionRegistry Tests

        [Test]
        public void FactionRegistry_RegisterFaction_Success()
        {
            var registry = new FactionRegistry();
            var faction = Faction.Create("test_faction", "Test", FactionType.Guild);

            registry.RegisterFaction(faction);

            Assert.That(registry.HasFaction("test_faction"), Is.True);
            Assert.That(registry.GetFaction("test_faction"), Is.EqualTo(faction));
        }

        [Test]
        public void FactionRegistry_RegisterDuplicate_Throws()
        {
            var registry = new FactionRegistry();
            var faction = Faction.Create("test_faction", "Test", FactionType.Guild);

            registry.RegisterFaction(faction);

            Assert.Throws<InvalidOperationException>(() =>
                registry.RegisterFaction(faction));
        }

        [Test]
        public void FactionRegistry_GetFactionsByType_FiltersCorrectly()
        {
            var registry = new FactionRegistry();
            registry.RegisterFaction(Faction.Create("guild1", "Guild 1", FactionType.Guild));
            registry.RegisterFaction(Faction.Create("kingdom1", "Kingdom 1", FactionType.Kingdom));
            registry.RegisterFaction(Faction.Create("guild2", "Guild 2", FactionType.Guild));

            var guilds = registry.GetFactionsByType(FactionType.Guild);

            Assert.That(guilds.Count, Is.EqualTo(2));
        }

        [Test]
        public void FactionRegistry_RegisterRelationship_Success()
        {
            var registry = new FactionRegistry();
            registry.RegisterFaction(Faction.Create("faction_a", "A", FactionType.Guild));
            registry.RegisterFaction(Faction.Create("faction_b", "B", FactionType.Guild));

            var rel = FactionRelationship.Create("faction_a", "faction_b", FactionRelationType.Allied);
            registry.RegisterRelationship(rel);

            var retrieved = registry.GetRelationship("faction_a", "faction_b");
            Assert.That(retrieved, Is.Not.Null);
            Assert.That(retrieved.RelationType, Is.EqualTo(FactionRelationType.Allied));
        }

        #endregion

        #region FactionManager Tests

        [Test]
        public void FactionManager_GetReputation_ReturnsDefault()
        {
            var entityManager = new EntityManager();
            var registry = new FactionRegistry();
            var faction = new Faction("test_faction", "Test", "Desc", FactionType.Guild, 25);
            registry.RegisterFaction(faction);

            var manager = new FactionManager(entityManager, registry);
            int entityId = entityManager.CreateEntity();

            int rep = manager.GetReputation(entityId, "test_faction");

            Assert.That(rep, Is.EqualTo(25));
        }

        [Test]
        public void FactionManager_ModifyReputation_UpdatesValue()
        {
            var entityManager = new EntityManager();
            var registry = new FactionRegistry();
            registry.RegisterFaction(Faction.Create("test_faction", "Test", FactionType.Guild));

            var manager = new FactionManager(entityManager, registry);
            int entityId = entityManager.CreateEntity();

            manager.ModifyReputation(entityId, "test_faction", 50);

            Assert.That(manager.GetReputation(entityId, "test_faction"), Is.EqualTo(50));
        }

        [Test]
        public void FactionManager_ModifyReputation_StartsFromTheDefault_AlsoForRelatedFactions()
        {
            var entityManager = new EntityManager();
            var registry = new FactionRegistry();
            registry.RegisterFaction(new Faction("guards", "Guards", "", FactionType.Military, defaultReputation: 20));
            registry.RegisterFaction(new Faction("bandits", "Bandits", "", FactionType.Criminal, defaultReputation: -30));
            registry.RegisterRelationship(new FactionRelationship("guards", "bandits", FactionRelationType.Enemy, -0.5f));

            var manager = new FactionManager(entityManager, registry);
            int entityId = entityManager.CreateEntity();

            manager.ModifyReputation(entityId, "guards", 10);

            Assert.That(manager.GetReputation(entityId, "guards"), Is.EqualTo(30));
            Assert.That(manager.GetReputation(entityId, "bandits"), Is.EqualTo(-35));
        }

        [Test]
        public void FactionManager_GetStanding_CalculatesCorrectly()
        {
            var entityManager = new EntityManager();
            var registry = new FactionRegistry();
            registry.RegisterFaction(Faction.Create("test_faction", "Test", FactionType.Guild));

            var manager = new FactionManager(entityManager, registry);
            int entityId = entityManager.CreateEntity();

            manager.SetReputation(entityId, "test_faction", 80);

            Assert.That(manager.GetStanding(entityId, "test_faction"),
                Is.EqualTo(FactionStanding.Revered));
        }

        [Test]
        public void FactionManager_RelationshipCascade_AppliesModifier()
        {
            var entityManager = new EntityManager();
            var registry = new FactionRegistry();

            registry.RegisterFaction(Faction.Create("faction_a", "A", FactionType.Guild));
            registry.RegisterFaction(Faction.Create("faction_b", "B", FactionType.Guild));
            registry.RegisterRelationship(
                FactionRelationship.Create("faction_a", "faction_b", FactionRelationType.Allied));

            var manager = new FactionManager(entityManager, registry);
            int entityId = entityManager.CreateEntity();

            manager.ModifyReputation(entityId, "faction_a", 100);

            // Allied gives +50% cascade
            int repB = manager.GetReputation(entityId, "faction_b");
            Assert.That(repB, Is.EqualTo(50));
        }

        [Test]
        public void FactionManager_InitializeEntityReputations_SetsDefaults()
        {
            var entityManager = new EntityManager();
            var registry = new FactionRegistry();
            registry.RegisterFaction(new Faction("faction_a", "A", "Desc", FactionType.Guild, 10));
            registry.RegisterFaction(new Faction("faction_b", "B", "Desc", FactionType.Kingdom, -20));

            var manager = new FactionManager(entityManager, registry);
            int entityId = entityManager.CreateEntity();

            manager.InitializeEntityReputations(entityId);

            Assert.That(manager.GetReputation(entityId, "faction_a"), Is.EqualTo(10));
            Assert.That(manager.GetReputation(entityId, "faction_b"), Is.EqualTo(-20));
        }

        #endregion
    }
}
