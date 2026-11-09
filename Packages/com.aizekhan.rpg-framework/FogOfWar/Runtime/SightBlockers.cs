using System;

namespace RPGFramework.FogOfWar
{
    /// <summary>
    /// What stops sight on a grid: walls, closed doors, thick trees. It is not what stops walking (a window or a chasm
    /// can be seen across, a curtain can be walked through), so a game keeps it apart from its navigation graph or
    /// builds it from one: <c>SightBlockers.From((x, y) =&gt; !graph.IsWalkable(new Position2D(x, y)))</c>.
    /// Cells outside the grid are asked too; answer true to keep sight inside it.
    /// </summary>
    public interface ISightBlockers
    {
        bool BlocksSight(int x, int y);
    }

    /// <summary>
    /// A grid of cells that block sight (none at first), or the game's own rule (<see cref="From"/>). Cells outside
    /// the grid block. After a change tell the fog: <see cref="FogOfWarMap.BlockersChanged(GridPoint)"/>.
    /// </summary>
    public sealed class SightBlockers : ISightBlockers
    {
        private readonly bool[] _blocks;

        public SightBlockers(int width, int height)
        {
            if (width <= 0)
                throw new ArgumentOutOfRangeException(nameof(width), "Width must be positive");
            if (height <= 0)
                throw new ArgumentOutOfRangeException(nameof(height), "Height must be positive");
            Width = width;
            Height = height;
            _blocks = new bool[width * height];
        }

        public int Width { get; }
        public int Height { get; }

        public void Set(int x, int y, bool blocks)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height)
                throw new ArgumentOutOfRangeException($"Cell ({x}, {y}) is outside the {Width}x{Height} grid");
            _blocks[y * Width + x] = blocks;
        }

        public bool BlocksSight(int x, int y) => x < 0 || x >= Width || y < 0 || y >= Height || _blocks[y * Width + x];

        /// <summary>
        /// The game's own rule, asked for every cell a sight passes (keep it cheap).
        /// </summary>
        public static ISightBlockers From(Func<int, int, bool> blocks) =>
            new RuleBlockers(blocks ?? throw new ArgumentNullException(nameof(blocks)));

        /// <summary>
        /// Nothing blocks sight: the shape and radius alone decide.
        /// </summary>
        public static ISightBlockers None { get; } = new RuleBlockers((x, y) => false);

        private sealed class RuleBlockers : ISightBlockers
        {
            private readonly Func<int, int, bool> _blocks;

            public RuleBlockers(Func<int, int, bool> blocks) => _blocks = blocks;

            public bool BlocksSight(int x, int y) => _blocks(x, y);
        }
    }
}
