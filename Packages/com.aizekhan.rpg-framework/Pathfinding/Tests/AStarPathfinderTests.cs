using NUnit.Framework;
using System.Linq;

namespace RPGFramework.Pathfinding.Tests
{
    [TestFixture]
    public class AStarPathfinderTests
    {
        private GridNavigationGraph _grid;
        private AStarPathfinder _pathfinder;

        [SetUp]
        public void Setup()
        {
            // Create 5x5 grid
            _grid = new GridNavigationGraph(5, 5, allowDiagonal: false);
            _pathfinder = new AStarPathfinder(_grid);
        }

        [Test]
        public void FindPath_StraightLine_ReturnsDirectPath()
        {
            // Arrange
            var start = new Position2D(0, 0);
            var goal = new Position2D(4, 0);

            // Act
            var path = _pathfinder.FindPath(start, goal);

            // Assert
            Assert.AreEqual(5, path.Count);
            Assert.AreEqual(start, path[0]);
            Assert.AreEqual(goal, path[4]);
        }

        [Test]
        public void FindPath_WithObstacle_FindsAlternatePath()
        {
            // Arrange
            var start = new Position2D(0, 0);
            var goal = new Position2D(4, 0);

            // Create vertical wall at x=2
            _grid.SetWalkable(2, 0, false);
            _grid.SetWalkable(2, 1, false);
            _grid.SetWalkable(2, 2, false);

            // Act
            var path = _pathfinder.FindPath(start, goal);

            // Assert
            Assert.Greater(path.Count, 5); // Path is longer than direct route
            Assert.AreEqual(start, path[0]);
            Assert.AreEqual(goal, path[path.Count - 1]);

            // Path should go around obstacle
            Assert.IsTrue(path.All(p => p.X != 2 || p.Y > 2)); // No path through wall
        }

        [Test]
        public void FindPath_NoPathExists_ReturnsEmptyList()
        {
            // Arrange
            var start = new Position2D(0, 0);
            var goal = new Position2D(4, 0);

            // Create complete wall blocking path
            for (int y = 0; y < 5; y++)
            {
                _grid.SetWalkable(2, y, false);
            }

            // Act
            var path = _pathfinder.FindPath(start, goal);

            // Assert
            Assert.AreEqual(0, path.Count);
        }

        [Test]
        public void FindPath_StartEqualsGoal_ReturnsSinglePosition()
        {
            // Arrange
            var position = new Position2D(2, 2);

            // Act
            var path = _pathfinder.FindPath(position, position);

            // Assert
            Assert.AreEqual(1, path.Count);
            Assert.AreEqual(position, path[0]);
        }

        [Test]
        public void FindPath_StartNotWalkable_ReturnsEmptyList()
        {
            // Arrange
            var start = new Position2D(0, 0);
            var goal = new Position2D(4, 0);
            _grid.SetWalkable(0, 0, false);

            // Act
            var path = _pathfinder.FindPath(start, goal);

            // Assert
            Assert.AreEqual(0, path.Count);
        }

        [Test]
        public void FindPath_GoalNotWalkable_ReturnsEmptyList()
        {
            // Arrange
            var start = new Position2D(0, 0);
            var goal = new Position2D(4, 0);
            _grid.SetWalkable(4, 0, false);

            // Act
            var path = _pathfinder.FindPath(start, goal);

            // Assert
            Assert.AreEqual(0, path.Count);
        }

        [Test]
        public void FindPath_ComplexMaze_FindsOptimalPath()
        {
            // Arrange
            var start = new Position2D(0, 0);
            var goal = new Position2D(4, 4);

            // Create maze
            _grid.SetWalkable(1, 0, false);
            _grid.SetWalkable(1, 1, false);
            _grid.SetWalkable(3, 1, false);
            _grid.SetWalkable(3, 2, false);
            _grid.SetWalkable(3, 3, false);
            _grid.SetWalkable(1, 3, false);

            // Act
            var path = _pathfinder.FindPath(start, goal);

            // Assert
            Assert.Greater(path.Count, 0);
            Assert.AreEqual(start, path[0]);
            Assert.AreEqual(goal, path[path.Count - 1]);
        }

        [Test]
        public void HasPath_PathExists_ReturnsTrue()
        {
            // Arrange
            var start = new Position2D(0, 0);
            var goal = new Position2D(4, 4);

            // Act
            bool hasPath = _pathfinder.HasPath(start, goal);

            // Assert
            Assert.IsTrue(hasPath);
        }

        [Test]
        public void HasPath_NoPathExists_ReturnsFalse()
        {
            // Arrange
            var start = new Position2D(0, 0);
            var goal = new Position2D(4, 0);

            // Block path
            for (int y = 0; y < 5; y++)
            {
                _grid.SetWalkable(2, y, false);
            }

            // Act
            bool hasPath = _pathfinder.HasPath(start, goal);

            // Assert
            Assert.IsFalse(hasPath);
        }

        [Test]
        public void FindPath_WithDiagonalMovement_UsesShortestPath()
        {
            // Arrange
            var gridDiagonal = new GridNavigationGraph(5, 5, allowDiagonal: true);
            var pathfinderDiagonal = new AStarPathfinder(gridDiagonal);
            var start = new Position2D(0, 0);
            var goal = new Position2D(4, 4);

            // Act
            var path = pathfinderDiagonal.FindPath(start, goal);

            // Assert
            Assert.AreEqual(5, path.Count); // Diagonal path: (0,0) → (1,1) → (2,2) → (3,3) → (4,4)
            Assert.AreEqual(start, path[0]);
            Assert.AreEqual(goal, path[4]);
        }

        [Test]
        public void FindPath_ManhattanHeuristic_FindsPath()
        {
            // Arrange
            var pathfinderManhattan = new AStarPathfinder(_grid, (a, b) => a.ManhattanDistance(b));
            var start = new Position2D(0, 0);
            var goal = new Position2D(4, 4);

            // Act
            var path = pathfinderManhattan.FindPath(start, goal);

            // Assert
            Assert.Greater(path.Count, 0);
            Assert.AreEqual(start, path[0]);
            Assert.AreEqual(goal, path[path.Count - 1]);
        }
    }
}
