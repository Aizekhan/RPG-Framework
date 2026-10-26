using System;
using RPGFramework.Core.Composition;
using RPGFramework.Logging.Unity;
using UnityEngine;
using ILogger = RPGFramework.Logging.ILogger;

namespace RPGFramework.Bootstrap.Unity
{
    /// <summary>
    /// Scene entry point: derive per game, add modules in <see cref="ConfigureModules"/>, put it on one
    /// GameObject in the first scene. Drives <see cref="GameHost"/> from Unity's player loop.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public abstract class GameHostBehaviour : MonoBehaviour
    {
        [SerializeField] private bool _dontDestroyOnLoad = true;
        [SerializeField] private string _worldName = "Main";
        [SerializeField] private string _initialPhaseId = "";
        [SerializeField] private string _logFilePrefix = "game";
        [SerializeField] private int _maxConsecutiveSystemErrors;

        private ILogger _logger;

        public GameHost Host { get; private set; }

        protected abstract void ConfigureModules(IGameCompositionBuilder builder);

        /// <summary>
        /// Console + rotating file logger by default; override to customise sinks or levels.
        /// </summary>
        protected virtual ILogger CreateLogger() => UnityLogging.CreateDefault(_logFilePrefix);

        protected virtual void Awake()
        {
            if (_dontDestroyOnLoad)
                DontDestroyOnLoad(gameObject);

            _logger = CreateLogger();
            StartHost();
        }

        /// <summary>
        /// Shuts the running game down and starts a new one from <see cref="ConfigureModules"/> (a game switching
        /// modes, such as joining someone else's game, or a "new game" button). Not from inside the host's own update.
        /// </summary>
        protected void Restart()
        {
            Host?.Dispose();
            enabled = true;
            StartHost();
        }

        private void StartHost()
        {
            Host = new GameHost(_logger, new FrameworkCoreOptions
            {
                WorldName = _worldName,
                InitialPhaseId = string.IsNullOrEmpty(_initialPhaseId) ? null : _initialPhaseId,
                MaxConsecutiveSystemErrors = _maxConsecutiveSystemErrors
            });

            try
            {
                Host.Start(ConfigureModules);
            }
            catch (Exception)
            {
                // Already logged as critical by GameHost; stop ticking a half-built game
                enabled = false;
            }
        }

        protected virtual void Update() => Host?.Update(UnityEngine.Time.deltaTime);

        protected virtual void FixedUpdate() => Host?.FixedUpdate(UnityEngine.Time.fixedDeltaTime);

        protected virtual void LateUpdate() => Host?.LateUpdate(UnityEngine.Time.deltaTime);

        protected virtual void OnDestroy()
        {
            Host?.Dispose();
            (_logger as IDisposable)?.Dispose();
        }
    }
}
