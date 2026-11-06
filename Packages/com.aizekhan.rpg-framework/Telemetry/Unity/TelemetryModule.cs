using System;
using RPGFramework.Bootstrap;
using UnityEngine;

namespace RPGFramework.Telemetry.Unity
{
    /// <summary>
    /// Registers <see cref="ITelemetry"/>: a <see cref="TelemetryClient"/> posting to the game's collector
    /// (<see cref="HttpTelemetryTransport"/>, or the game's own transport), driven every real-time frame by a
    /// <see cref="TelemetryDriver"/> that also sends on pause and ends the session on quit. The player's consent is
    /// kept in PlayerPrefs (<see cref="ConsentKey"/>) and is off until the game asks and the player agrees. The build
    /// id defaults to <c>Application.version</c>, the platform to <c>Application.platform</c>. A service registered
    /// before this module configures is kept.
    /// </summary>
    public sealed class TelemetryModule : GameFeatureModule
    {
        public const string ModuleName = "Framework.Telemetry";
        public const string ConsentKey = "RPGFramework.Telemetry.Consent";

        private readonly string _endpoint;
        private readonly string _key;
        private readonly TelemetryOptions _options;
        private readonly ITelemetryTransport _transport;
        private TelemetryClient _client;
        private TelemetryDriver _driver;
        private HttpTelemetryTransport _http;

        /// <param name="endpoint">The collector's address; ignored when <paramref name="transport"/> is given.</param>
        /// <param name="key">Its write-only key for this game (sent as a bearer token).</param>
        public TelemetryModule(string endpoint, string key, TelemetryOptions options = null, ITelemetryTransport transport = null)
        {
            if (transport == null && string.IsNullOrEmpty(endpoint))
                throw new ArgumentException("A collector address or a transport", nameof(endpoint));
            _endpoint = endpoint;
            _key = key;
            _options = options ?? new TelemetryOptions();
            _transport = transport;
        }

        public override string Name => ModuleName;

        protected override void OnConfigure()
        {
            if (!Container.IsRegistered<ITelemetry>())
                Container.RegisterFactory<ITelemetry>(_ => Create());
        }

        protected override void OnShutdown()
        {
            if (_driver != null)
                UnityEngine.Object.Destroy(_driver.gameObject);
            _client?.Dispose();
            _http?.Dispose();
            _driver = null;
            _client = null;
            _http = null;
        }

        private ITelemetry Create()
        {
            if (string.IsNullOrEmpty(_options.BuildId))
                _options.BuildId = Application.version;
            if (string.IsNullOrEmpty(_options.Platform))
                _options.Platform = Application.platform.ToString();
            var transport = _transport;
            if (transport == null)
            {
                _http = new HttpTelemetryTransport(_endpoint, _key);
                transport = _http;
            }
            _client = new TelemetryClient(transport, _options) { Consent = PlayerPrefs.GetInt(ConsentKey, 0) == 1 };
            _client.ConsentChanged += consent =>
            {
                PlayerPrefs.SetInt(ConsentKey, consent ? 1 : 0);
                PlayerPrefs.Save();
            };
            var host = new GameObject("[Telemetry]");
            UnityEngine.Object.DontDestroyOnLoad(host);
            _driver = host.AddComponent<TelemetryDriver>();
            _driver.Client = _client;
            _client.StartSession();
            return _client;
        }
    }
}
