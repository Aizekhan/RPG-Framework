using System;

namespace RPGFramework.ActionRpg
{
    /// <summary>
    /// Waves of enemies: a new wave starts once every enemy is dead and the delay has passed, each one bigger.
    /// </summary>
    [Serializable]
    public sealed class WaveSettings
    {
        public bool Enabled = true;

        /// <summary>
        /// Seconds from the start of the game to the first wave.
        /// </summary>
        public float FirstWaveDelay = 1f;

        /// <summary>
        /// Seconds from the last enemy of a wave dying to the next wave.
        /// </summary>
        public float DelayBetweenWaves = 3f;

        public int FirstWaveSize = 2;
        public int AddedPerWave = 1;
        public int MaxWaveSize = 8;

        public int WaveSize(int wave) =>
            Math.Max(1, Math.Min(MaxWaveSize, FirstWaveSize + AddedPerWave * Math.Max(0, wave - 1)));
    }
}
