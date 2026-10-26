using System.Collections.Generic;

namespace RPGFramework.Pathfinding
{
    /// <summary>
    /// Core pathfinding interface.
    /// Framework contract with no engine or game dependencies.
    /// </summary>
    public interface IPathfinder
    {
        /// <summary>
        /// Find a path from start to goal position.
        /// </summary>
        /// <param name="start">Starting position</param>
        /// <param name="goal">Goal position</param>
        /// <returns>List of positions representing the path, or empty list if no path exists</returns>
        IReadOnlyList<Position2D> FindPath(Position2D start, Position2D goal);

        /// <summary>
        /// Check if a path exists between start and goal.
        /// More efficient than FindPath when only existence check is needed.
        /// </summary>
        /// <param name="start">Starting position</param>
        /// <param name="goal">Goal position</param>
        /// <returns>True if a path exists</returns>
        bool HasPath(Position2D start, Position2D goal);
    }
}
