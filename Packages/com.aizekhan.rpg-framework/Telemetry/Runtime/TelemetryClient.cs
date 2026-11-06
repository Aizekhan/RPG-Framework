using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace RPGFramework.Telemetry
{
    /// <summary>
    /// How a <see cref="TelemetryClient"/> batches and sends.
    /// </summary>
    public sealed class TelemetryOptions
    {
        /// <summary>
        /// The build the events come from (a version or a commit).
        /// </summary>
        public string BuildId { get; set; } = "";

        /// <summary>
        /// The version of the game's own event schema (its event names and properties).
        /// </summary>
        public int SchemaVersion { get; set; } = 1;

        /// <summary>
        /// The platform in every event's context (Windows, Android...).
        /// </summary>
        public string Platform { get; set; } = "";

        /// <summary>
        /// Seconds between batches (30 to 60 keeps a collector cheap); <see cref="TelemetryClient.Flush"/> sends at once.
        /// </summary>
        public float BatchSeconds { get; set; } = 30f;

        /// <summary>
        /// The most events in one batch (a collector takes at most 500).
        /// </summary>
        public int MaxBatch { get; set; } = 500;

        /// <summary>
        /// The most events kept waiting; beyond it the oldest are dropped (<see cref="TelemetryClient.Dropped"/>).
        /// </summary>
        public int MaxQueue { get; set; } = 5000;

        /// <summary>
        /// The least seconds between two <see cref="TelemetryClient.PositionSampleEvent"/>s (never one per frame).
        /// </summary>
        public float PositionSampleSeconds { get; set; } = 5f;

        /// <summary>
        /// Seconds before the first retry of a batch the collector did not take; each further failure doubles it.
        /// </summary>
        public float RetrySeconds { get; set; } = 2f;

        public float MaxRetrySeconds { get; set; } = 300f;
    }

    /// <summary>
    /// Telemetry for playtests and balancing: the game tracks events, the service sends them in batches.
    /// </summary>
    public interface ITelemetry
    {
        /// <summary>
        /// The player agreed to send telemetry (opt-in: off until set). Turning it off drops everything waiting.
        /// </summary>
        bool Consent { get; set; }

        event Action<bool> ConsentChanged;

        string SessionId { get; }

        /// <summary>
        /// The player, pseudonymous (<see cref="TelemetryIds.Pseudonymous"/>); never a name or an e-mail.
        /// </summary>
        string PlayerId { get; set; }

        /// <summary>
        /// The game mode in every event's context.
        /// </summary>
        string Mode { get; set; }

        string Cohort { get; set; }

        /// <summary>
        /// The A/B variant this player plays.
        /// </summary>
        string Variant { get; set; }

        /// <returns>False when there is no consent (the event is not kept).</returns>
        bool Track(TelemetryEvent telemetryEvent);

        bool Track(string name);

        /// <summary>
        /// Where the player is, at most once per <see cref="TelemetryOptions.PositionSampleSeconds"/>.
        /// </summary>
        /// <returns>True when a sample was kept.</returns>
        bool SamplePosition(TelemetryPosition position, string levelId = null, string zoneId = null);

        /// <summary>
        /// A playtest survey's answer.
        /// </summary>
        bool AnswerSurvey(string playtest, string question, string answer);

        /// <summary>
        /// A new session id and its start event.
        /// </summary>
        void StartSession();

        /// <summary>
        /// The session's end event, then everything waiting is sent.
        /// </summary>
        void EndSession();

        /// <summary>
        /// Sends what is waiting now instead of at the next batch time.
        /// </summary>
        void Flush();

        /// <summary>
        /// Real time passing (a paused game too): sends batches when due and takes the collector's answers.
        /// </summary>
        void Update(float deltaTime);
    }

    /// <summary>
    /// The telemetry client: tracked events wait in a queue (at most <see cref="TelemetryOptions.MaxQueue"/>), go out
    /// every <see cref="TelemetryOptions.BatchSeconds"/> in batches of up to <see cref="TelemetryOptions.MaxBatch"/>
    /// through an <see cref="ITelemetryTransport"/> on a worker thread (never the game's), and a batch the collector
    /// could not take is sent again later with a growing pause. Nothing is kept or sent without
    /// <see cref="Consent"/>. Position samples are throttled. The queue lives in memory: events of a session that
    /// crashed before its batch went out are lost.
    /// </summary>
    public sealed class TelemetryClient : ITelemetry, IDisposable
    {
        public const string SessionStartedEvent = "SessionStarted";
        public const string SessionEndedEvent = "SessionEnded";
        public const string PositionSampleEvent = "PositionSample";
        public const string SurveyAnsweredEvent = "SurveyAnswered";

        private static readonly Regex RejectedItem = new Regex("\"i\"\\s*:", RegexOptions.CultureInvariant);
        private static readonly Regex RejectedList = new Regex("\"rejected\"\\s*:\\s*\\[(.*)\\]", RegexOptions.Singleline | RegexOptions.CultureInvariant);
        private static readonly Regex FirstReason = new Regex("\"reason\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"", RegexOptions.CultureInvariant);

        private readonly ITelemetryTransport _transport;
        private readonly TelemetryOptions _options;
        private readonly Func<long> _now;
        private readonly List<TelemetryEvent> _queue = new List<TelemetryEvent>();
        private readonly CancellationTokenSource _stop = new CancellationTokenSource();
        private List<TelemetryEvent> _inFlight;
        private Task<TelemetryResponse> _sending;
        private bool _consent;
        private float _time;
        private float _nextSendAt;
        private float _nextPositionAt;
        private int _failures;
        private TelemetryIdentity _identity;
        private TelemetryContext _context;
        private string _playerId = "";
        private string _mode;
        private string _cohort;
        private string _variant;

        /// <param name="now">Unix milliseconds for the events' timestamps; null: the system clock.</param>
        public TelemetryClient(ITelemetryTransport transport, TelemetryOptions options = null, Func<long> now = null)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _options = options ?? new TelemetryOptions();
            if (_options.MaxBatch < 1 || _options.MaxQueue < 1)
                throw new ArgumentException("MaxBatch and MaxQueue are at least 1", nameof(options));
            _now = now ?? (() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            SessionId = Guid.NewGuid().ToString();
            _nextSendAt = _options.BatchSeconds;
            Refresh();
        }

        public event Action<bool> ConsentChanged;

        public bool Consent
        {
            get => _consent;
            set
            {
                if (_consent == value)
                    return;
                _consent = value;
                if (!value)
                {
                    Dropped += _queue.Count;
                    _queue.Clear();
                }
                ConsentChanged?.Invoke(value);
            }
        }

        public string SessionId { get; private set; }

        public string PlayerId { get => _playerId; set { _playerId = value ?? ""; Refresh(); } }
        public string Mode { get => _mode; set { _mode = value; Refresh(); } }
        public string Cohort { get => _cohort; set { _cohort = value; Refresh(); } }
        public string Variant { get => _variant; set { _variant = value; Refresh(); } }

        /// <summary>
        /// Events waiting to be sent (the batch on its way not counted).
        /// </summary>
        public int Queued => _queue.Count;

        /// <summary>
        /// Events the collector took.
        /// </summary>
        public int Sent { get; private set; }

        /// <summary>
        /// Events the collector took but refused one by one (its answer's <c>rejected</c> list).
        /// </summary>
        public int Rejected { get; private set; }

        /// <summary>
        /// Events lost: the queue was full, consent was withdrawn, or the collector refused a whole batch for good.
        /// </summary>
        public int Dropped { get; private set; }

        /// <summary>
        /// The last thing that went wrong (a status and reason), or null.
        /// </summary>
        public string LastError { get; private set; }

        /// <summary>
        /// A batch is on its way.
        /// </summary>
        public bool IsSending => _sending != null;

        public bool Track(string name) => Track(new TelemetryEvent(name));

        public bool Track(TelemetryEvent telemetryEvent)
        {
            if (telemetryEvent == null)
                throw new ArgumentNullException(nameof(telemetryEvent));
            if (telemetryEvent.IsTracked)
                throw new InvalidOperationException($"'{telemetryEvent.Name}' is already tracked");
            if (!_consent)
                return false;
            telemetryEvent.Track(Guid.NewGuid().ToString(), _now(), _identity, _context);
            if (_queue.Count >= _options.MaxQueue)
            {
                _queue.RemoveAt(0);
                Dropped++;
            }
            _queue.Add(telemetryEvent);
            return true;
        }

        public bool SamplePosition(TelemetryPosition position, string levelId = null, string zoneId = null)
        {
            if (!_consent || _time < _nextPositionAt)
                return false;
            _nextPositionAt = _time + Math.Max(0f, _options.PositionSampleSeconds);
            return Track(new TelemetryEvent(PositionSampleEvent) { Position = position, LevelId = levelId, ZoneId = zoneId });
        }

        public bool AnswerSurvey(string playtest, string question, string answer) =>
            Track(new TelemetryEvent(SurveyAnsweredEvent).Set("playtest", playtest ?? "").Set("question", question ?? "").Set("answer", answer ?? ""));

        public void StartSession()
        {
            SessionId = Guid.NewGuid().ToString();
            Refresh();
            Track(SessionStartedEvent);
        }

        public void EndSession()
        {
            Track(SessionEndedEvent);
            Flush();
        }

        public void Flush()
        {
            _nextSendAt = _time;
            Collect();
            if (_sending == null)
                Send();
        }

        public void Update(float deltaTime)
        {
            if (deltaTime > 0f)
                _time += deltaTime;
            Collect();
            if (_sending == null && _time >= _nextSendAt)
                Send();
        }

        /// <summary>
        /// Blocks until the batch on its way is answered, at most <paramref name="milliseconds"/> (for quitting).
        /// </summary>
        /// <returns>True when nothing is left on its way.</returns>
        public bool WaitForSending(int milliseconds)
        {
            var sending = _sending;
            if (sending != null)
            {
                try
                {
                    sending.Wait(milliseconds);
                }
                catch (AggregateException)
                {
                    // Collect reads the failure
                }
            }
            Collect();
            return _sending == null;
        }

        public void Dispose()
        {
            _stop.Cancel();
            _stop.Dispose();
        }

        private void Refresh()
        {
            _identity = new TelemetryIdentity(_options.SchemaVersion, _options.BuildId ?? "", SessionId, _playerId);
            _context = new TelemetryContext(_options.Platform, _mode, _cohort, _variant);
        }

        private void Send()
        {
            if (!_consent || _queue.Count == 0)
            {
                _nextSendAt = _time + _options.BatchSeconds;
                return;
            }
            int count = Math.Min(_queue.Count, _options.MaxBatch);
            var batch = _queue.GetRange(0, count);
            _queue.RemoveRange(0, count);
            _inFlight = batch;
            var transport = _transport;
            var stop = _stop.Token;
            // The batch is frozen (tracked events cannot change), so the worker only reads it
            _sending = Task.Run(() => transport.SendAsync(TelemetryJson.Batch(batch), stop), stop);
        }

        private void Collect()
        {
            if (_sending == null || !_sending.IsCompleted)
                return;
            TelemetryResponse response;
            if (_sending.Status == TaskStatus.RanToCompletion)
                response = _sending.Result;
            else
                response = new TelemetryResponse(0, _sending.Exception?.GetBaseException().Message ?? "cancelled");
            var batch = _inFlight;
            _sending = null;
            _inFlight = null;

            if (response.Succeeded)
            {
                int rejected = CountRejected(response.Body, out string reason);
                Sent += batch.Count;
                Rejected += rejected;
                if (rejected > 0)
                    LastError = $"{rejected} rejected: {reason}";
                _failures = 0;
                // After a full batch the rest of a long queue goes on at once
                _nextSendAt = batch.Count >= _options.MaxBatch && _queue.Count > 0 ? _time : _time + _options.BatchSeconds;
                return;
            }
            LastError = $"{response.Status}: {Shorten(response.Body)}";
            if (response.Retry && _consent)
            {
                _queue.InsertRange(0, batch);
                while (_queue.Count > _options.MaxQueue)
                {
                    _queue.RemoveAt(_queue.Count - 1);
                    Dropped++;
                }
                _failures++;
                double pause = _options.RetrySeconds * Math.Pow(2, Math.Min(_failures - 1, 16));
                _nextSendAt = _time + (float)Math.Min(_options.MaxRetrySeconds, pause);
                return;
            }
            Dropped += batch.Count;
            _nextSendAt = _time + _options.BatchSeconds;
        }

        private static int CountRejected(string body, out string reason)
        {
            reason = null;
            if (string.IsNullOrEmpty(body))
                return 0;
            var list = RejectedList.Match(body);
            if (!list.Success)
                return 0;
            var first = FirstReason.Match(list.Groups[1].Value);
            reason = first.Success ? first.Groups[1].Value : "";
            return RejectedItem.Matches(list.Groups[1].Value).Count;
        }

        private static string Shorten(string text) => text == null || text.Length <= 200 ? text : text.Substring(0, 200);
    }
}
