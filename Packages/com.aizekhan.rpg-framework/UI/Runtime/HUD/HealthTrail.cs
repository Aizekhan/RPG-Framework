using System;
using System.Collections.Generic;

namespace RPGFramework.UI.HUD
{
    /// <summary>
    /// The delayed chunk of health bars: after a hit the lost part stays on the bar for <see cref="Hold"/> seconds
    /// (drawn light, under the health), then drains down to the health at <see cref="DrainPerSecond"/>, so the eye sees
    /// how much the blow took. A new blow holds it whole again; healing snaps it to the health. One trail per bar id
    /// (the entity it shows), so one instance serves every bar over many characters. Read with the time now: a bar
    /// drawn twice in a frame drains once. Plain C#: tests drive the time.
    /// </summary>
    public sealed class HealthTrail
    {
        private struct Bar
        {
            public float Shown;
            public float Health;
            public float HoldUntil;
            public float Time;
        }

        private readonly Dictionary<int, Bar> _bars = new Dictionary<int, Bar>();

        /// <param name="hold">Seconds the lost chunk stays whole after a hit.</param>
        /// <param name="drainPerSecond">How fast it then drains: a share of the whole bar a second.</param>
        public HealthTrail(float hold = 0.45f, float drainPerSecond = 0.8f)
        {
            Hold = Math.Max(0f, hold);
            DrainPerSecond = Math.Max(0.01f, drainPerSecond);
        }

        public float Hold { get; }

        public float DrainPerSecond { get; }

        /// <summary>
        /// Bars the trail keeps.
        /// </summary>
        public int Count => _bars.Count;

        /// <summary>
        /// Where the trail ends on this bar now (0..1, never below the health): draw it under the health's fill.
        /// </summary>
        /// <param name="id">The bar's own id (the entity it shows).</param>
        /// <param name="health">The health now, 0 to 1.</param>
        /// <param name="now">The time now in seconds (unscaled, so a slowed or paused game still drains).</param>
        public float Update(int id, float health, float now)
        {
            health = Math.Max(0f, Math.Min(1f, health));
            if (!_bars.TryGetValue(id, out var bar) || health >= bar.Shown)
            {
                _bars[id] = new Bar { Shown = health, Health = health, HoldUntil = now, Time = now };
                return health;
            }
            // What was draining drains up to now; then a new blow holds what is left whole again
            if (bar.Shown > bar.Health && now > bar.HoldUntil)
            {
                float from = Math.Max(bar.Time, bar.HoldUntil);
                bar.Shown = Math.Max(health, bar.Shown - DrainPerSecond * (now - from));
            }
            if (health < bar.Health)
                bar.HoldUntil = now + Hold;
            bar.Health = health;
            bar.Time = now;
            _bars[id] = bar;
            return bar.Shown;
        }

        /// <summary>
        /// The chunk shown on the bar: the trail's end less the health (0 when nothing is draining).
        /// </summary>
        public float ChunkOf(int id) => _bars.TryGetValue(id, out var bar) ? Math.Max(0f, bar.Shown - bar.Health) : 0f;

        /// <summary>
        /// Lets one bar go (its character is gone).
        /// </summary>
        public bool Forget(int id) => _bars.Remove(id);

        /// <summary>
        /// Lets every bar go (a level ended).
        /// </summary>
        public void Clear() => _bars.Clear();
    }
}
