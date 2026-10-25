using System.Collections.Generic;

namespace RPGFramework.Pathfinding
{
    /// <summary>
    /// Navigation graph interface for pathfinding.
    /// Represents the walkable space and neighbor relationships.
    /// </summary>
    public interface INavigationGraph
    {
        /// <summary>
        /// Get all neighbors of a given position.
        /// </summary>
        /// <param name="position">Current position</param>
        /// <returns>List of reachable neighbor positions</returns>
        IReadOnlyList<Position2D> GetNeighbors(Position2D position);

        /// <summary>
        /// Get the movement cost from one position to a neighbor.
        /// </summary>
        /// <param name="from">Starting position</param>
        /// <param name="to">Neighbor position</param>
        /// <returns>Movement cost (typically 1.0 for uniform cost, or distance)</returns>
        float GetCost(Position2D from, Position2D to);

        /// <summary>
        /// Check if a position is walkable.
        /// </summary>
        /// <param name="position">Position to check</param>
        /// <returns>True if walkable</returns>
        bool IsWalkable(Position2D position);
    }
}
