using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
#nullable enable

namespace RPGFramework.Logging.Tests
{
    public sealed class LoggerTests
    {
        private MemoryLogSink _sink = null!;
        private Logger _logger = null!;

        [SetUp]
        public void SetUp()
        {
            _sink = new MemoryLogSink(16);
            _logger = new Logger(LogSeverity.Information, _sink);
            _logger.SetClock(() => new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc));
        }

        [Test]
        public void Log_BelowMinimumSeverity_IsDropped()
        {
            _logger.LogDebug("hidden");
            _logger.LogInfo("shown");

            Assert.That(_sink.Snapshot().Select(e => e.Message), Is.EqualTo(new[] { "shown" }));
        }

        [Test]
        public void Log_WithoutCategory_UsesDefaultCategory()
        {
            _logger.LogInfo("message");

            Assert.That(_sink.Snapshot()[0].Category, Is.EqualTo(Logger.DefaultCategory));
        }

        [Test]
        public void CategoryLevel_OverridesMinimumInBothDirections()
        {
            _logger.SetCategoryLevel("Verbose", LogSeverity.Trace);
            _logger.SetCategoryLevel("Quiet", LogSeverity.Error);

            _logger.LogTrace("verbose trace", "Verbose");
            _logger.LogWarning("quiet warning", "Quiet");
            _logger.LogError("quiet error", "Quiet");

            Assert.That(_sink.Snapshot().Select(e => e.Message), Is.EqualTo(new[] { "verbose trace", "quiet error" }));
        }

        [Test]
        public void Context_IsCapturedPerEntry()
        {
            _logger.SetContext("player", 7);
            _logger.LogInfo("first");
            _logger.SetContext("player", 8);
            _logger.LogInfo("second");
            _logger.ClearContext();
            _logger.LogInfo("third");

            var entries = _sink.Snapshot();
            Assert.That(entries[0].Properties!["player"], Is.EqualTo(7));
            Assert.That(entries[1].Properties!["player"], Is.EqualTo(8));
            Assert.That(entries[2].Properties, Is.Null);
        }

        [Test]
        public void Enricher_AddsPropertiesAndFailingEnricherIsIsolated()
        {
            _logger.AddEnricher(_ => throw new InvalidOperationException("boom"));
            _logger.AddEnricher(p => p["frame"] = 42);

            _logger.LogInfo("message");

            Assert.That(_sink.Snapshot()[0].Properties!["frame"], Is.EqualTo(42));
            Assert.That(_logger.SuppressedErrorCount, Is.EqualTo(1));
        }

        [Test]
        public void FailingSink_DoesNotStopOtherSinksOrThrow()
        {
            var logger = new Logger(LogSeverity.Trace, new ThrowingSink(), _sink);

            Assert.DoesNotThrow(() => logger.LogInfo("message"));
            Assert.That(_sink.Count, Is.EqualTo(1));
            Assert.That(logger.SuppressedErrorCount, Is.EqualTo(1));
        }

        [Test]
        public void ForCategory_AppliesCategoryUnlessCallerOverrides()
        {
            var combat = _logger.ForCategory("Combat");

            combat.LogInfo("hit");
            combat.LogInfo("override", "Other");

            Assert.That(_sink.Snapshot().Select(e => e.Category), Is.EqualTo(new[] { "Combat", "Other" }));
        }

        [Test]
        public void NullLoggerAndNullReference_AreSafe()
        {
            ILogger? missing = null;

            Assert.DoesNotThrow(() => missing.LogError("ignored"));
            Assert.DoesNotThrow(() => NullLogger.Instance.LogCritical("ignored"));
        }

        [Test]
        public void MemoryLogSink_KeepsMostRecentEntriesInOrder()
        {
            var sink = new MemoryLogSink(3);
            var logger = new Logger(LogSeverity.Trace, sink);

            for (int i = 0; i < 5; i++)
                logger.LogInfo(i.ToString());

            Assert.That(sink.Snapshot().Select(e => e.Message), Is.EqualTo(new[] { "2", "3", "4" }));
        }

        [Test]
        public void RemoveSink_StopsDelivery()
        {
            _logger.RemoveSink(_sink);
            _logger.LogInfo("message");

            Assert.That(_sink.Count, Is.EqualTo(0));
        }

        [Test]
        public void LogEntry_ToString_IncludesSeverityCategoryPropertiesAndException()
        {
            _logger.SetContext("id", "abc");
            _logger.LogError("failed", "Save", new InvalidOperationException("disk"));

            string text = _sink.Snapshot()[0].ToString();

            StringAssert.Contains("2026-01-02 03:04:05.000Z [Error] [Save] failed", text);
            StringAssert.Contains("id=abc", text);
            StringAssert.Contains("disk", text);
        }

        private sealed class ThrowingSink : ILogSink
        {
            public void Write(in LogEntry entry) => throw new IOException("sink failure");

            public void Flush()
            {
            }
        }
    }

    public sealed class FileLogSinkTests
    {
        private string _directory = null!;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "RPGFrameworkLogTests_" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, true);
        }

        [Test]
        public void Write_IsBufferedUntilFlush()
        {
            using var sink = new FileLogSink(_directory, bufferSize: 10);
            var logger = new Logger(LogSeverity.Trace, sink);

            logger.LogInfo("buffered");
            Assert.That(sink.CurrentFilePath, Is.Null);

            logger.Flush();
            StringAssert.Contains("buffered", File.ReadAllText(sink.CurrentFilePath));
        }

        [Test]
        public void Write_ErrorFlushesImmediately()
        {
            using var sink = new FileLogSink(_directory, bufferSize: 100);
            var logger = new Logger(LogSeverity.Trace, sink);

            logger.LogInfo("before");
            logger.LogError("urgent");

            string text = File.ReadAllText(sink.CurrentFilePath);
            StringAssert.Contains("before", text);
            StringAssert.Contains("urgent", text);
        }

        [Test]
        public void Header_IsWrittenAtTopOfFile()
        {
            using var sink = new FileLogSink(_directory, bufferSize: 1, header: "=== header ===");
            new Logger(LogSeverity.Trace, sink).LogInfo("entry");

            Assert.That(File.ReadAllLines(sink.CurrentFilePath)[0], Is.EqualTo("=== header ==="));
        }

        [Test]
        public void Rotation_StartsNewFileAndKeepsMaxFiles()
        {
            using var sink = new FileLogSink(_directory, bufferSize: 1, maxFileBytes: 10, maxFiles: 2);
            var logger = new Logger(LogSeverity.Trace, sink);

            for (int i = 0; i < 6; i++)
                logger.LogInfo("a message longer than ten bytes " + i);

            var files = Directory.GetFiles(_directory, "log_*.log");
            Assert.That(files.Length, Is.EqualTo(2));
            StringAssert.Contains("bytes 5", File.ReadAllText(sink.CurrentFilePath));
        }

        [Test]
        public void Dispose_FlushesPendingEntries()
        {
            var sink = new FileLogSink(_directory, bufferSize: 100);
            var logger = new Logger(LogSeverity.Trace, sink);
            logger.LogInfo("pending");

            logger.Dispose();

            string file = Directory.GetFiles(_directory, "log_*.log").Single();
            StringAssert.Contains("pending", File.ReadAllText(file));
        }
    }
}
