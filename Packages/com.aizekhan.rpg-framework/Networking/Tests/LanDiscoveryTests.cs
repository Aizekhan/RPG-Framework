using System;
using System.Linq;
using System.Net;
using System.Threading;
using NUnit.Framework;

namespace RPGFramework.Networking.Tests
{
    public sealed class LanDiscoveryTests
    {
        private const int Port = 47991;

        private static void WaitFor(Func<bool> condition, LanDiscoveryResponder host, LanDiscovery player)
        {
            for (int i = 0; i < 100 && !condition(); i++)
            {
                host?.Poll();
                player.Poll();
                Thread.Sleep(10);
            }
        }

        [Test]
        public void APlayer_FindsAHostOfTheSameGame_WithItsJoinCodeAndHowFullItIs()
        {
            int players = 2;
            using (var host = new LanDiscoveryResponder("myth-1.0", () => new LanGameDescription
                   {
                       HostName = "Ann", Players = players, MaxPlayers = 4, InLobby = true, GamePort = 7790
                   }, Port))
            using (var player = new LanDiscovery("myth-1.0", Port))
            {
                Assert.That(host.Start(), Is.True);
                LanGame found = null;
                player.Found += game => found = found ?? game;
                Assert.That(player.Search(new[] { IPAddress.Loopback }), Is.True);
                WaitFor(() => player.Games.Any(g => g.JoinCode == "127.0.0.1:7790"), host, player);

                var game = player.Games.First(g => g.JoinCode == "127.0.0.1:7790");
                Assert.That(game.HostName, Is.EqualTo("Ann"));
                Assert.That(game.Players, Is.EqualTo(2));
                Assert.That(game.MaxPlayers, Is.EqualTo(4));
                Assert.That(game.InLobby, Is.True);
                Assert.That(found, Is.Not.Null);

                // Asking again updates the same entry
                players = 3;
                int count = player.Games.Count;
                player.Search(new[] { IPAddress.Loopback });
                WaitFor(() => game.Players == 3, host, player);
                Assert.That(game.Players, Is.EqualTo(3));
                Assert.That(player.Games.Count, Is.EqualTo(count));

                // A host that stops answering drops out
                player.Poll(player.ForgetAfter + 1f);
                Assert.That(player.Games, Is.Empty);
            }
        }

        [Test]
        public void HostsOfAnotherGameOrVersion_DoNotAnswer()
        {
            using (var host = new LanDiscoveryResponder("myth-1.0", () => new LanGameDescription { HostName = "Ann", GamePort = 7777 }, Port + 1))
            using (var player = new LanDiscovery("myth-1.1", Port + 1))
            {
                Assert.That(host.Start(), Is.True);
                player.Search(new[] { IPAddress.Loopback });
                WaitFor(() => host.Answered > 0, host, player);
                for (int i = 0; i < 10; i++)
                {
                    host.Poll();
                    player.Poll();
                    Thread.Sleep(10);
                }

                Assert.That(host.Answered, Is.Zero);
                Assert.That(player.Games, Is.Empty);
            }
        }

        [Test]
        public void TheUsualGamePort_IsLeftOutOfTheJoinCode() =>
            Assert.That(LanDiscoveryProtocol.JoinCodeOf(IPAddress.Parse("192.168.1.20"), 7777), Is.EqualTo("192.168.1.20"));
    }
}
