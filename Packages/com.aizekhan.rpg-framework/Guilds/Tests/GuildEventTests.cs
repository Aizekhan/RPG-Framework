using System.Collections.Generic;
using NUnit.Framework;
using RPGFramework.ECS;
using RPGFramework.Events;
using RPGFramework.Factions;
using RPGFramework.Guilds.Runtime;

namespace RPGFramework.Guilds.Tests
{
    public sealed class GuildEventTests
    {
        [Test]
        public void Manager_PublishesLifecycleAndMembershipEvents()
        {
            var bus = new EventBus();
            var log = new List<string>();
            bus.Subscribe<GuildCreatedEvent>(e => log.Add($"created {e.Guild.GuildId} by {e.FounderId}"));
            bus.Subscribe<MemberJoinedEvent>(e => log.Add($"joined {e.EntityId} as {e.InitialRank}"));
            bus.Subscribe<MemberPromotedEvent>(e => log.Add($"promoted {e.EntityId} {e.OldRank}->{e.NewRank}"));
            bus.Subscribe<MemberDemotedEvent>(e => log.Add($"demoted {e.EntityId} {e.OldRank}->{e.NewRank}"));
            bus.Subscribe<MemberLeftEvent>(e => log.Add($"left {e.EntityId} was {e.LastRank}"));
            bus.Subscribe<GuildDisbandedEvent>(e => log.Add($"disbanded {e.GuildId}"));

            var entities = new EntityManager();
            var manager = new GuildManager(entities, new FactionRegistry(), new GuildRegistry(), bus);
            int leader = entities.CreateEntity();
            int recruit = entities.CreateEntity();

            manager.CreateGuild("g1", "Wolves", "WLF", leader);
            manager.AddMember("g1", recruit);
            manager.PromoteMember("g1", recruit, GuildRankType.Officer);
            manager.DemoteMember("g1", recruit, GuildRankType.Member);
            manager.PromoteMember("g1", recruit, GuildRankType.Initiate); // not a promotion: no event
            manager.RemoveMember("g1", recruit);
            manager.DisbandGuild("g1");

            CollectionAssert.AreEqual(new[]
            {
                $"created g1 by {leader}",
                $"joined {recruit} as Initiate",
                $"promoted {recruit} Initiate->Officer",
                $"demoted {recruit} Officer->Member",
                $"left {recruit} was Member",
                "disbanded g1"
            }, log);
        }
    }
}
