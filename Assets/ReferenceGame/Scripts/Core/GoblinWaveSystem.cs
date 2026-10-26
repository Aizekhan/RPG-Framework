using System;
using System.Linq;
using RPGFramework.Systems;

namespace ReferenceGame
{
    /// <summary>
    /// Starts a new, bigger wave of goblins once the valley has been quiet long enough. Waits while the hero is dead.
    /// </summary>
    public sealed class GoblinWaveSystem : IUpdateSystem
    {
        private readonly GoblinValleyGame _game;
        private readonly GoblinValleyOptions _options;
        private float _quietTime;

        public GoblinWaveSystem(GoblinValleyGame game, GoblinValleyOptions options)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public void Initialize()
        {
        }

        public void Update(float deltaTime)
        {
            if (!_options.WavesEnabled || !_game.IsStarted || !_game.Player.IsAlive())
                return;
            if (_game.LivingGoblins.Any())
            {
                _quietTime = 0f;
                return;
            }

            _quietTime += deltaTime;
            float wait = _game.CurrentWave == 0 ? _options.FirstWaveDelay : _options.DelayBetweenWaves;
            if (_quietTime < wait)
                return;

            _quietTime = 0f;
            _game.StartWave(_options.WaveSize(_game.CurrentWave + 1));
        }

        public void Dispose()
        {
        }
    }
}
