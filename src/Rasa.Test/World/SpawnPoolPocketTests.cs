using System;
using System.IO;
using System.Linq;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Rasa.Test.World
{
    using Rasa.Managers;
    using Rasa.Navigation;
    using Rasa.Structures;
    using Rasa.Test.TestSupport;

    /// <summary>
    /// A pool whose point is over a pocket of navmesh spreads its creatures from the open ground
    /// in its area (SpawnPoolManager.SpreadCentre), against the maps' real navmeshes. The Xanx
    /// Wanderers by the Logos "Now" on Concordia Divide all came out inside their nests, on a
    /// 22 m² pocket joined to nothing, and never left it.
    /// </summary>
    [TestClass]
    public class SpawnPoolPocketTests
    {
        private const string Divide = "adv_foreas_concordia_divide";

        /// <summary>The open ground by the shrine the Xanx stand round.</summary>
        private static readonly Vector3 ShrineGround = new Vector3(515.65f, 89.35f, 764.72f);

        [TestMethod]
        [DataRow(530036U, 500.4f, 86.1f, 792.8f)]
        [DataRow(530037U, 538.6f, 74.1f, 731.3f)]
        public void TheXanxByTheLogosComeOutOnTheOpenGround(uint id, float x, float y, float z)
        {
            var map = Map(Divide);
            var pool = Pool(id, x, y, z, 6f);
            var shrine = map.NavMesh.Nearest(ShrineGround);

            Assert.IsTrue(shrine.HasValue);
            Assert.IsTrue(map.NavMesh.RegionArea(map.NavMesh.Nearest(pool.Position).Value) < 25f, "the point is over a pocket");
            Assert.AreNotEqual(true, map.NavMesh.Reachable(map.NavMesh.Nearest(pool.Position).Value, shrine.Value), "and the pocket is cut off");

            var from = SpawnPoolManager.SpreadCentre(map, pool);

            Assert.AreNotEqual(pool.Position, from);
            Assert.IsTrue(Horizontal(from, pool.Position) <= pool.Radius, $"{from}: within the pool's area");
            Assert.IsTrue(map.NavMesh.RegionArea(from) > 1000f, $"{from}: on open ground");

            for (var i = 0; i < 40; i++)
            {
                var point = SpawnPoolManager.SpawnPoint(map, pool, 3);

                Assert.IsTrue(map.NavMesh.IsOnMesh(point), $"{point}");
                Assert.AreEqual(true, map.NavMesh.Reachable(point, shrine.Value), $"{point}: can walk to the shrine");
                Assert.IsTrue(Horizontal(point, from) <= pool.Radius + 0.01f, $"{point}: within the radius");
            }
        }

        [TestMethod]
        [DataRow(530034U, 460.8f, 78.8f, 744.6f, 13.1f)]
        [DataRow(530035U, 504.3f, 79.8f, 693.3f, 11.8f)]
        public void TheOtherXanxPoolsOnOpenGroundStayWhereTheyAre(uint id, float x, float y, float z, float radius)
        {
            var pool = Pool(id, x, y, z, radius);

            Assert.AreEqual(pool.Position, SpawnPoolManager.SpreadCentre(Map(Divide), pool));
        }

        [TestMethod]
        public void AXanxCreatureWandersRoundTheGroundItCameOutOn()
        {
            // Round the pool's point, a spider put on the open ground beside the nest would be
            // outside its own zone from its first step, with nowhere to stroll to.
            var map = Map(Divide);
            var pool = Pool(530036, 500.4f, 86.1f, 792.8f, 6f);
            pool.MapContextId = 1148;
            var from = SpawnPoolManager.SpreadCentre(map, pool);
            var creature = new Creature { MapContextId = 1148, SpawnPool = pool };

            Assert.AreEqual((from, 6f), BehaviorManager.WanderZoneOf(creature));
            Assert.AreEqual(from, pool.AreaCentre);

            pool.Position += new Vector3(1f, 0f, 0f);
            Assert.AreEqual(pool.Position, pool.AreaCentre, "moved: the point, until it is worked out again");
        }

        [TestMethod]
        [DataRow(531257U, "adv_arieki_torden_incline_ojasaattahive", -279.5f, 135.6f, -17.1f, 15f, 113.2f)]   // Ojasa Gatherers: anchor on a queen's mound
        [DataRow(531429U, "adv_arieki_torden_mires_banefluxitemines", 110.4f, 127.3f, 448.3f, 40f, 105.9f)]  // Baneflux mines: the carts' floor, not the catwalk beside it
        [DataRow(540106U, "adv_arieki_torden_plains", -1171.9f, 409.4f, -212.7f, 15f, 381.0f)]               // Beam Mantas: anchor on a cave mouth's rock; their sisters' floor, not the upland over it
        [DataRow(531227U, "adv_arieki_torden_incline", -556.6f, 368.4f, 291.9f, 20f, 316.6f)]                // Atta lair: props from 302 m to 399 m
        public void APointOffTheMeshWhoseAnchorIsAPocketSpreadsFromTheFloorUnderIt(uint id, string mapName, float x, float y, float z, float radius, float floor)
        {
            var map = Map(mapName);
            var pool = Pool(id, x, y, z, radius);

            Assert.IsFalse(map.NavMesh.IsOnMesh(pool.Position), "the point is off the mesh");
            Assert.IsTrue(map.NavMesh.RegionArea(SpawnPoolManager.Anchor(map, pool).Value) < NavMeshManager.UnstickPocketArea, "and its anchor is a pocket");

            var from = SpawnPoolManager.SpreadCentre(map, pool);

            Assert.IsTrue(map.NavMesh.RegionArea(from) >= NavMeshManager.UnstickPocketArea, $"{from}: open ground");
            Assert.AreEqual(floor, from.Y, 2f, $"{from}: the floor the props stand on");

            for (var i = 0; i < 20; i++)
            {
                var point = SpawnPoolManager.SpawnPoint(map, pool, 3);

                Assert.AreEqual(true, map.NavMesh.Reachable(point, from), $"{point}: on that ground");
            }
        }

        [TestMethod]
        [DataRow(531476U, 715.2f, 139.8f, -704.3f, 11f)]
        [DataRow(531488U, 729.9f, 140.7f, -723.0f, 5f)]
        public void ThraxInABaneTrenchOnPalisadesComeOutOfIt(uint id, float x, float y, float z, float radius)
        {
            // The trench's floor is 38 m² of mesh, cut off by its walls: beyond the radius, but in reach.
            var map = Map("adv_foreas_concordia_palisades");
            var pool = Pool(id, x, y, z, radius);
            var from = SpawnPoolManager.SpreadCentre(map, pool);

            Assert.AreNotEqual(pool.Position, from);
            Assert.IsTrue(Horizontal(from, pool.Position) <= SpawnPoolManager.SpreadReach, $"{from}");
            Assert.IsTrue(map.NavMesh.RegionArea(from) > 1_000_000f, $"{from}: the open ground of the map");
        }

        [TestMethod]
        public void APocketWithNoOpenGroundInReachKeepsItsPoint()
        {
            // 540090: Beam Mantas on 34 m² of a rock vista; the plateau is 63 m off, and the 75 m²
            // pocket 60 m under them is no better, so not worth leaving their floor for.
            var map = Map("adv_arieki_torden_mires");
            var pool = Pool(540090, 492.5f, 263.0f, -385.6f, 15f);

            Assert.AreEqual(pool.Position, SpawnPoolManager.SpreadCentre(map, pool));
            Assert.IsTrue(map.NavMesh.IsOnMesh(SpawnPoolManager.SpawnPoint(map, pool, 2)), "still put on its ground");
        }

        [TestMethod]
        public void TheCentreIsWorkedOutOnceForAPoint()
        {
            var map = Map(Divide);
            var pool = Pool(530036, 500.4f, 86.1f, 792.8f, 6f);
            var from = SpawnPoolManager.SpreadCentre(map, pool);

            Assert.AreEqual(from, pool.SpreadFrom);
            Assert.AreEqual(from, SpawnPoolManager.SpreadCentre(map, pool));

            // Moved (by a GM, say): worked out again for the new point.
            pool.Position = new Vector3(460.8f, 78.8f, 744.6f);
            Assert.AreEqual(pool.Position, SpawnPoolManager.SpreadCentre(map, pool));
        }

        [TestMethod]
        public void AMapWithoutANavmeshSpreadsFromThePoint()
        {
            var map = new MapChannel { MapInfo = new MapInfo(1148, Divide, 0, 0) };
            var pool = Pool(530036, 500.4f, 86.1f, 792.8f, 6f);

            Assert.AreEqual(pool.Position, SpawnPoolManager.SpreadCentre(map, pool));
        }

        private static float Horizontal(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.X, a.Z), new Vector2(b.X, b.Z));

        private static SpawnPool Pool(uint id, float x, float y, float z, float radius) =>
            new SpawnPool { DbId = id, Position = new Vector3(x, y, z), Radius = radius, Mode = SpawnPoolManager.ModeAutomatic };

        private static MapChannel Map(string mapName)
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);

            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Rasa.NET.sln")))
                directory = directory.Parent;

            Assert.IsNotNull(directory, "Repository root not found.");

            return new MapChannel
            {
                MapInfo = new MapInfo(1, mapName, 0, 0),
                NavMesh = TestNavMeshes.Query(NavMeshFile.PathFor(Path.Combine(directory.FullName, "navmesh"), mapName))
            };
        }
    }
}
