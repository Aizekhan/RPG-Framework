using System;

namespace RPGFramework.FogOfWar
{
    /// <summary>
    /// Lays a fog grid over a continuous world: the ground plane (x and z in 3D, x and y in 2D) from a corner, in
    /// square cells of a size. Free-moving units then see on the grid, as fog works in real-time strategy and top-down
    /// action games; a cell of 1 to 2 metres is usual.
    /// </summary>
    public sealed class FogGridSpace
    {
        /// <param name="originX">World x of the grid's corner (cell 0, 0).</param>
        /// <param name="originY">World z (3D) or y (2D) of the grid's corner.</param>
        public FogGridSpace(float originX, float originY, float cellSize, int width, int height)
        {
            if (!(cellSize > 0f))
                throw new ArgumentOutOfRangeException(nameof(cellSize), "Cell size must be positive");
            if (width <= 0)
                throw new ArgumentOutOfRangeException(nameof(width), "Width must be positive");
            if (height <= 0)
                throw new ArgumentOutOfRangeException(nameof(height), "Height must be positive");
            OriginX = originX;
            OriginY = originY;
            CellSize = cellSize;
            Width = width;
            Height = height;
        }

        /// <summary>
        /// A grid that covers an area of the world (its two corners), in cells of a size.
        /// </summary>
        public static FogGridSpace Covering(float minX, float minY, float maxX, float maxY, float cellSize)
        {
            if (!(cellSize > 0f))
                throw new ArgumentOutOfRangeException(nameof(cellSize), "Cell size must be positive");
            int width = Math.Max(1, (int)Math.Ceiling((maxX - minX) / cellSize));
            int height = Math.Max(1, (int)Math.Ceiling((maxY - minY) / cellSize));
            return new FogGridSpace(minX, minY, cellSize, width, height);
        }

        public float OriginX { get; }
        public float OriginY { get; }
        public float CellSize { get; }
        public int Width { get; }
        public int Height { get; }

        /// <summary>
        /// The cell a world point falls in (it may be outside the grid: <see cref="Contains"/>).
        /// </summary>
        public GridPoint CellAt(float x, float y) =>
            new GridPoint((int)Math.Floor((x - OriginX) / CellSize), (int)Math.Floor((y - OriginY) / CellSize));

        public bool Contains(GridPoint cell) => cell.X >= 0 && cell.X < Width && cell.Y >= 0 && cell.Y < Height;

        /// <summary>
        /// The world point at a cell's centre.
        /// </summary>
        public void CenterOf(GridPoint cell, out float x, out float y)
        {
            x = OriginX + (cell.X + 0.5f) * CellSize;
            y = OriginY + (cell.Y + 0.5f) * CellSize;
        }

        /// <summary>
        /// A world distance in whole cells, rounded up (a sight radius of 12 m on 2 m cells is 6).
        /// </summary>
        public int CellsIn(float distance) => distance <= 0f ? 0 : (int)Math.Ceiling(distance / CellSize - 1e-4f);
    }
}
