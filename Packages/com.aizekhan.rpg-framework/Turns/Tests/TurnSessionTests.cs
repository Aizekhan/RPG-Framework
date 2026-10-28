using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RPGFramework.Networking;

namespace RPGFramework.Turns.Tests
{
    public sealed class TurnSessionTests
    {
        private const int Port = 7788;
        private const string Fingerprint = "race-1";

        // A race: on turn a seat moves 1 to 6; every seat has a secret card only it may see
        private sealed class RaceRules : ITurnRules
        {
            private readonly int[] _positions;

            public RaceRules(int seats) => _positions = new int[seats];

            public int CurrentSeat { get; set; }
            public int Position(int seat) => _positions[seat];

            public bool CanAct(int seat, TurnIntent intent) => seat == CurrentSeat && intent.Kind == "Move";

            public TurnOutcome Apply(int seat, TurnIntent intent)
            {
                int steps = intent.Number();
                if (steps < 1 || steps > 6)
                    return TurnOutcome.Refused("1 to 6");
                _positions[seat] += steps;
                CurrentSeat = (CurrentSeat + 1) % _positions.Length;
                return TurnOutcome.Ok;
            }

            // Whose turn, every position, then this seat's secret card
            public byte[] ViewFor(int seat) =>
                new[] { (byte)CurrentSeat }.Concat(_positions.Select(p => (byte)p)).Concat(new[] { (byte)(100 + seat) }).ToArray();

            public TurnIntent DefaultIntent(int seat) => TurnIntent.Of("Move", 1);
        }

        private LoopbackNetwork _loopback;
        private TurnHost _host;
        private readonly List<TurnClient> _clients = new List<TurnClient>();

        [SetUp]
        public void SetUp()
        {
            _loopback = new LoopbackNetwork();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var client in _clients)
                client.Dispose();
            _clients.Clear();
            _host?.Dispose();
            _host = null;
        }

        private TurnHost Host(TurnHostOptions options = null)
        {
            options = options ?? new TurnHostOptions();
            if (string.IsNullOrEmpty(options.Fingerprint))
                options.Fingerprint = Fingerprint;
            _host = new TurnHost(TurnProtocol.CreateNetwork(_loopback.CreateTransport()), "Ann", options);
            _host.Open(Port);
            return _host;
        }

        private TurnClient Join(string name, string fingerprint = Fingerprint, string token = "")
        {
            var client = new TurnClient(TurnProtocol.CreateNetwork(_loopback.CreateTransport()), name, fingerprint)
            {
                RejoinToken = token
            };
            _clients.Add(client);
            client.Join("loopback", Port);
            Pump();
            return client;
        }

        private void Pump(int frames = 8, float deltaTime = 0.02f)
        {
            for (int i = 0; i < frames; i++)
            {
                _host?.Update(deltaTime);
                foreach (var client in _clients)
                    client.Update(deltaTime);
            }
        }

        private RaceRules StartRace(params TurnClient[] players)
        {
            var rules = new RaceRules(players.Length + 1);
            _host.Start(rules, new byte[] { 42 });
            Pump();
            return rules;
        }

        [Test]
        public void Players_TakeSeatsInTheLobby_AndEveryoneSeesThem()
        {
            Host();
            var bob = Join("Bob");
            var cid = Join("Cid");

            Assert.AreEqual(TurnClientState.Seated, bob.State);
            Assert.AreEqual(1, bob.LocalSeat);
            Assert.AreEqual(2, cid.LocalSeat);
            Assert.IsNotEmpty(bob.RejoinToken);
            CollectionAssert.AreEqual(new[] { "Ann", "Bob", "Cid" }, bob.Seats.Select(s => s.Name).ToArray());
            Assert.AreEqual(3, _host.Seats.Count);

            bob.SetReady(true);
            cid.SetReady(true);
            Pump();
            Assert.IsFalse(_host.AllReady);
            _host.SetReady(true);
            Pump();
            Assert.IsTrue(_host.AllReady);
            Assert.IsTrue(cid.Seats.All(s => s.Ready));
        }

        [Test]
        public void APlayerWithAnotherGame_IsRefused_WithTheReason()
        {
            Host();
            var bob = Join("Bob", "race-2");

            Assert.AreEqual(TurnClientState.Ended, bob.State);
            Assert.AreEqual("Another version of the game", bob.EndReason);
            Assert.AreEqual(1, _host.Seats.Count);
        }

        [Test]
        public void AFullTable_AndAStartedMatch_RefuseNewPlayers()
        {
            Host(new TurnHostOptions { MaxSeats = 2 });
            var bob = Join("Bob");
            var cid = Join("Cid");
            Assert.AreEqual("The table is full", cid.EndReason);

            StartRace(bob);
            var dan = Join("Dan");
            Assert.AreEqual("The match has already started", dan.EndReason);
        }

        [Test]
        public void TheMatchStarts_WithTheOptions_AndEachSeatGetsOnlyItsOwnView()
        {
            Host();
            var bob = Join("Bob");
            var cid = Join("Cid");
            StartRace(bob, cid);

            Assert.IsTrue(bob.IsStarted);
            CollectionAssert.AreEqual(new byte[] { 42 }, bob.Options);
            CollectionAssert.AreEqual(new byte[] { 0, 0, 0, 0, 100 }, _host.View);
            CollectionAssert.AreEqual(new byte[] { 0, 0, 0, 0, 101 }, bob.View);
            CollectionAssert.AreEqual(new byte[] { 0, 0, 0, 0, 102 }, cid.View);
        }

        [Test]
        public void MovesAreChecked_OnTheHost_AndTheAnswerComesBack()
        {
            Host();
            var bob = Join("Bob");
            var rules = StartRace(bob);
            var replies = new List<(int, TurnOutcome)>();
            bob.Replied += (id, outcome) => replies.Add((id, outcome));
            var moves = new List<(int, TurnIntent)>();
            _host.Accepted += (seat, intent) => moves.Add((seat, intent));

            int early = bob.Submit(TurnIntent.Of("Move", 3));
            Pump();
            Assert.AreEqual(early, replies.Single().Item1);
            Assert.AreEqual("Not your move now", replies.Single().Item2.Reason);
            Assert.AreEqual(0, rules.Position(1));

            TurnOutcome hostOutcome = default;
            _host.Replied += (id, outcome) => hostOutcome = outcome;
            Assert.Greater(_host.Submit(TurnIntent.Of("Move", 2)), 0);
            Assert.IsTrue(hostOutcome.Accepted);
            Pump();
            int tooFar = bob.Submit(TurnIntent.Of("Move", 9));
            int fine = bob.Submit(TurnIntent.Of("Move", 4));
            Pump();

            Assert.AreEqual("1 to 6", replies.Single(r => r.Item1 == tooFar).Item2.Reason);
            Assert.IsTrue(replies.Single(r => r.Item1 == fine).Item2.Accepted);
            CollectionAssert.AreEqual(new byte[] { 0, 2, 4, 101 }, bob.View);
            CollectionAssert.AreEqual(new[] { 0, 1 }, moves.Select(m => m.Item1).ToArray());
            Assert.AreEqual(4, moves[1].Item2.Number());
        }

        [Test]
        public void Notices_ReachOnlyTheSeatsThatMaySeeThem()
        {
            Host();
            var bob = Join("Bob");
            var cid = Join("Cid");
            StartRace(bob, cid);
            var seen = new Dictionary<string, List<string>> { ["Ann"] = new List<string>(), ["Bob"] = new List<string>(), ["Cid"] = new List<string>() };
            _host.Noticed += n => seen["Ann"].Add(n.Kind);
            bob.Noticed += n => seen["Bob"].Add(n.Kind);
            cid.Noticed += n => seen["Cid"].Add(n.Kind);

            _host.Notify(new TurnNotice("die", new byte[] { 5 }));
            _host.Notify(new TurnNotice("card"), TurnVisibility.Seat, 2);
            _host.Notify(new TurnNotice("trap"), TurnVisibility.Host);
            _host.Notify(new TurnNotice("drew"), TurnVisibility.Others, 1);
            Pump();

            CollectionAssert.AreEqual(new[] { "die", "trap", "drew" }, seen["Ann"]);
            CollectionAssert.AreEqual(new[] { "die" }, seen["Bob"]);
            CollectionAssert.AreEqual(new[] { "die", "card", "drew" }, seen["Cid"]);
        }

        [Test]
        public void AQuestion_ClosesWhenAllAnswer_OrPassesWhenTimeIsUp()
        {
            Host();
            var bob = Join("Bob");
            var cid = Join("Cid");
            StartRace(bob, cid);
            TurnPrompt asked = null;
            bob.Prompted += p => asked = p;
            IReadOnlyDictionary<int, int> answers = null;

            _host.OpenPrompt("block", new[] { "Block", "Dodge" }, new[] { 1, 2 }, 0f, a => answers = a, title: "Stop the ambush?");
            Pump();
            Assert.AreEqual("block", asked.Kind);
            Assert.AreEqual("Stop the ambush?", asked.Title);
            Assert.AreEqual(1, cid.OpenPrompts.Count);
            bob.Answer(asked.WindowId, 1);
            Pump();
            Assert.IsNull(answers);
            cid.Answer(asked.WindowId, TurnPrompt.Pass);
            Pump();
            Assert.AreEqual(1, answers[1]);
            Assert.AreEqual(TurnPrompt.Pass, answers[2]);
            Assert.AreEqual(0, bob.OpenPrompts.Count);

            answers = null;
            _host.OpenPrompt("ambush", new[] { "Pay" }, new[] { 1 }, 0.5f, a => answers = a);
            Pump();
            Assert.AreEqual(1, bob.OpenPrompts.Count);
            Pump(40);
            Assert.AreEqual(TurnPrompt.Pass, answers[1]);
            Assert.AreEqual(0, bob.OpenPrompts.Count);
        }

        [Test]
        public void WhenTheTurnTimeRunsOut_TheDefaultMoveIsMade()
        {
            Host(new TurnHostOptions { TurnSeconds = 0.5f });
            var bob = Join("Bob");
            var rules = StartRace(bob);
            rules.CurrentSeat = 1;
            Pump(40);

            Assert.AreEqual(1, rules.Position(1));
            Assert.AreEqual(1, bob.View[2]);
        }

        [Test]
        public void APlayerWhoLeaves_KeepsTheSeat_AndComesBackWithTheToken()
        {
            Host();
            var bob = Join("Bob");
            var rules = StartRace(bob);
            string token = bob.RejoinToken;
            bob.Leave();
            Pump();
            Assert.IsFalse(_host.Seats[1].Connected);

            _host.Submit(TurnIntent.Of("Move", 3));
            var promptId = _host.OpenPrompt("block", new[] { "Block" }, new[] { 0, 1 }, 0f, _ => { });
            var back = Join("Bob", token: token);

            Assert.AreEqual(TurnClientState.Seated, back.State);
            Assert.AreEqual(1, back.LocalSeat);
            Assert.IsTrue(back.IsStarted);
            Assert.IsTrue(_host.Seats[1].Connected);
            CollectionAssert.AreEqual(new byte[] { 1, 3, 0, 101 }, back.View);
            Assert.AreEqual(promptId, back.OpenPrompts.Single().WindowId);
            Assert.AreEqual(2, _host.Seats.Count);
            Assert.AreEqual(1, rules.CurrentSeat);
        }

        [Test]
        public void AnAwaySeat_PassesQuestions_SoTheTableIsNotHeldUp()
        {
            Host();
            var bob = Join("Bob");
            StartRace(bob);
            bob.Leave();
            Pump();
            IReadOnlyDictionary<int, int> answers = null;

            _host.OpenPrompt("block", new[] { "Block" }, new[] { 1 }, 0f, a => answers = a);

            Assert.AreEqual(TurnPrompt.Pass, answers[1]);
        }
    }
}
