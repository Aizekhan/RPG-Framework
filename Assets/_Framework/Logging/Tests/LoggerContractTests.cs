using System;
using NUnit.Framework;
using RPGFramework.Logging;
#nullable enable

namespace RPGFramework.Logging.Tests
{
    public sealed class LoggerContractTests
    {
        [Test]
        public void Log_PreservesSeverityMessageCategoryAndException()
        {
            var logger = new RecordingLogger();
            var exception = new InvalidOperationException("test failure");

            logger.Log(LogSeverity.Error, "operation failed", "ContractTest", exception);

            Assert.That(logger.LastSeverity, Is.EqualTo(LogSeverity.Error));
            Assert.That(logger.LastMessage, Is.EqualTo("operation failed"));
            Assert.That(logger.LastCategory, Is.EqualTo("ContractTest"));
            Assert.That(logger.LastException, Is.SameAs(exception));
        }

        private sealed class RecordingLogger : ILogger
        {
            public LogSeverity LastSeverity { get; private set; }
            public string LastMessage { get; private set; } = string.Empty;
            public string? LastCategory { get; private set; }
            public Exception? LastException { get; private set; }

            public void Log(LogSeverity severity, string message, string? category = null, Exception? exception = null)
            {
                LastSeverity = severity;
                LastMessage = message;
                LastCategory = category;
                LastException = exception;
            }
        }
    }
}