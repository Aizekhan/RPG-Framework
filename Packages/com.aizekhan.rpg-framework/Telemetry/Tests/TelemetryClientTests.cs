using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace RPGFramework.Telemetry.Tests
{
    // WhiteWrite's telemetry contract: opt-in consent, a local queue, batches every 30-60 s and on pause / session end,
    // retry with backoff, never on the game thread, position samples at most every 5 s
    public class TelemetryClientTests
    {
        private FakeTransport _transport;
        private TelemetryClient _client;

        [SetUp]
        public void SetUp()
        {
            _transport = new FakeTransport();
            _client = new TelemetryClient(_transport, new TelemetryOptions { BuildId = "0.1.0", Platform = "Test", BatchSeconds = 30f },
                () => 1700000000000L) { Consent = true, PlayerId = "p1" };
        }

        [TearDown]
        public void TearDown() => _client.Dispose();

        // Lets the worker finish and the client take its answer
        private void Settle()
        {
            _client.WaitForSending(2000);
            _client.Update(0f);
        }

        [Test]
        public void WithoutConsent_NothingIsKept_AndWithdrawingItDropsTheQueue()
        {
            _client.Consent = false;
            Assert.That(_client.Track("a"), Is.False);
            Assert.That(_client.Queued, Is.Zero);

            var changes = new List<bool>();
            _client.ConsentChanged += changes.Add;
            _client.Consent = true;
            _client.Track("a");
            _client.Track("b");
            _client.Consent = false;

            Assert.That(changes, Is.EqualTo(new[] { true, false }));
            Assert.That(_client.Queued, Is.Zero);
            Assert.That(_client.Dropped, Is.EqualTo(2));
        }

        [Test]
        public void EventsGoOutInBatches_EveryBatchSeconds_OrAtOnceOnFlush()
        {
            _client.Track("a");
            _client.Update(10f);
            Assert.That(_transport.Bodies, Is.Empty, "not yet");
            _client.Update(21f);
            Settle();
            Assert.That(_transport.Bodies.Count, Is.EqualTo(1));
            Assert.That(_client.Sent, Is.EqualTo(1));

            _client.Track("b");
            _client.Flush();
            Settle();
            Assert.That(_transport.Bodies.Count, Is.EqualTo(2));
            Assert.That(_transport.Threads.All(t => t != Thread.CurrentThread.ManagedThreadId), Is.True, "never on the game thread");
        }

        [Test]
        public void ABatchCarriesTheEnvelope()
        {
            _client.Variant = "B";
            var e = new TelemetryEvent("combat_hit") { LevelId = "L1", MechanicId = "mech_umcf", Position = new TelemetryPosition(1f, 2f, 3.5f), GameTime = 12.5 }
                .Set("attack", "slash \"quick\"").Set("damage", 10.5).Set("combo", true).Set("count", 3);
            Assert.That(_client.Track(e), Is.True);
            Assert.Throws<InvalidOperationException>(() => e.Set("late", 1), "a tracked event cannot change");
            _client.Flush();
            Settle();

            string body = _transport.Bodies.Single();
            StringAssert.StartsWith("{\"events\":[{\"event_id\":\"", body);
            StringAssert.Contains("\"event_name\":\"combat_hit\",\"schema_version\":1,\"build_id\":\"0.1.0\"", body);
            StringAssert.Contains($"\"session_id\":\"{_client.SessionId}\",\"player_id\":\"p1\",\"timestamp\":1700000000000,\"game_time\":12.5", body);
            StringAssert.Contains("\"level_id\":\"L1\",\"mechanic_id\":\"mech_umcf\",\"position\":[1,2,3.5]", body);
            StringAssert.Contains("\"context\":{\"platform\":\"Test\",\"variant\":\"B\"}", body);
            StringAssert.Contains("\"properties\":{\"attack\":\"slash \\\"quick\\\"\",\"damage\":10.5,\"combo\":true,\"count\":3}", body);
            StringAssert.EndsWith("}]}", body);
        }

        [Test]
        public void AFailedBatch_IsSentAgainWithAGrowingPause_AndARefusedOneIsDropped()
        {
            _transport.Answers.Enqueue(new TelemetryResponse(503, "busy"));
            _transport.Answers.Enqueue(new TelemetryResponse(0, "offline"));
            _client.Track("a");
            _client.Flush();
            Settle();
            Assert.That(_client.Queued, Is.EqualTo(1), "back in the queue");
            Assert.That(_client.LastError, Is.EqualTo("503: busy"));

            _client.Update(1.9f);
            Assert.That(_transport.Bodies.Count, Is.EqualTo(1), "waits 2 s");
            _client.Update(0.2f);
            Settle();
            Assert.That(_transport.Bodies.Count, Is.EqualTo(2));
            _client.Update(3.9f);
            Assert.That(_transport.Bodies.Count, Is.EqualTo(2), "then 4 s");
            _client.Update(0.2f);
            Settle();
            Assert.That(_transport.Bodies.Count, Is.EqualTo(3));
            Assert.That(_client.Sent, Is.EqualTo(1));

            _transport.Answers.Enqueue(new TelemetryResponse(401, "bad key"));
            _client.Track("b");
            _client.Flush();
            Settle();
            Assert.That(_client.Queued, Is.Zero);
            Assert.That(_client.Dropped, Is.EqualTo(1), "a refusal would fail again");
        }

        [Test]
        public void TheCollectorsRejectedEvents_AreCounted_AndBigQueuesGoInSeveralBatches()
        {
            var client = new TelemetryClient(_transport, new TelemetryOptions { MaxBatch = 2, MaxQueue = 3 }) { Consent = true };
            for (int i = 0; i < 5; i++)
                client.Track("e" + i);
            Assert.That(client.Dropped, Is.EqualTo(2), "the oldest go when the queue is full");

            _transport.Answers.Enqueue(new TelemetryResponse(200, "{\"accepted\":1,\"rejected\":[{\"i\":1,\"reason\":\"unknown event\"}]}"));
            client.Flush();
            client.WaitForSending(2000);
            client.Update(0f);
            client.WaitForSending(2000);
            client.Update(0f);

            Assert.That(_transport.Bodies.Count, Is.EqualTo(2));
            StringAssert.Contains("\"e2\"", _transport.Bodies[0]);
            Assert.That(client.Sent, Is.EqualTo(3));
            Assert.That(client.Rejected, Is.EqualTo(1));
            Assert.That(client.LastError, Is.EqualTo("1 rejected: unknown event"));
            client.Dispose();
        }

        [Test]
        public void PositionSamples_AreThrottled_SessionsAndSurveysHaveTheirEvents()
        {
            Assert.That(_client.SamplePosition(new TelemetryPosition(0f, 0f, 0f)), Is.True);
            _client.Update(4f);
            Assert.That(_client.SamplePosition(new TelemetryPosition(1f, 0f, 0f)), Is.False);
            _client.Update(1f);
            Assert.That(_client.SamplePosition(new TelemetryPosition(2f, 0f, 0f), zoneId: "Z"), Is.True);

            string first = _client.SessionId;
            _client.StartSession();
            Assert.That(_client.SessionId, Is.Not.EqualTo(first));
            _client.AnswerSurvey("pt1", "fun", "4");
            _client.EndSession();
            Settle();

            string body = _transport.Bodies.Single();
            Assert.That(Count(body, "\"PositionSample\""), Is.EqualTo(2));
            StringAssert.Contains("\"SessionStarted\"", body);
            StringAssert.Contains("\"properties\":{\"playtest\":\"pt1\",\"question\":\"fun\",\"answer\":\"4\"}", body);
            StringAssert.Contains("\"SessionEnded\"", body);
        }

        [Test]
        public void AnEventHasAtMost20Properties_AndAPlayerIdIsPseudonymous()
        {
            var e = new TelemetryEvent("x");
            for (int i = 0; i < TelemetryEvent.MaxProperties; i++)
                e.Set("p" + i, i);
            e.Set("p0", "again");
            Assert.That(e.Get("p0"), Is.EqualTo("again"));
            Assert.Throws<InvalidOperationException>(() => e.Set("one more", 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TelemetryEvent("y").Set("n", double.NaN));

            string id = TelemetryIds.Pseudonymous("steam:123", "game-salt");
            Assert.That(id, Has.Length.EqualTo(64));
            Assert.That(TelemetryIds.Pseudonymous("steam:123", "game-salt"), Is.EqualTo(id));
            Assert.That(TelemetryIds.Pseudonymous("steam:123", "other"), Is.Not.EqualTo(id));
            Assert.That(id, Does.Not.Contain("123"));
        }

        [Test]
        public void TheHttpTransport_NeedsAnAddress_AndReportsAnUnreachableOneAsRetry()
        {
            Assert.Throws<ArgumentException>(() => new HttpTelemetryTransport("not an address", "k"));
            using (var http = new HttpTelemetryTransport("http://127.0.0.1:9/", "wwi_test", TimeSpan.FromSeconds(2)))
            {
                var response = http.SendAsync("{\"events\":[]}", CancellationToken.None).GetAwaiter().GetResult();
                Assert.That(response.Retry, Is.True);
                Assert.That(response.Succeeded, Is.False);
            }
        }

        private static int Count(string text, string part)
        {
            int count = 0;
            for (int at = text.IndexOf(part, StringComparison.Ordinal); at >= 0; at = text.IndexOf(part, at + 1, StringComparison.Ordinal))
                count++;
            return count;
        }

        private sealed class FakeTransport : ITelemetryTransport
        {
            public readonly List<string> Bodies = new List<string>();
            public readonly List<int> Threads = new List<int>();
            public readonly Queue<TelemetryResponse> Answers = new Queue<TelemetryResponse>();

            public Task<TelemetryResponse> SendAsync(string json, CancellationToken cancellationToken)
            {
                lock (Bodies)
                {
                    Bodies.Add(json);
                    Threads.Add(Thread.CurrentThread.ManagedThreadId);
                    return Task.FromResult(Answers.Count > 0 ? Answers.Dequeue() : new TelemetryResponse(200, "{\"accepted\":1,\"rejected\":[]}"));
                }
            }
        }
    }
}
