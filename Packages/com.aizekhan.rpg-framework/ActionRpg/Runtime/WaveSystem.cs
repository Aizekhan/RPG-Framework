using System;
using System.Linq;
using RPGFramework.Systems;

namespace RPGFramework.ActionRpg
{
    /// <summary>
    /// Starts a new, bigger wave once every enemy is dead and the delay has passed. Waits while the player is dead.
    /// </summary>
    public sealed class WaveSystem : IUpdateSystem
    {
        private readonly ActorWorld _world;
        private float _quietTime;

        public WaveSystem(ActorWorld world)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
        }

        public void Initialize()
        {
        }

        public void Update(float deltaTime)
        {
            var waves = _world.Settings.Waves;
            if (waves == null || !waves.Enabled || !_world.IsStarted || !_world.Player.IsAlive())
                return;
            if (_world.LivingEnemies.Any())
            {
                _quietTime = 0f;
                return;
            }

            _quietTime += deltaTime;
            float wait = _world.CurrentWave == 0 ? waves.FirstWaveDelay : waves.DelayBetweenWaves;
            if (_quietTime < wait)
                return;

            _quietTime = 0f;
            _world.StartWave(waves.WaveSize(_world.CurrentWave + 1));
        }

        public void Dispose()
        {
        }
    }
}
