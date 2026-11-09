using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace RPGFramework.FogOfWar.Tests
{
    /// <summary>
    /// Sight on a grid: Bresenham lines, the three shapes, and symmetric shadowcasting (walls seen, what is behind them
    /// hidden, every floor cell seen from a cell it sees).
    /// </summary>
    public class GridSightTests
    {
        private static GridPoint P(int x, int y) => new GridPoint(x, y);

        [Test]
        public void Line_GoesCellByCell_BothEndsIncluded()
        {
            Assert.That(GridSight.Line(P(0, 0), P(4, 2)),
                Is.EqualTo(new[] { P(0, 0), P(1, 1), P(2, 1), P(3, 2), P(4, 2) }));
            Assert.That(GridSight.Line(P(3, 3), P(3, 3)), Is.EqualTo(new[] { P(3, 3) }));
            var into = new List<GridPoint> { P(9, 9) };
            GridSight.Line(P(2, 0), P(0, 0), into);
            Assert.That(into, Is.EqualTo(new[] { P(2, 0), P(1, 0), P(0, 0) }));
        }

        [Test]
        public void HasLine_AWallBetweenBlocks_TheEndsDoNot()
        {
            var walls = new SightBlockers(10, 10);
            walls.Set(5, 5, true);
            Assert.That(GridSight.HasLine(walls, P(2, 5), P(8, 5)), Is.False);
            Assert.That(GridSight.HasLine(walls, P(2, 5), P(5, 5)), Is.True, "the wall itself is seen");
            Assert.That(GridSight.HasLine(walls, P(5, 5), P(8, 5)), Is.True, "an archer on the wall sees out");
            Assert.That(GridSight.HasLine(walls, P(2, 4), P(8, 4)), Is.True);
            Assert.That(GridSight.CanSee(walls, P(2, 4), P(8, 4), 5), Is.False, "out of the radius");
            Assert.That(GridSight.CanSee(walls, P(2, 4), P(7, 4), 5), Is.True);
        }

        [Test]
        public void HasLine_IsTheSameBothWays()
        {
            var random = new Random(7);
            var walls = RandomWalls(random, 20, 20, 0.3);
            for (int i = 0; i < 2000; i++)
            {
                var a = P(random.Next(20), random.Next(20));
                var b = P(random.Next(20), random.Next(20));
                Assert.That(GridSight.HasLine(walls, a, b), Is.EqualTo(GridSight.HasLine(walls, b, a)), $"{a} {b}");
            }
        }

        [Test]
        public void Shapes_CircleSquareDiamond()
        {
            Assert.That(GridSight.InShape(3, 0, 3, SightShape.Circle), Is.True);
            Assert.That(GridSight.InShape(2, 2, 3, SightShape.Circle), Is.True);
            Assert.That(GridSight.InShape(3, 2, 3, SightShape.Circle), Is.False);
            Assert.That(GridSight.InShape(3, 3, 3, SightShape.Square), Is.True);
            Assert.That(GridSight.InShape(4, 0, 3, SightShape.Square), Is.False);
            Assert.That(GridSight.InShape(2, 1, 3, SightShape.Diamond), Is.True);
            Assert.That(GridSight.InShape(2, 2, 3, SightShape.Diamond), Is.False);
            Assert.That(GridSight.InShape(0, 0, -1, SightShape.Circle), Is.False);
        }

        [TestCase(SightShape.Circle)]
        [TestCase(SightShape.Square)]
        [TestCase(SightShape.Diamond)]
        public void OpenGround_SeesTheWholeShape_EachCellOnce_TheEyeFirst(SightShape shape)
        {
            var seen = GridSight.Visible(SightBlockers.None, 30, 30, P(15, 15), 6, shape);
            int expected = 0;
            for (int x = 0; x < 30; x++)
                for (int y = 0; y < 30; y++)
                    if (GridSight.InShape(x - 15, y - 15, 6, shape))
                        expected++;
            Assert.That(seen.Count, Is.EqualTo(expected));
            Assert.That(seen.Distinct().Count(), Is.EqualTo(seen.Count));
            Assert.That(seen[0], Is.EqualTo(P(15, 15)));
        }

        [Test]
        public void TheGridsEdge_CutsTheView()
        {
            var seen = GridSight.Visible(SightBlockers.None, 5, 5, P(0, 0), 10, SightShape.Square);
            Assert.That(seen.Count, Is.EqualTo(25));
            Assert.That(GridSight.Visible(SightBlockers.None, 5, 5, P(-1, 0), 3), Is.Empty, "an eye off the grid");
            Assert.That(GridSight.Visible(SightBlockers.None, 5, 5, P(2, 2), 0), Is.EqualTo(new[] { P(2, 2) }));
        }

        [Test]
        public void AWall_IsSeen_WhatIsBehindItIsNot()
        {
            // A wall across the room at x = 6 with the eye at (2, 5)
            var walls = new SightBlockers(12, 11);
            for (int y = 0; y < 11; y++)
                walls.Set(6, y, true);
            var seen = new HashSet<GridPoint>(GridSight.Visible(walls, 12, 11, P(2, 5), 8));
            Assert.That(seen, Has.Member(P(6, 5)).And.Member(P(6, 3)), "the wall lights up");
            Assert.That(seen.Any(c => c.X > 6), Is.False, "nothing behind it");
            Assert.That(seen, Has.Member(P(5, 1)), "the room in front");
        }

        [Test]
        public void APillar_HidesOnlyItsShadow()
        {
            var walls = new SightBlockers(20, 20);
            walls.Set(10, 7, true);
            var seen = new HashSet<GridPoint>(GridSight.Visible(walls, 20, 20, P(10, 10), 8));
            Assert.That(seen, Has.Member(P(10, 7)));
            Assert.That(seen, Has.No.Member(P(10, 6)).And.Not.Member(P(10, 4)), "straight behind it");
            Assert.That(seen, Has.Member(P(8, 5)).And.Member(P(12, 5)), "beside the shadow");
            Assert.That(seen, Has.Member(P(9, 8)).And.Member(P(11, 8)), "beside the pillar");
        }

        [Test]
        public void ThroughWalls_SeesTheWholeShape()
        {
            var walls = RandomWalls(new Random(3), 20, 20, 0.5);
            var cells = new List<GridPoint>();
            new FieldOfView(20, 20).Compute(walls, P(10, 10), 4, SightShape.Diamond, cells, throughWalls: true);
            Assert.That(cells.Count, Is.EqualTo(41));
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void FloorCells_SeeEachOtherBothWays(int seed)
        {
            const int Size = 24, Radius = 8;
            var random = new Random(seed);
            var walls = RandomWalls(random, Size, Size, 0.25);
            var fieldOfView = new FieldOfView(Size, Size);
            var sees = new Dictionary<GridPoint, HashSet<GridPoint>>();
            var cells = new List<GridPoint>();
            for (int x = 0; x < Size; x++)
            {
                for (int y = 0; y < Size; y++)
                {
                    if (walls.BlocksSight(x, y))
                        continue;
                    fieldOfView.Compute(walls, P(x, y), Radius, SightShape.Circle, cells);
                    sees[P(x, y)] = new HashSet<GridPoint>(cells);
                }
            }
            int pairs = 0;
            foreach (var pair in sees)
            {
                foreach (var other in pair.Value)
                {
                    if (!sees.TryGetValue(other, out var back))
                        continue;
                    pairs++;
                    Assert.That(back, Has.Member(pair.Key), $"{pair.Key} sees {other} but not back");
                }
            }
            Assert.That(pairs, Is.GreaterThan(1000));
        }

        [Test]
        public void ANarrowDoorway_LetsAConeThrough()
        {
            // A wall at y = 5 with a gap at x = 10; the eye stands two cells below it
            var walls = new SightBlockers(21, 21);
            for (int x = 0; x < 21; x++)
                if (x != 10)
                    walls.Set(x, 5, true);
            var seen = new HashSet<GridPoint>(GridSight.Visible(walls, 21, 21, P(10, 7), 10));
            Assert.That(seen, Has.Member(P(10, 0)), "straight through");
            Assert.That(seen, Has.Member(P(9, 2)).And.Member(P(11, 2)), "a cone widens behind the door");
            Assert.That(seen, Has.No.Member(P(2, 3)), "not along the wall's far side");
        }

        internal static SightBlockers RandomWalls(Random random, int width, int height, double chance)
        {
            var walls = new SightBlockers(width, height);
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    walls.Set(x, y, random.NextDouble() < chance);
            return walls;
        }
    }
}
