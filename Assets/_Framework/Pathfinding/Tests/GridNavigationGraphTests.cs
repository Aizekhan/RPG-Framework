using NUnit.Framework;
using System;

namespace RPGFramework.Pathfinding.Tests
{
    [TestFixture]
    public class GridNavigationGraphTests
    {
        [Test]
        public void Constructor_InvalidDimensions_ThrowsArgumentException()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => new GridNavigationGraph(0, 5));
            Assert.Throws<ArgumentException>(() => new GridNavigationGraph(5, 0));
            Assert.Throws<ArgumentException>(() => new GridNavigationGraph(-1, 5));
        }

        [Test]
        public void IsWalkable_AllCellsInitiallyWalkable()
        {
            // Arrange
            var grid = new GridNavigationGraph(3, 3);

            // Act & Assert
            for (int x = 0; x < 3; x++)
            {
                for (int y = 0; y < 3; y++)
                {
                    Assert.IsTrue(grid.IsWalkable(new Position2D(x, y)));
                }
            }
        }

        [Test]
        public void SetWalkable_UpdatesCellState()
        {
            // Arrange
            var grid = new GridNavigationGraph(3, 3);
            var position = new Position2D(1, 1);

            // Act
            grid.SetWalkable(1, 1, false);

            // Assert
            Assert.IsFalse(grid.IsWalkable(position));
        }

        [Test]
        public void SetWalkable_OutOfBounds_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            var grid = new GridNavigationGraph(3, 3);

            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => grid.SetWalkable(-1, 1, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => grid.SetWalkable(1, -1, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => grid.SetWalkable(3, 1, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => grid.SetWalkable(1, 3, false));
        }

        [Test]
        public void IsWalkable_OutOfBounds_ReturnsFalse()
        {
            // Arrange
            var grid = new GridNavigationGraph(3, 3);

            // Act & Assert
            Assert.IsFalse(grid.IsWalkable(new Position2D(-1, 0)));
            Assert.IsFalse(grid.IsWalkable(new Position2D(0, -1)));
            Assert.IsFalse(grid.IsWalkable(new Position2D(3, 0)));
            Assert.IsFalse(grid.IsWalkable(new Position2D(0, 3)));
        }

        [Test]
        public void GetNeighbors_CenterCell_ReturnsFourNeighbors()
        {
            // Arrange
            var grid = new GridNavigationGraph(3, 3, allowDiagonal: false);
            var center = new Position2D(1, 1);

            // Act
            var neighbors = grid.GetNeighbors(center);

            // Assert
            Assert.AreEqual(4, neighbors.Count);
        }

        [Test]
        public void GetNeighbors_CornerCell_ReturnsTwoNeighbors()
        {
            // Arrange
            var grid = new GridNavigationGraph(3, 3, allowDiagonal: false);
            var corner = new Position2D(0, 0);

            // Act
            var neighbors = grid.GetNeighbors(corner);

            // Assert
            Assert.AreEqual(2, neighbors.Count);
        }

        [Test]
        public void GetNeighbors_WithDiagonal_ReturnsEightNeighbors()
        {
            // Arrange
            var grid = new GridNavigationGraph(3, 3, allowDiagonal: true);
            var center = new Position2D(1, 1);

            // Act
            var neighbors = grid.GetNeighbors(center);

            // Assert
            Assert.AreEqual(8, neighbors.Count);
        }

        [Test]
        public void GetNeighbors_SkipsUnwalkableNeighbors()
        {
            // Arrange
            var grid = new GridNavigationGraph(3, 3, allowDiagonal: false);
            var center = new Position2D(1, 1);

            // Block two neighbors
            grid.SetWalkable(1, 0, false); // Up
            grid.SetWalkable(2, 1, false); // Right

            // Act
            var neighbors = grid.GetNeighbors(center);

            // Assert
            Assert.AreEqual(2, neighbors.Count);
        }

        [Test]
        public void GetCost_CardinalDirection_ReturnsOne()
        {
            // Arrange
            var grid = new GridNavigationGraph(3, 3, allowDiagonal: false);
            var from = new Position2D(1, 1);
            var to = new Position2D(1, 2);

            // Act
            float cost = grid.GetCost(from, to);

            // Assert
            Assert.AreEqual(1.0f, cost);
        }

        [Test]
        public void GetCost_DiagonalDirection_ReturnsDiagonalCost()
        {
            // Arrange
            var grid = new GridNavigationGraph(3, 3, allowDiagonal: true, diagonalCost: 1.414f);
            var from = new Position2D(1, 1);
            var to = new Position2D(2, 2);

            // Act
            float cost = grid.GetCost(from, to);

            // Assert
            Assert.AreEqual(1.414f, cost, 0.001f);
        }

        [Test]
        public void GetNeighbors_OutOfBounds_ReturnsEmptyList()
        {
            // Arrange
            var grid = new GridNavigationGraph(3, 3);
            var outOfBounds = new Position2D(-1, 0);

            // Act
            var neighbors = grid.GetNeighbors(outOfBounds);

            // Assert
            Assert.AreEqual(0, neighbors.Count);
        }
    }
}
