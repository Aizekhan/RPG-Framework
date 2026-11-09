using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RPGFramework.Bootstrap;
using RPGFramework.Events;
using RPGFramework.Persistence;
using UnityEngine.TestTools.Constraints;
using Is = NUnit.Framework.Is;

namespace RPGFramework.FogOfWar.Tests
{
    /// <summary>
    /// A fog per team: cells unexplored, explored or visible; eyes counted per cell, moved, removed; doors that open;
    /// allies that share sight; map scrolls; saves; the bus.
    /// </summary>
    public class FogOfWarMapTests
    {
        private const int Players = 0;
        private const int Monsters = 1;

        private static GridPoint P(int x, int y) => new GridPoint(x, y);

        [Test]
        public void ANewMap_IsUnexploredForEveryTeam()
        {
            var fog = new FogOfWarMap(10, 8, teams: 2);
            Assert.That(fog.CellCount, Is.EqualTo(80));
            Assert.That(fog.StateOf(Players, P(3, 3)), Is.EqualTo(FogState.Unexplored));
            Assert.That(fog.StateOf(Monsters, P(9, 7)), Is.EqualTo(FogState.Unexplored));
            Assert.That(fog.StateOf(Players, P(-1, 3)), Is.EqualTo(FogState.Unexplored), "off the map");
            Assert.That(fog.ExploredCount(Players), Is.Zero);
            Assert.That(() => fog.StateOf(2, P(0, 0)), Throws.InstanceOf<ArgumentOutOfRangeException>());
            Assert.That(() => new FogOfWarMap(10, 10, teams: 33), Throws.InstanceOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void AnEye_MakesWhatItSeesVisible_AndLeavesItExploredWhenItMoves()
        {
            var fog = new FogOfWarMap(30, 10);
            int hero = fog.AddEye(Players, P(3, 5), 2);
            Assert.That(fog.StateOf(Players, P(3, 5)), Is.EqualTo(FogState.Visible));
            Assert.That(fog.StateOf(Players, P(5, 5)), Is.EqualTo(FogState.Visible));
            Assert.That(fog.StateOf(Players, P(6, 5)), Is.EqualTo(FogState.Unexplored));
            int first = fog.ExploredCount(Players);
            Assert.That(fog.SeenBy(hero).Count, Is.EqualTo(first));

            fog.MoveEye(hero, P(20, 5));
            Assert.That(fog.StateOf(Players, P(3, 5)), Is.EqualTo(FogState.Explored), "remembered");
            Assert.That(fog.StateOf(Players, P(20, 5)), Is.EqualTo(FogState.Visible));
            Assert.That(fog.ExploredCount(Players), Is.EqualTo(first * 2));
            Assert.That(fog.PositionOf(hero), Is.EqualTo(P(20, 5)));

            Assert.That(fog.RemoveEye(hero), Is.True);
            Assert.That(fog.StateOf(Players, P(20, 5)), Is.EqualTo(FogState.Explored));
            Assert.That(fog.RemoveEye(hero), Is.False);
            Assert.That(fog.MoveEye(hero, P(1, 1)), Is.False);
            Assert.That(fog.EyeCount, Is.Zero);
        }

        [Test]
        public void ACellStaysVisible_WhileAnyEyeOfTheTeamSeesIt()
        {
            var fog = new FogOfWarMap(20, 20);
            int a = fog.AddEye(Players, P(5, 5), 3);
            int b = fog.AddEye(Players, P(8, 5), 3);
            fog.RemoveEye(a);
            Assert.That(fog.IsVisible(Players, P(6, 5)), Is.True, "b still sees it");
            Assert.That(fog.IsVisible(Players, P(3, 5)), Is.False);
            fog.MoveEye(b, P(15, 15));
            Assert.That(fog.IsVisible(Players, P(6, 5)), Is.False);
            Assert.That(fog.IsExplored(Players, P(6, 5)), Is.True);
        }

        [Test]
        public void EachTeam_HasItsOwnFog_AndAlliesShareTheirsOneWay()
        {
            var fog = new FogOfWarMap(20, 20, teams: 3);
            fog.AddEye(0, P(2, 2), 2);
            fog.AddEye(1, P(17, 17), 2);
            Assert.That(fog.IsVisible(1, P(2, 2)), Is.False);
            long before = fog.ViewVersion(1);

            fog.ShareVision(0, 1);
            Assert.That(fog.ViewVersion(1), Is.Not.EqualTo(before), "the view changed");
            Assert.That(fog.IsVisible(1, P(2, 2)), Is.True, "team 1 sees what team 0 sees");
            Assert.That(fog.IsVisible(0, P(17, 17)), Is.False, "not back");
            Assert.That(fog.IsVisible(2, P(2, 2)), Is.False);
            Assert.That(fog.SharesVision(0, 1), Is.True);
            Assert.That(fog.ExploredCount(1), Is.EqualTo(fog.ExploredCount(0)), "each counts its own");

            fog.ShareVision(0, 1, false);
            Assert.That(fog.IsVisible(1, P(2, 2)), Is.False);
            Assert.That(fog.IsExplored(1, P(2, 2)), Is.False);
        }

        [Test]
        public void ADoorThatOpens_LetsTheEyesInReachSeeThrough()
        {
            var walls = new SightBlockers(20, 5);
            for (int y = 0; y < 5; y++)
                walls.Set(10, y, true);
            var fog = new FogOfWarMap(20, 5, walls);
            fog.AddEye(Players, P(7, 2), 8);
            fog.AddEye(Players, P(0, 0), 1);
            Assert.That(fog.IsVisible(Players, P(10, 2)), Is.True, "the door is seen");
            Assert.That(fog.IsVisible(Players, P(13, 2)), Is.False);

            walls.Set(10, 2, false);
            fog.BlockersChanged(P(10, 2));
            Assert.That(fog.IsVisible(Players, P(13, 2)), Is.True);

            walls.Set(10, 2, true);
            fog.BlockersChanged();
            Assert.That(fog.StateOf(Players, P(13, 2)), Is.EqualTo(FogState.Explored));
        }

        [Test]
        public void AnEyesOwnBlockers_SeeOverLowWalls()
        {
            var lowWalls = new SightBlockers(20, 5);
            lowWalls.Set(10, 2, true);
            var fog = new FogOfWarMap(20, 5, lowWalls, teams: 2);
            fog.AddEye(0, P(7, 2), 6);
            fog.AddEye(1, P(7, 2), new FogSight(6, blockers: SightBlockers.None));
            Assert.That(fog.IsVisible(0, P(12, 2)), Is.False, "behind the wall");
            Assert.That(fog.IsVisible(1, P(12, 2)), Is.True, "from high ground");
        }

        [Test]
        public void SetSight_GrowsAndShrinksWhatAnEyeSees()
        {
            var fog = new FogOfWarMap(30, 30);
            int torch = fog.AddEye(Players, P(15, 15), 2);
            Assert.That(fog.IsVisible(Players, P(19, 15)), Is.False);
            fog.SetSight(torch, new FogSight(5, SightShape.Square));
            Assert.That(fog.IsVisible(Players, P(20, 20)), Is.True);
            Assert.That(fog.SightOf(torch).Radius, Is.EqualTo(5));
            fog.SetSight(torch, new FogSight(0));
            Assert.That(fog.SeenBy(torch), Is.EqualTo(new[] { P(15, 15) }));
        }

        [Test]
        public void Explore_MarksAnAreaWithoutSight_AndForgetKeepsOnlyWhatIsSeen()
        {
            var walls = GridSightTests.RandomWalls(new Random(1), 40, 40, 0.4);
            var fog = new FogOfWarMap(40, 40, walls);
            int explored = fog.Explore(Players, P(30, 30), 3, SightShape.Square);
            Assert.That(explored, Is.EqualTo(49), "walls do not stop a map");
            Assert.That(fog.StateOf(Players, P(30, 30)), Is.EqualTo(FogState.Explored));
            Assert.That(fog.Explore(Players, P(30, 30), 3, SightShape.Square), Is.Zero);

            fog.AddEye(Players, P(5, 5), new FogSight(2, throughWalls: true));
            fog.ForgetExplored(Players);
            Assert.That(fog.IsExplored(Players, P(30, 30)), Is.False);
            Assert.That(fog.IsVisible(Players, P(5, 5)), Is.True);
            Assert.That(fog.ExploredCount(Players), Is.EqualTo(21), "a circle of radius 2");

            fog.ExploreAll(Players);
            Assert.That(fog.ExploredFraction(Players), Is.EqualTo(1f));
        }

        [Test]
        public void ExploringIsAnnounced_OnTheBusAndAsAnEvent()
        {
            var bus = new EventBus();
            var onBus = new List<FogExploredEvent>();
            bus.Subscribe<FogExploredEvent>(onBus.Add);
            var fog = new FogOfWarMap(10, 10, teams: 2, events: bus);
            var raised = new List<FogExploredEvent>();
            fog.Explored += raised.Add;

            int eye = fog.AddEye(Monsters, P(5, 5), 1, SightShape.Diamond);
            Assert.That(onBus.Single().Team, Is.EqualTo(Monsters));
            Assert.That(onBus.Single().NewCells, Is.EqualTo(5));
            Assert.That(onBus.Single().TotalCells, Is.EqualTo(100));
            Assert.That(raised, Is.EqualTo(onBus));

            fog.MoveEye(eye, P(5, 6));
            Assert.That(onBus.Last().NewCells, Is.EqualTo(3));
            Assert.That(onBus.Last().ExploredCells, Is.EqualTo(8));
            fog.MoveEye(eye, P(5, 5));
            Assert.That(onBus.Count, Is.EqualTo(2), "nothing new");
        }

        [Test]
        public void ViewVersion_GrowsOnChange_NotWhenAnEyeStaysInItsCell()
        {
            var fog = new FogOfWarMap(10, 10);
            int eye = fog.AddEye(Players, P(5, 5), 2);
            long version = fog.ViewVersion(Players);
            fog.MoveEye(eye, P(5, 5));
            Assert.That(fog.ViewVersion(Players), Is.EqualTo(version));
            fog.MoveEye(eye, P(4, 5));
            Assert.That(fog.ViewVersion(Players), Is.GreaterThan(version));
        }

        [Test]
        public void WriteStates_GivesAByteACell_RowByRow()
        {
            var fog = new FogOfWarMap(4, 3);
            fog.Explore(Players, P(3, 2), 0);
            fog.AddEye(Players, P(0, 0), 0);
            var bytes = new byte[12];
            fog.WriteStates(Players, bytes, unexplored: 9, explored: 5, visible: 1);
            Assert.That(bytes, Is.EqualTo(new byte[] { 1, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 5 }));
            Assert.That(() => fog.WriteStates(Players, new byte[11]), Throws.ArgumentException);
        }

        [Test]
        public void ARecord_BringsBackWhatEachTeamExplored()
        {
            var fog = new FogOfWarMap(13, 7, teams: 2);
            fog.Explore(0, P(2, 2), 2);
            fog.Explore(1, P(10, 4), 1, SightShape.Diamond);
            var record = fog.Capture();

            var loaded = new FogOfWarMap(13, 7, teams: 2);
            loaded.AddEye(0, P(12, 6), 0);
            Assert.That(loaded.Restore(record), Is.True);
            Assert.That(loaded.StateOf(0, P(2, 2)), Is.EqualTo(FogState.Explored));
            Assert.That(loaded.StateOf(1, P(10, 4)), Is.EqualTo(FogState.Explored));
            Assert.That(loaded.StateOf(0, P(12, 6)), Is.EqualTo(FogState.Visible), "what is seen now stays");
            Assert.That(loaded.ExploredCount(0), Is.EqualTo(fog.ExploredCount(0) + 1));
            Assert.That(loaded.ExploredCount(1), Is.EqualTo(5));

            Assert.That(new FogOfWarMap(12, 7).Restore(record), Is.False, "another map size");
            Assert.That(loaded.Restore(new FogOfWarRecord { Width = 13, Height = 7, Explored = { "not base64!" } }),
                Is.False);
        }

        [Test]
        public void TheSave_KeepsTheFogUnderItsKey()
        {
            var serializer = new JsonSaveSerializer();
            var before = new FogOfWarMap(9, 9);
            before.Explore(Players, P(4, 4), 2);
            ISaveParticipant saving = new FogOfWarSave(before);
            byte[] data = saving.Capture(serializer);

            var after = new FogOfWarMap(9, 9);
            ISaveParticipant loading = new FogOfWarSave(() => after, "fog-of-war-level-2");
            Assert.That(saving.SaveKey, Is.EqualTo(FogOfWarSave.Key));
            Assert.That(loading.SaveKey, Is.EqualTo("fog-of-war-level-2"));
            loading.Restore(loading.Read(data, loading.Version, serializer));
            Assert.That(after.ExploredCount(Players), Is.EqualTo(before.ExploredCount(Players)));
            Assert.That(after.IsExplored(Players, P(4, 2)), Is.True);
        }

        [Test]
        public void TheModule_RegistersAMap_AnnouncedOnTheHostsBus()
        {
            var host = new GameHost();
            host.Start(b => b.UseModule(new FogOfWarModule(16, 16, teams: 2)));
            try
            {
                var fog = host.Container.Resolve<FogOfWarMap>();
                Assert.That(fog.TeamCount, Is.EqualTo(2));
                var explored = new List<FogExploredEvent>();
                host.Container.Resolve<IEventBus>().Subscribe<FogExploredEvent>(explored.Add);
                fog.AddEye(1, P(8, 8), 3);
                Assert.That(explored.Single().Team, Is.EqualTo(1));
            }
            finally
            {
                host.Dispose();
            }
        }

        [Test]
        public void TheGridSpace_PutsWorldPointsInCells()
        {
            var space = new FogGridSpace(-50f, -20f, 2f, 50, 20);
            Assert.That(space.CellAt(-50f, -20f), Is.EqualTo(P(0, 0)));
            Assert.That(space.CellAt(-48.1f, -17.9f), Is.EqualTo(P(0, 1)));
            Assert.That(space.CellAt(-51f, 0f).X, Is.EqualTo(-1));
            Assert.That(space.Contains(space.CellAt(49.9f, 19.9f)), Is.True);
            Assert.That(space.Contains(space.CellAt(50f, 0f)), Is.False);
            space.CenterOf(P(0, 0), out float x, out float y);
            Assert.That(x, Is.EqualTo(-49f));
            Assert.That(y, Is.EqualTo(-19f));
            Assert.That(space.CellsIn(12f), Is.EqualTo(6));
            Assert.That(space.CellsIn(12.5f), Is.EqualTo(7));
            var covering = FogGridSpace.Covering(0f, 0f, 101f, 40f, 2f);
            Assert.That(covering.Width, Is.EqualTo(51));
            Assert.That(covering.Height, Is.EqualTo(20));
        }

        [Test]
        public void MovingEyes_MakeNoGarbage()
        {
            var walls = GridSightTests.RandomWalls(new Random(5), 64, 64, 0.2);
            var fog = new FogOfWarMap(64, 64, walls, teams: 2);
            var eyes = new int[20];
            var random = new Random(9);
            for (int i = 0; i < eyes.Length; i++)
                eyes[i] = fog.AddEye(i % 2, P(random.Next(64), random.Next(64)), 8);
            var path = new GridPoint[400];
            for (int i = 0; i < path.Length; i++)
                path[i] = P(random.Next(64), random.Next(64));
            int step = 0;
            void MoveAll()
            {
                for (int i = 0; i < eyes.Length; i++)
                    fog.MoveEye(eyes[i], path[(step + i) % path.Length]);
                step++;
            }
            for (int i = 0; i < path.Length; i++)
                MoveAll();

            var allocates = new AllocatingGCMemoryConstraint();
            Assert.That(allocates.ApplyTo(new TestDelegate(() => new int[16].GetHashCode())).IsSuccess, Is.True,
                "the check sees garbage");
            int dirty = 0;
            for (int i = 0; i < 100; i++)
                if (allocates.ApplyTo(new TestDelegate(MoveAll)).IsSuccess)
                    dirty++;
            // A stray runtime allocation can fall into a measured call on CI runners
            Assert.That(dirty, Is.LessThanOrEqualTo(2));
        }
    }
}
