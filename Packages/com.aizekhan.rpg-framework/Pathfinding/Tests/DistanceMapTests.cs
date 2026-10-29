using NUnit.Framework;

namespace RPGFramework.Pathfinding.Tests
{
    /// <summary>
    /// A distance map: distances from the nearest of several goals around walls, steps towards and away, the whole
    /// way, a search kept to a neighbourhood, places that cannot be reached.
    /// </summary>
    public sealed class DistanceMapTests
    {
        // 5 x 5, a wall down x = 2 except at y = 4
        private static GridNavigationGraph Grid()
        {
            var grid = new GridNavigationGraph(5, 5);
            for (int y = 0; y < 4; y++)
                grid.SetWalkable(2, y, false);
            return grid;
        }

        private static Position2D P(int x, int y) => new Position2D(x, y);

        [Test]
        public void Distances_GoAroundWalls_AndChasersStepCloser()
        {
            var map = new DistanceMap(Grid());
            map.Build(P(0, 0));

            Assert.That(map.DistanceTo(P(0, 0)), Is.EqualTo(0f));
            Assert.That(map.DistanceTo(P(3, 0)), Is.EqualTo(11f), "round the wall's end at y = 4");
            Assert.That(map.DistanceTo(P(2, 0)), Is.EqualTo(float.PositiveInfinity), "a wall");
            Assert.That(map.Count, Is.EqualTo(21));

            Assert.That(map.NextStep(P(3, 0), out var step), Is.True);
            Assert.That(map.DistanceTo(step), Is.EqualTo(10f));
            Assert.That(map.NextStep(P(0, 0), out _), Is.False, "on the goal");

            var path = map.PathFrom(P(3, 0));
            Assert.That(path.Count, Is.EqualTo(12));
            Assert.That(path[path.Count - 1], Is.EqualTo(P(0, 0)));

            Assert.That(map.StepAway(P(1, 0), out var away), Is.True);
            Assert.That(map.DistanceTo(away), Is.EqualTo(2f));
        }

        [Test]
        public void SeveralGoals_TheNearestCounts_AndASearchCanStayNear()
        {
            var map = new DistanceMap(Grid());
            map.Build(new[] { P(0, 0), P(4, 0) });
            Assert.That(map.DistanceTo(P(3, 0)), Is.EqualTo(1f));
            Assert.That(map.DistanceTo(P(1, 1)), Is.EqualTo(2f));

            map.Build(P(0, 0), maxCost: 2f);
            Assert.That(map.IsReached(P(1, 1)), Is.True);
            Assert.That(map.IsReached(P(0, 3)), Is.False, "further than the limit");
            Assert.That(map.NextStep(P(0, 3), out _), Is.False);
            Assert.That(map.PathFrom(P(0, 3)), Is.Empty);
        }
    }
}
