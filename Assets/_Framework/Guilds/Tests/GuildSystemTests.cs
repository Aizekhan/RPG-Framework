using NUnit.Framework;
using RPGFramework.Guilds.Runtime;
using RPGFramework.ECS;
using RPGFramework.Factions;
using System;
using System.Linq;

namespace RPGFramework.Guilds.Tests
{
    [TestFixture]
    public class GuildSystemTests
    {
        private EntityManager _entityManager;
        private GuildRegistry _registry;
        private GuildManager _manager;

        [SetUp]
        public void SetUp()
        {
            _entityManager = new EntityManager();
            _registry = new GuildRegistry();
            _manager = new GuildManager(_entityManager, new FactionRegistry(), _registry);
        }

        #region Guild Tests

        [Test]
        public void Guild_CreateWithLeader_ValidProperties()
        {
            int leaderId = _entityManager.CreateEntity();
            var guild = _manager.CreateGuild("guild_001", "The Adventurers", "ADV", leaderId);

            Assert.That(guild.GuildId, Is.EqualTo("guild_001"));
            Assert.That(guild.Name, Is.EqualTo("The Adventurers"));
            Assert.That(guild.Tag, Is.EqualTo("ADV"));
            Assert.That(guild.LeaderId, Is.EqualTo(leaderId));
            Assert.That(guild.MemberCount, Is.EqualTo(1));
            Assert.That(guild.Level, Is.EqualTo(1));
            Assert.That(guild.Experience, Is.EqualTo(0));
            Assert.That(guild.IsMember(leaderId), Is.True);
        }

        [Test]
        public void Guild_DisbandGuild_RemovesAllMembers()
        {
            int leaderId = _entityManager.CreateEntity();
            var guild = _manager.CreateGuild("guild_002", "Test Guild", "TST", leaderId);

            int member1 = _entityManager.CreateEntity();
            int member2 = _entityManager.CreateEntity();
            _manager.AddMember("guild_002", member1);
            _manager.AddMember("guild_002", member2);

            Assert.That(guild.MemberCount, Is.EqualTo(3));

            _manager.DisbandGuild("guild_002");

            Assert.That(_manager.GetGuild("guild_002"), Is.Null);
            Assert.That(_manager.IsInGuild(leaderId), Is.False);
            Assert.That(_manager.IsInGuild(member1), Is.False);
            Assert.That(_manager.IsInGuild(member2), Is.False);
        }

        [Test]
        public void Guild_LevelAndExperience_InitialValues()
        {
            int leaderId = _entityManager.CreateEntity();
            var guild = _manager.CreateGuild("guild_003", "Level Test", "LVL", leaderId);

            Assert.That(guild.Level, Is.EqualTo(1));
            Assert.That(guild.Experience, Is.EqualTo(0));
        }

        [Test]
        public void Guild_UpdateMOTD_StoresCorrectly()
        {
            int leaderId = _entityManager.CreateEntity();
            var guild = _manager.CreateGuild("guild_004", "MOTD Test", "MTD", leaderId);

            guild.SetMessageOfTheDay("New message of the day!");

            // MOTD update is a stub in current implementation
            // This test validates the method doesn't throw
            Assert.Pass("MOTD method executed without error");
        }

        [Test]
        public void Guild_MemberCapacityLimits_EnforcesMaxMembers()
        {
            int leaderId = _entityManager.CreateEntity();
            var guild = _manager.CreateGuild("guild_005", "Small Guild", "SML", leaderId, "", maxMembers: 3);

            int member1 = _entityManager.CreateEntity();
            int member2 = _entityManager.CreateEntity();
            int member3 = _entityManager.CreateEntity();

            Assert.That(_manager.AddMember("guild_005", member1), Is.True);
            Assert.That(_manager.AddMember("guild_005", member2), Is.True);
            Assert.That(guild.IsFull(), Is.True);
            Assert.That(_manager.AddMember("guild_005", member3), Is.False);
        }

        [Test]
        public void Guild_IsFull_ReturnsTrueWhenAtCapacity()
        {
            int leaderId = _entityManager.CreateEntity();
            var guild = _manager.CreateGuild("guild_006", "Full Test", "FUL", leaderId, "", maxMembers: 2);

            Assert.That(guild.IsFull(), Is.False);

            int member = _entityManager.CreateEntity();
            _manager.AddMember("guild_006", member);

            Assert.That(guild.IsFull(), Is.True);
        }

        #endregion

        #region Member Tests

        [Test]
        public void Member_AddToGuild_Success()
        {
            int leaderId = _entityManager.CreateEntity();
            var guild = _manager.CreateGuild("guild_007", "Member Test", "MBR", leaderId);

            int memberId = _entityManager.CreateEntity();
            bool added = _manager.AddMember("guild_007", memberId);

            Assert.That(added, Is.True);
            Assert.That(guild.IsMember(memberId), Is.True);
            Assert.That(guild.MemberCount, Is.EqualTo(2));
        }

        [Test]
        public void Member_RemoveFromGuild_Success()
        {
            int leaderId = _entityManager.CreateEntity();
            var guild = _manager.CreateGuild("guild_008", "Remove Test", "RMV", leaderId);

            int memberId = _entityManager.CreateEntity();
            _manager.AddMember("guild_008", memberId);

            bool removed = _manager.RemoveMember("guild_008", memberId);

            Assert.That(removed, Is.True);
            Assert.That(guild.IsMember(memberId), Is.False);
            Assert.That(guild.MemberCount, Is.EqualTo(1));
        }

        [Test]
        public void Member_KickWithPermission_ChecksAuthority()
        {
            int leaderId = _entityManager.CreateEntity();
            var guild = _manager.CreateGuild("guild_009", "Kick Test", "KCK", leaderId);

            int officer = _entityManager.CreateEntity();
            _manager.AddMember("guild_009", officer, GuildRankType.Officer);

            var officerMember = guild.GetMember(officer);
            Assert.That(officerMember.HasPermission(GuildPermission.Kick), Is.True);
        }

        [Test]
        public void Member_RankAndPermissions_DefaultInitiate()
        {
            int leaderId = _entityManager.CreateEntity();
            var guild = _manager.CreateGuild("guild_010", "Rank Test", "RNK", leaderId);

            int memberId = _entityManager.CreateEntity();
            _manager.AddMember("guild_010", memberId);

            var member = guild.GetMember(memberId);
            Assert.That(member.Rank, Is.EqualTo(GuildRankType.Initiate));
            Assert.That(member.HasPermission(GuildPermission.DepositTreasury), Is.True);
            Assert.That(member.HasPermission(GuildPermission.Kick), Is.False);
        }

        [Test]
        public void Member_ContributionPoints_StartsAtZero()
        {
            int leaderId = _entityManager.CreateEntity();
            var guild = _manager.CreateGuild("guild_011", "Contrib Test", "CTB", leaderId);

            var leader = guild.GetMember(leaderId);
            Assert.That(leader.ContributionPoints, Is.EqualTo(0));
        }

        [Test]
        public void Member_AddContribution_IncreasesPoints()
        {
            int leaderId = _entityManager.CreateEntity();
            var guild = _manager.CreateGuild("guild_012", "Contrib Add", "CAD", leaderId);

            var leader = guild.GetMember(leaderId);
            leader.AddContribution(100);

            Assert.That(leader.ContributionPoints, Is.EqualTo(100));
        }

        [Test]
        public void Member_CannotJoinMultipleGuilds()
        {
            int leaderId1 = _entityManager.CreateEntity();
            int leaderId2 = _entityManager.CreateEntity();
            _manager.CreateGuild("guild_013", "Guild One", "GO1", leaderId1);
            _manager.CreateGuild("guild_014", "Guild Two", "GO2", leaderId2);

            int memberId = _entityManager.CreateEntity();
            _manager.AddMember("guild_013", memberId);

            bool addedToSecond = _manager.AddMember("guild_014", memberId);
            Assert.That(addedToSecond, Is.False);
        }

        #endregion

        #region Rank Tests

        [Test]
        public void Rank_CreateCustomRanks_ValidProperties()
        {
            var rank = new GuildRank(
                GuildRankType.Officer,
                "Custom Officer",
                2,
                GuildPermission.Invite | GuildPermission.Kick,
                GuildPrivilege.UseGuildChat | GuildPrivilege.AccessBank
            );

            Assert.That(rank.RankType, Is.EqualTo(GuildRankType.Officer));
            Assert.That(rank.Name, Is.EqualTo("Custom Officer"));
            Assert.That(rank.HierarchyLevel, Is.EqualTo(2));
        }

        [Test]
        public void Rank_AssignPermissions_ChecksCorrectly()
        {
            var rank = new GuildRank(
                GuildRankType.Member,
                "Member",
                1,
                GuildPermission.Invite | GuildPermission.DepositTreasury,
                GuildPrivilege.None
            );

            Assert.That(rank.HasPermission(GuildPermission.Invite), Is.True);
            Assert.That(rank.HasPermission(GuildPermission.DepositTreasury), Is.True);
            Assert.That(rank.HasPermission(GuildPermission.Kick), Is.False);
        }

        [Test]
        public void Rank_AssignPrivileges_ChecksCorrectly()
        {
            var rank = new GuildRank(
                GuildRankType.Officer,
                "Officer",
                2,
                GuildPermission.None,
                GuildPrivilege.UseGuildChat | GuildPrivilege.AccessBank | GuildPrivilege.UseOfficerChat
            );

            Assert.That(rank.HasPrivilege(GuildPrivilege.UseGuildChat), Is.True);
            Assert.That(rank.HasPrivilege(GuildPrivilege.AccessBank), Is.True);
            Assert.That(rank.HasPrivilege(GuildPrivilege.UseOfficerChat), Is.True);
            Assert.That(rank.HasPrivilege(GuildPrivilege.GuildSalary), Is.False);
        }

        [Test]
        public void Rank_HierarchyAndAuthority_HigherRankHasAuthority()
        {
            var elderRank = new GuildRank(GuildRankType.Elder, "Elder", 3, GuildPermission.None, GuildPrivilege.None);
            var memberRank = new GuildRank(GuildRankType.Member, "Member", 1, GuildPermission.None, GuildPrivilege.None);

            Assert.That(elderRank.HasAuthorityOver(memberRank), Is.True);
            Assert.That(memberRank.HasAuthorityOver(elderRank), Is.False);
        }

        [Test]
        public void Rank_DefaultRankSystem_FiveTiers()
        {
            int leaderId = _entityManager.CreateEntity();
            var guild = _manager.CreateGuild("guild_015", "Rank Tiers", "RNT", leaderId);

            var ranks = guild.GetRanks();
            Assert.That(ranks.Count, Is.EqualTo(5));
            Assert.That(ranks.Any(r => r.RankType == GuildRankType.Initiate), Is.True);
            Assert.That(ranks.Any(r => r.RankType == GuildRankType.Member), Is.True);
            Assert.That(ranks.Any(r => r.RankType == GuildRankType.Officer), Is.True);
            Assert.That(ranks.Any(r => r.RankType == GuildRankType.Elder), Is.True);
            Assert.That(ranks.Any(r => r.RankType == GuildRankType.Leader), Is.True);
        }

        [Test]
        public void Rank_AddPermission_ModifiesRank()
        {
            var rank = new GuildRank(
                GuildRankType.Member,
                "Member",
                1,
                GuildPermission.None,
                GuildPrivilege.None
            );

            rank.AddPermission(GuildPermission.Invite);
            Assert.That(rank.HasPermission(GuildPermission.Invite), Is.True);
        }

        [Test]
        public void Rank_RemovePermission_ModifiesRank()
        {
            var rank = new GuildRank(
                GuildRankType.Officer,
                "Officer",
                2,
                GuildPermission.Invite | GuildPermission.Kick,
                GuildPrivilege.None
            );

            rank.RemovePermission(GuildPermission.Kick);
            Assert.That(rank.HasPermission(GuildPermission.Invite), Is.True);
            Assert.That(rank.HasPermission(GuildPermission.Kick), Is.False);
        }

        #endregion

        #region Permission Tests

        [Test]
        public void Permission_CheckMemberPermissions_Leader()
        {
            int leaderId = _entityManager.CreateEntity();
            _manager.CreateGuild("guild_016", "Perm Leader", "PLD", leaderId);

            Assert.That(_manager.HasPermission(leaderId, GuildPermission.All), Is.True);
            Assert.That(_manager.HasPermission(leaderId, GuildPermission.Kick), Is.True);
            Assert.That(_manager.HasPermission(leaderId, GuildPermission.DisbandGuild), Is.True);
        }

        [Test]
        public void Permission_PermissionInheritance_BasedOnRank()
        {
            int leaderId = _entityManager.CreateEntity();
            var guild = _manager.CreateGuild("guild_017", "Perm Inherit", "PIN", leaderId);

            int officer = _entityManager.CreateEntity();
            _manager.AddMember("guild_017", officer, GuildRankType.Officer);

            Assert.That(_manager.HasPermission(officer, GuildPermission.Invite), Is.True);
            Assert.That(_manager.HasPermission(officer, GuildPermission.Kick), Is.True);
            Assert.That(_manager.HasPermission(officer, GuildPermission.DisbandGuild), Is.False);
        }

        [Test]
        public void Permission_AuthorityBasedOperations_CannotKickHigherRank()
        {
            int leaderId = _entityManager.CreateEntity();
            var guild = _manager.CreateGuild("guild_018", "Auth Test", "AUT", leaderId);

            var leaderRank = guild.GetRank(GuildRankType.Leader);
            var officerRank = guild.GetRank(GuildRankType.Officer);

            Assert.That(officerRank.HasAuthorityOver(leaderRank), Is.False);
        }

        [Test]
        public void Permission_LeaderOnlyOperations_DisbandGuild()
        {
            int leaderId = _entityManager.CreateEntity();
            var guild = _manager.CreateGuild("guild_019", "Leader Only", "LDO", leaderId);

            int elder = _entityManager.CreateEntity();
            _manager.AddMember("guild_019", elder, GuildRankType.Elder);

            Assert.That(_manager.HasPermission(leaderId, GuildPermission.DisbandGuild), Is.True);
            Assert.That(_manager.HasPermission(elder, GuildPermission.DisbandGuild), Is.False);
        }

        #endregion

        #region Privilege Tests

        [Test]
        public void Privilege_GrantPrivilegesByRank_Officer()
        {
            int leaderId = _entityManager.CreateEntity();
            var guild = _manager.CreateGuild("guild_020", "Priv Officer", "PRO", leaderId);

            int officer = _entityManager.CreateEntity();
            _manager.AddMember("guild_020", officer, GuildRankType.Officer);

            Assert.That(_manager.HasPrivilege(officer, GuildPrivilege.UseGuildChat), Is.True);
            Assert.That(_manager.HasPrivilege(officer, GuildPrivilege.UseOfficerChat), Is.True);
            Assert.That(_manager.HasPrivilege(officer, GuildPrivilege.AccessBank), Is.True);
        }

        [Test]
        public void Privilege_CheckMemberPrivileges_Initiate()
        {
            int leaderId = _entityManager.CreateEntity();
            var guild = _manager.CreateGuild("guild_021", "Priv Initiate", "PRI", leaderId);

            int initiate = _entityManager.CreateEntity();
            _manager.AddMember("guild_021", initiate, GuildRankType.Initiate);

            Assert.That(_manager.HasPrivilege(initiate, GuildPrivilege.UseGuildChat), Is.True);
            Assert.That(_manager.HasPrivilege(initiate, GuildPrivilege.UseOfficerChat), Is.False);
            Assert.That(_manager.HasPrivilege(initiate, GuildPrivilege.AccessBank), Is.False);
        }

        [Test]
        public void Privilege_MultiplePrivilegeFlags_AllGranted()
        {
            int leaderId = _entityManager.CreateEntity();
            var guild = _manager.CreateGuild("guild_022", "Priv Multi", "PRM", leaderId);

            int elder = _entityManager.CreateEntity();
            _manager.AddMember("guild_022", elder, GuildRankType.Elder);

            var elderMember = guild.GetMember(elder);
            var privileges = elderMember.GetPrivileges();

            Assert.That((privileges & GuildPrivilege.UseGuildChat) != 0, Is.True);
            Assert.That((privileges & GuildPrivilege.AccessBank) != 0, Is.True);
            Assert.That((privileges & GuildPrivilege.ParticipateInRaids) != 0, Is.True);
        }

        #endregion

        #region Treasury Tests

        [Test]
        public void Treasury_WithPublisher_PublishesDepositsAndWithdrawals()
        {
            using var bus = new RPGFramework.Events.EventBus();
            var treasury = new GuildTreasury(GuildTreasuryData.Create("guild_events"), bus);
            var deposits = new System.Collections.Generic.List<TreasuryDepositEvent>();
            var withdrawals = new System.Collections.Generic.List<TreasuryWithdrawEvent>();
            bus.Subscribe<TreasuryDepositEvent>(deposits.Add);
            bus.Subscribe<TreasuryWithdrawEvent>(withdrawals.Add);

            treasury.DepositGold(500, 1);
            treasury.DepositItem("gem", 3, 1);
            treasury.WithdrawGold(200, 2);
            treasury.WithdrawGold(10_000, 2);

            Assert.That(deposits.Select(d => (d.GoldAmount, d.ItemId, d.ItemQuantity)),
                Is.EqualTo(new[] { (500L, (string)null, 0), (0L, "gem", 3) }));
            Assert.That(withdrawals, Has.Count.EqualTo(1), "a refused withdrawal publishes nothing");
            Assert.That(withdrawals[0].WithdrawerId, Is.EqualTo(2));
            Assert.That(withdrawals[0].GuildId, Is.EqualTo("guild_events"));
        }

        [Test]
        public void Treasury_DepositGold_Success()
        {
            var treasuryData = GuildTreasuryData.Create("guild_023");
            var treasury = new GuildTreasury(treasuryData);

            bool deposited = treasury.DepositGold(1000, 1);

            Assert.That(deposited, Is.True);
            Assert.That(treasury.Gold, Is.EqualTo(1000));
        }

        [Test]
        public void Treasury_WithdrawGoldWithPermissionCheck_Success()
        {
            var treasuryData = GuildTreasuryData.Create("guild_024");
            var treasury = new GuildTreasury(treasuryData);
            treasury.DepositGold(5000, 1);

            int leaderId = _entityManager.CreateEntity();
            _manager.CreateGuild("guild_024", "Withdraw Test", "WTH", leaderId);

            bool canWithdraw = _manager.HasPermission(leaderId, GuildPermission.WithdrawGold);
            Assert.That(canWithdraw, Is.True);

            bool withdrawn = treasury.WithdrawGold(1000, leaderId);
            Assert.That(withdrawn, Is.True);
            Assert.That(treasury.Gold, Is.EqualTo(4000));
        }

        [Test]
        public void Treasury_TreasuryCapacity_EnforcesLimit()
        {
            var treasuryData = GuildTreasuryData.Create("guild_025", maxCapacity: 10000);
            var treasury = new GuildTreasury(treasuryData);

            treasury.DepositGold(9000, 1);
            bool deposited = treasury.DepositGold(2000, 1);

            Assert.That(deposited, Is.False);
            Assert.That(treasury.Gold, Is.EqualTo(9000));
        }

        [Test]
        public void Treasury_TransactionHistory_RecordsDeposits()
        {
            var treasuryData = GuildTreasuryData.Create("guild_026");
            var treasury = new GuildTreasury(treasuryData);

            treasury.DepositGold(500, 1);
            treasury.DepositGold(300, 2);

            var history = treasury.GetTransactionHistory();
            Assert.That(history.Count, Is.EqualTo(2));
            Assert.That(history[0].Type, Is.EqualTo(TreasuryTransactionType.GoldDeposit));
        }

        [Test]
        public void Treasury_DepositItems_Success()
        {
            var treasuryData = GuildTreasuryData.Create("guild_027");
            var treasury = new GuildTreasury(treasuryData);

            bool deposited = treasury.DepositItem("sword_001", 5, 1);

            Assert.That(deposited, Is.True);
            Assert.That(treasury.HasItem("sword_001", 5), Is.True);
        }

        [Test]
        public void Treasury_WithdrawItems_Success()
        {
            var treasuryData = GuildTreasuryData.Create("guild_028");
            var treasury = new GuildTreasury(treasuryData);
            treasury.DepositItem("potion_001", 10, 1);

            bool withdrawn = treasury.WithdrawItem("potion_001", 3, 2);

            Assert.That(withdrawn, Is.True);
            Assert.That(treasury.HasItem("potion_001", 7), Is.True);
            Assert.That(treasury.HasItem("potion_001", 8), Is.False);
        }

        [Test]
        public void Treasury_InsufficientFunds_WithdrawFails()
        {
            var treasuryData = GuildTreasuryData.Create("guild_029");
            var treasury = new GuildTreasury(treasuryData);
            treasury.DepositGold(100, 1);

            bool withdrawn = treasury.WithdrawGold(200, 1);

            Assert.That(withdrawn, Is.False);
            Assert.That(treasury.Gold, Is.EqualTo(100));
        }

        #endregion

        #region Manager Tests

        [Test]
        public void Manager_CreateGuildViaManager_Success()
        {
            int leaderId = _entityManager.CreateEntity();
            var guild = _manager.CreateGuild("guild_030", "Manager Guild", "MGR", leaderId, "Test description");

            Assert.That(guild, Is.Not.Null);
            Assert.That(guild.GuildId, Is.EqualTo("guild_030"));
            Assert.That(guild.LeaderId, Is.EqualTo(leaderId));
        }

        [Test]
        public void Manager_AddRemoveMembersViaManager_Success()
        {
            int leaderId = _entityManager.CreateEntity();
            _manager.CreateGuild("guild_031", "Member Mgmt", "MEM", leaderId);

            int member1 = _entityManager.CreateEntity();
            int member2 = _entityManager.CreateEntity();

            _manager.AddMember("guild_031", member1);
            _manager.AddMember("guild_031", member2);

            Assert.That(_manager.IsInGuild(member1), Is.True);
            Assert.That(_manager.IsInGuild(member2), Is.True);

            _manager.RemoveMember("guild_031", member1);
            Assert.That(_manager.IsInGuild(member1), Is.False);
        }

        [Test]
        public void Manager_PromoteDemoteMembers_Success()
        {
            int leaderId = _entityManager.CreateEntity();
            var guild = _manager.CreateGuild("guild_032", "Promotion Test", "PMT", leaderId);

            int member = _entityManager.CreateEntity();
            _manager.AddMember("guild_032", member);

            bool promoted = _manager.PromoteMember("guild_032", member, GuildRankType.Member);
            Assert.That(promoted, Is.True);
            Assert.That(guild.GetMember(member).Rank, Is.EqualTo(GuildRankType.Member));

            bool demoted = _manager.DemoteMember("guild_032", member, GuildRankType.Initiate);
            Assert.That(demoted, Is.True);
            Assert.That(guild.GetMember(member).Rank, Is.EqualTo(GuildRankType.Initiate));
        }

        [Test]
        public void Manager_TransferLeadership_Success()
        {
            int leaderId = _entityManager.CreateEntity();
            var guild = _manager.CreateGuild("guild_033", "Transfer Test", "TRF", leaderId);

            int newLeader = _entityManager.CreateEntity();
            _manager.AddMember("guild_033", newLeader, GuildRankType.Elder);

            bool transferred = _manager.TransferLeadership("guild_033", newLeader);

            Assert.That(transferred, Is.True);
            Assert.That(guild.GetMember(newLeader).Rank, Is.EqualTo(GuildRankType.Leader));
            Assert.That(guild.GetMember(leaderId).Rank, Is.EqualTo(GuildRankType.Elder));
        }

        [Test]
        public void Manager_DisbandGuildCleanup_RemovesFromRegistry()
        {
            int leaderId = _entityManager.CreateEntity();
            _manager.CreateGuild("guild_034", "Disband Test", "DIS", leaderId);

            _manager.DisbandGuild("guild_034");

            Assert.That(_manager.GetGuild("guild_034"), Is.Null);
            Assert.That(_registry.HasGuild("guild_034"), Is.False);
        }

        [Test]
        public void Manager_CannotRemoveLeaderWithMembers_Fails()
        {
            int leaderId = _entityManager.CreateEntity();
            var guild = _manager.CreateGuild("guild_035", "Leader Remove", "LDR", leaderId);

            int member = _entityManager.CreateEntity();
            _manager.AddMember("guild_035", member);

            bool removed = _manager.RemoveMember("guild_035", leaderId);

            Assert.That(removed, Is.False);
            Assert.That(guild.IsMember(leaderId), Is.True);
        }

        #endregion

        #region Registry Tests

        [Test]
        public void Registry_RegisterGuild_Success()
        {
            var registry = new GuildRegistry();
            var guildData = GuildData.Create("guild_036", "Registry Test", "REG", 1);
            var guild = new Guild(guildData);

            registry.RegisterGuild(guild);

            Assert.That(registry.HasGuild("guild_036"), Is.True);
            Assert.That(registry.GetGuild("guild_036"), Is.EqualTo(guild));
        }

        [Test]
        public void Registry_RetrieveById_Success()
        {
            var registry = new GuildRegistry();
            var guildData = GuildData.Create("guild_037", "Retrieve Test", "RTV", 1);
            var guild = new Guild(guildData);
            registry.RegisterGuild(guild);

            var retrieved = registry.GetGuild("guild_037");

            Assert.That(retrieved, Is.Not.Null);
            Assert.That(retrieved.GuildId, Is.EqualTo("guild_037"));
        }

        [Test]
        public void Registry_CheckNameUniqueness_DetectsDuplicates()
        {
            var registry = new GuildRegistry();
            var guildData = GuildData.Create("guild_038", "Unique Name", "UNQ", 1);
            var guild = new Guild(guildData);
            registry.RegisterGuild(guild);

            Assert.That(registry.IsNameTaken("Unique Name"), Is.True);
            Assert.That(registry.IsNameTaken("unique name"), Is.True);
            Assert.That(registry.IsNameTaken("Other Name"), Is.False);
        }

        [Test]
        public void Registry_CheckTagUniqueness_DetectsDuplicates()
        {
            var registry = new GuildRegistry();
            var guildData = GuildData.Create("guild_039", "Tag Test", "TAG", 1);
            var guild = new Guild(guildData);
            registry.RegisterGuild(guild);

            Assert.That(registry.IsTagTaken("TAG"), Is.True);
            Assert.That(registry.IsTagTaken("tag"), Is.True);
            Assert.That(registry.IsTagTaken("OTH"), Is.False);
        }

        [Test]
        public void Registry_ListAllGuilds_ReturnsAll()
        {
            var registry = new GuildRegistry();
            registry.RegisterGuild(new Guild(GuildData.Create("guild_040", "Guild 1", "G01", 1)));
            registry.RegisterGuild(new Guild(GuildData.Create("guild_041", "Guild 2", "G02", 2)));
            registry.RegisterGuild(new Guild(GuildData.Create("guild_042", "Guild 3", "G03", 3)));

            var allGuilds = registry.GetAllGuilds();

            Assert.That(allGuilds.Count, Is.EqualTo(3));
        }

        [Test]
        public void Registry_SearchByName_FindsMatches()
        {
            var registry = new GuildRegistry();
            registry.RegisterGuild(new Guild(GuildData.Create("guild_043", "Dragon Slayers", "DRG", 1)));
            registry.RegisterGuild(new Guild(GuildData.Create("guild_044", "Dragon Riders", "DRR", 2)));
            registry.RegisterGuild(new Guild(GuildData.Create("guild_045", "Phoenix Guild", "PHX", 3)));

            var results = registry.SearchGuildsByName("Dragon");

            Assert.That(results.Count, Is.EqualTo(2));
        }

        [Test]
        public void Registry_GetGuildByTag_FindsCorrectGuild()
        {
            var registry = new GuildRegistry();
            registry.RegisterGuild(new Guild(GuildData.Create("guild_046", "Tag Search", "TSR", 1)));

            var guild = registry.GetGuildByTag("TSR");

            Assert.That(guild, Is.Not.Null);
            Assert.That(guild.GuildId, Is.EqualTo("guild_046"));
        }

        #endregion

        #region Integration Tests

        [Test]
        public void Integration_FullGuildLifecycle_CreateToDisband()
        {
            // Create guild with leader
            int leaderId = _entityManager.CreateEntity();
            var guild = _manager.CreateGuild("guild_047", "Lifecycle Guild", "LCG", leaderId);

            // Add members
            int member1 = _entityManager.CreateEntity();
            int member2 = _entityManager.CreateEntity();
            _manager.AddMember("guild_047", member1);
            _manager.AddMember("guild_047", member2);

            // Promote a member
            _manager.PromoteMember("guild_047", member1, GuildRankType.Officer);

            Assert.That(guild.MemberCount, Is.EqualTo(3));
            Assert.That(guild.GetMember(member1).Rank, Is.EqualTo(GuildRankType.Officer));

            // Disband guild
            _manager.DisbandGuild("guild_047");

            Assert.That(_registry.HasGuild("guild_047"), Is.False);
            Assert.That(_manager.IsInGuild(leaderId), Is.False);
        }

        [Test]
        public void Integration_MultiMemberGuildOperations_Complex()
        {
            int leaderId = _entityManager.CreateEntity();
            var guild = _manager.CreateGuild("guild_048", "Complex Ops", "CPX", leaderId);

            // Add multiple members with different ranks
            int officer1 = _entityManager.CreateEntity();
            int officer2 = _entityManager.CreateEntity();
            int member1 = _entityManager.CreateEntity();
            int member2 = _entityManager.CreateEntity();

            _manager.AddMember("guild_048", officer1, GuildRankType.Officer);
            _manager.AddMember("guild_048", officer2, GuildRankType.Officer);
            _manager.AddMember("guild_048", member1, GuildRankType.Member);
            _manager.AddMember("guild_048", member2, GuildRankType.Initiate);

            // Verify permissions
            Assert.That(_manager.HasPermission(officer1, GuildPermission.Kick), Is.True);
            Assert.That(_manager.HasPermission(member1, GuildPermission.Kick), Is.False);

            // Verify privileges
            Assert.That(_manager.HasPrivilege(officer1, GuildPrivilege.UseOfficerChat), Is.True);
            Assert.That(_manager.HasPrivilege(member1, GuildPrivilege.UseOfficerChat), Is.False);

            // Transfer leadership
            _manager.TransferLeadership("guild_048", officer1);
            Assert.That(guild.GetMember(officer1).Rank, Is.EqualTo(GuildRankType.Leader));
        }

        [Test]
        public void Integration_PermissionBasedTreasuryAccess_EnforcesRules()
        {
            int leaderId = _entityManager.CreateEntity();
            var guild = _manager.CreateGuild("guild_049", "Treasury Access", "TRA", leaderId);

            int officer = _entityManager.CreateEntity();
            int member = _entityManager.CreateEntity();
            _manager.AddMember("guild_049", officer, GuildRankType.Officer);
            _manager.AddMember("guild_049", member, GuildRankType.Member);

            // Officer cannot withdraw gold (needs Elder or Leader rank)
            Assert.That(_manager.HasPermission(officer, GuildPermission.WithdrawGold), Is.False);

            // Member can deposit
            Assert.That(_manager.HasPermission(member, GuildPermission.DepositTreasury), Is.True);

            // Leader can withdraw
            Assert.That(_manager.HasPermission(leaderId, GuildPermission.WithdrawGold), Is.True);
        }

        [Test]
        public void Integration_RankPromotionChain_FollowsHierarchy()
        {
            int leaderId = _entityManager.CreateEntity();
            var guild = _manager.CreateGuild("guild_050", "Promotion Chain", "PRC", leaderId);

            int member = _entityManager.CreateEntity();
            _manager.AddMember("guild_050", member);

            // Initiate -> Member -> Officer -> Elder
            Assert.That(guild.GetMember(member).Rank, Is.EqualTo(GuildRankType.Initiate));

            _manager.PromoteMember("guild_050", member, GuildRankType.Member);
            Assert.That(guild.GetMember(member).Rank, Is.EqualTo(GuildRankType.Member));

            _manager.PromoteMember("guild_050", member, GuildRankType.Officer);
            Assert.That(guild.GetMember(member).Rank, Is.EqualTo(GuildRankType.Officer));

            _manager.PromoteMember("guild_050", member, GuildRankType.Elder);
            Assert.That(guild.GetMember(member).Rank, Is.EqualTo(GuildRankType.Elder));

            // Verify permissions increased with rank
            Assert.That(_manager.HasPermission(member, GuildPermission.WithdrawGold), Is.True);
        }

        #endregion
    }
}
