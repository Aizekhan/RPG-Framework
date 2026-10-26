using NUnit.Framework;
using System;

namespace RPGFramework.Pathfinding.Tests
{
    [TestFixture]
    public class Position2DTests
    {
        [Test]
        public void Constructor_SetsXAndY()
        {
            // Arrange & Act
            var position = new Position2D(5.0f, 10.0f);

            // Assert
            Assert.AreEqual(5.0f, position.X);
            Assert.AreEqual(10.0f, position.Y);
        }

        [Test]
        public void ManhattanDistance_CalculatesCorrectly()
        {
            // Arrange
            var pos1 = new Position2D(0, 0);
            var pos2 = new Position2D(3, 4);

            // Act
            float distance = pos1.ManhattanDistance(pos2);

            // Assert
            Assert.AreEqual(7.0f, distance); // |3-0| + |4-0| = 7
        }

        [Test]
        public void EuclideanDistance_CalculatesCorrectly()
        {
            // Arrange
            var pos1 = new Position2D(0, 0);
            var pos2 = new Position2D(3, 4);

            // Act
            float distance = pos1.EuclideanDistance(pos2);

            // Assert
            Assert.AreEqual(5.0f, distance, 0.001f); // sqrt(3^2 + 4^2) = 5
        }

        [Test]
        public void SquaredDistance_CalculatesCorrectly()
        {
            // Arrange
            var pos1 = new Position2D(0, 0);
            var pos2 = new Position2D(3, 4);

            // Act
            float squaredDistance = pos1.SquaredDistance(pos2);

            // Assert
            Assert.AreEqual(25.0f, squaredDistance); // 3^2 + 4^2 = 25
        }

        [Test]
        public void Equals_SamePosition_ReturnsTrue()
        {
            // Arrange
            var pos1 = new Position2D(5, 10);
            var pos2 = new Position2D(5, 10);

            // Act & Assert
            Assert.IsTrue(pos1.Equals(pos2));
            Assert.IsTrue(pos1 == pos2);
        }

        [Test]
        public void Equals_DifferentPosition_ReturnsFalse()
        {
            // Arrange
            var pos1 = new Position2D(5, 10);
            var pos2 = new Position2D(6, 10);

            // Act & Assert
            Assert.IsFalse(pos1.Equals(pos2));
            Assert.IsTrue(pos1 != pos2);
        }

        [Test]
        public void GetHashCode_SamePosition_ReturnsSameHash()
        {
            // Arrange
            var pos1 = new Position2D(5, 10);
            var pos2 = new Position2D(5, 10);

            // Act
            int hash1 = pos1.GetHashCode();
            int hash2 = pos2.GetHashCode();

            // Assert
            Assert.AreEqual(hash1, hash2);
        }

        [Test]
        public void ToString_FormatsCorrectly()
        {
            // Arrange
            var position = new Position2D(5.5f, 10.25f);

            // Act
            string result = position.ToString();

            // Assert
            Assert.AreEqual("(5.50, 10.25)", result);
        }
    }
}
