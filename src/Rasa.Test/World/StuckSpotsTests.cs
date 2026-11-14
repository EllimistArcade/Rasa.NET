using System;
using System.Linq;
using System.Numerics;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Rasa.Data;
using Rasa.Managers;
using Rasa.Structures.World;
using Rasa.Test.Missions.Wilderness;
using D = Rasa.Services.Preloader.DevilsDenContent;

namespace Rasa.Test.World
{
    /// <summary>
    /// Corners the client's collision holds a player in (StuckSpots): one who stays in one is put
    /// down at its way out, on open floor; one passing through, or dead, is left alone.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class StuckSpotsTests
    {
        private long _now = 1_000_000;

        /// <summary>The corner under Pinhole Falls, where the report put the player.</summary>
        private static readonly Vector3 Corner = new Vector3(336.2f, 228.8f, 476.0f);

        [TestInitialize]
        public void Clock()
        {
            StuckSpots.Reset();
            StuckSpots.Now = () => _now;
        }

        [TestCleanup]
        public void Restore()
        {
            StuckSpots.Reset();
            StuckSpots.Now = () => Environment.TickCount64;
            PlacedDestructibles.Load(null);
            PlacedDestructibles.Reset();
        }

        /// <summary>Inside the Devil's Den generator, where the report put the player.</summary>
        private static readonly Vector3 InTheGenerator = new Vector3(-123f, 134.2f, 165.5f);

        [TestMethod]
        public void EveryWayOutIsOpenFloorJoinedToItsCorner()
        {
            using var harness = WildernessRuntimeTestHarness.Create();
            var nav = harness.Map.NavMesh;

            foreach (var spot in StuckSpots.Spots.Where(spot => spot.Map == 1220))
            {
                var middle = (spot.Min + spot.Max) / 2f;
                var corner = nav.Nearest(middle);
                var way = nav.Nearest(spot.Out);

                Assert.IsNotNull(corner, spot.Name);
                Assert.IsNotNull(way, spot.Name);
                Assert.IsTrue(Vector3.Distance(way.Value, spot.Out) < 1f, $"{spot.Name}: the way out {spot.Out} is on the mesh ({way})");
                Assert.IsNull(StuckSpots.At(spot.Map, way.Value), $"{spot.Name}: the way out is in no corner");
                Assert.AreEqual(true, nav.Reachable(corner.Value, way.Value), $"{spot.Name}: on the same floor");
                Assert.IsTrue(nav.DistanceToWall(way.Value, 2f) >= 1f, $"{spot.Name}: clear of the mesh's edges");
                Assert.IsTrue(Vector3.Distance(middle, way.Value) < 8f, $"{spot.Name}: a few metres off");
            }

            Assert.IsNotNull(StuckSpots.At(1220, Corner), "the reported spot is in one");
        }

        [TestMethod]
        public void ThoseWhoStayInTheCornerAreTakenOutAndThosePassingAreNot()
        {
            using var harness = WildernessRuntimeTestHarness.Create();
            var player = harness.Client.Player;
            player.State = CharacterState.Idle;

            // Passing through: in, and out again before the hold is up.
            Move(harness, Corner);
            Work(harness, 0);
            Work(harness, StuckSpots.HoldMs - 1000);
            Move(harness, Corner + new Vector3(4f, 0f, -4f));
            Work(harness, 1000);
            Move(harness, Corner);
            Work(harness, 1000);
            Assert.AreEqual(Corner, player.Position, "the clock started again");

            // Held there.
            Work(harness, StuckSpots.HoldMs);
            var spot = StuckSpots.At(1220, Corner);
            Assert.IsTrue(Vector3.Distance(player.Position, spot.Out) < 1f, $"taken out to {player.Position}");
            Assert.IsNull(StuckSpots.At(1220, player.Position));
            Assert.IsTrue(Math.Abs(harness.Map.NavMesh.GroundHeight(player.Position).Value - player.Position.Y) < 0.1f, "on the floor");
        }

        [TestMethod]
        public void TheDeadAreLeftWhereTheyFell()
        {
            using var harness = WildernessRuntimeTestHarness.Create();
            var player = harness.Client.Player;

            Move(harness, Corner);
            player.State = CharacterState.Dead;
            Work(harness, 0);
            Work(harness, StuckSpots.HoldMs * 3);
            Assert.AreEqual(Corner, player.Position);
        }

        [TestMethod]
        public void TheDensGeneratorHoldsThoseUnderItsRoofAndNotThoseOnItOrBesideIt()
        {
            using var harness = Den();
            var map = harness.Map;
            Assert.IsNull(StuckSpots.At(map, InTheGenerator), "not until the generator is put down");

            PlaceGenerator(harness);

            var hall = StuckSpots.At(map, InTheGenerator);
            Assert.IsNotNull(hall, "the reported spot is under the hall's roof");
            StringAssert.Contains(hall.Name, "generator");

            Vector3 At(float x, float y, float z) => StuckSpots.InFrame(D.GeneratorAt, (float)D.GeneratorYaw, new Vector3(x, y, z));

            Assert.IsNotNull(StuckSpots.At(map, At(0f, 0f, 0f)), "the middle of the hall");
            Assert.IsNotNull(StuckSpots.At(map, At(-6f, 2.8f, 0f)), "on the slope inside, under the roof");
            Assert.IsNotNull(StuckSpots.At(map, At(0f, -1.5f, -11f)), "in the downhill tank");
            Assert.IsNull(StuckSpots.At(map, At(-6.5f, 4.2f, 0f)), "on the roof where the uphill ground runs onto it");
            Assert.IsNull(StuckSpots.At(map, At(0f, 7.8f, 0f)), "on its crest");
            Assert.IsNull(StuckSpots.At(map, At(9.2f, 0.5f, 0f)), "beside it");
            Assert.IsNull(StuckSpots.At(map, At(4f, -1f, -11f)), "beside the tank, in the open");
            Assert.IsNull(StuckSpots.At(map, hall.Out), "the way out");

            // The way out: open floor, joined to the floor the report was on.
            var nav = map.NavMesh;
            var way = nav.Nearest(hall.Out);
            Assert.IsNotNull(way);
            Assert.IsTrue(Vector3.Distance(new Vector3(way.Value.X, 0f, way.Value.Z), new Vector3(hall.Out.X, 0f, hall.Out.Z)) < 0.5f, $"{hall.Out} on the mesh ({way})");
            Assert.AreEqual(true, nav.Reachable(nav.Nearest(InTheGenerator).Value, way.Value));
            Assert.IsTrue(nav.DistanceToWall(way.Value, 2f) >= 1f, "clear of the mesh's edges");
        }

        [TestMethod]
        public void OneWhoStaysInTheGeneratorIsTakenOutBesideIt()
        {
            using var harness = Den();
            PlaceGenerator(harness);
            var player = harness.Client.Player;
            player.State = CharacterState.Idle;

            Move(harness, InTheGenerator);
            Work(harness, 0);
            Work(harness, StuckSpots.HoldMs);

            var spot = StuckSpots.At(D.Den, InTheGenerator);
            Assert.IsTrue(Vector3.Distance(player.Position, spot.Out) < 1.5f, $"taken out to {player.Position}");
            Assert.IsNull(StuckSpots.At(harness.Map, player.Position));
        }

        private static WildernessRuntimeTestHarness Den() => WildernessRuntimeTestHarness.Create(mapContextId: D.Den);

        private static void PlaceGenerator(WildernessRuntimeTestHarness harness)
        {
            PlacedDestructibles.Load(harness.World.Set<WorldDestructibleEntry>().AsNoTracking()
                .Where(row => row.MapContextId == D.Den && row.Id == D.GeneratorRow).ToList());
            Assert.AreEqual(1, PlacedDestructibles.Place(harness.Map));
            Assert.IsNotNull(PlacedDestructibles.LiveOf(harness.Map, D.GeneratorRow));
        }

        private static void Move(WildernessRuntimeTestHarness harness, Vector3 to)
        {
            harness.Client.Player.PlaceAt(to);
            harness.Drain();
        }

        private void Work(WildernessRuntimeTestHarness harness, long milliseconds)
        {
            _now += milliseconds;
            StuckSpots.Worker(harness.Map);
            harness.Drain();
        }
    }
}
