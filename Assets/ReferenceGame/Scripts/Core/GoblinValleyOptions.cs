using System;

namespace ReferenceGame
{
    /// <summary>
    /// Tunable rules of Goblin Valley. Serializable so a designer edits them in the inspector
    /// (see <c>GoblinValleyConfig</c>); tests build them in code.
    /// </summary>
    [Serializable]
    public sealed class GoblinValleyOptions
    {
        /// <summary>
        /// Fixed random seed for loot rolls; 0 picks a new seed every run.
        /// </summary>
        public int Seed;

        public bool WavesEnabled = true;

        /// <summary>
        /// Seconds from the start of the game to the first wave.
        /// </summary>
        public float FirstWaveDelay = 1f;

        /// <summary>
        /// Seconds from the last goblin of a wave dying to the next wave.
        /// </summary>
        public float DelayBetweenWaves = 3f;

        public int FirstWaveSize = 2;

        public int GoblinsAddedPerWave = 1;

        public int MaxWaveSize = 8;

        public int WaveSize(int wave) =>
            Math.Max(1, Math.Min(MaxWaveSize, FirstWaveSize + GoblinsAddedPerWave * Math.Max(0, wave - 1)));
    }
}
