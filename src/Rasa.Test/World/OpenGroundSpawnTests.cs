using System;
using System.IO;
using System.Linq;
using System.Numerics;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Rasa.Game;
using Rasa.Managers;
using Rasa.Navigation;
using Rasa.Structures;
using Rasa.Test.Missions.Plateau;
using Rasa.Test.Missions.Wilderness;

namespace Rasa.Test.World
{
    /// <summary>
    /// A pool whose area is out in the open puts its creatures, and strolls them, on open ground
    /// (SpawnPoolManager.IsOpenGround): the Thrax at the pillbox of the Bane fort in western
    /// Valverde Pools (531678, Bane Investigation's camp) came out one in three inside the
    /// pillbox, on its roof or along the wall tops round it.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class OpenGroundSpawnTests
    {
        private const uint Pools = 1304;
        private const uint PillboxCamp = 531678;

        // The pillbox (ArchBaneOutpostPillbox at -795.9, 782.6, -17.3): its floor at the
        // terminal, under its roof, and its roof.
        private static readonly Vector3 PillboxFloor = new(-790f, 783.46f, -11.3f);
        private static readonly Vector3 PillboxRoof = new(-797f, 788.32f, -15f);
        private static readonly Vector3 FortGround = new(-779.7f, 782.7f, -7.2f);

        [TestMethod]
        public void ThePillboxIsNoOpenGroundAndTheGroundBesideItIs()
        {
            using var harness = WildernessRuntimeTestHarness.Create();
            var map = PoolsMap(harness);

            Assert.IsTrue(SpawnPoolManager.IsOpenGround(map, FortGround), "the fort's open ground");
            Assert.IsFalse(SpawnPoolManager.IsOpenGround(map, PillboxFloor), "under the pillbox's roof");
            Assert.IsFalse(SpawnPoolManager.IsOpenGround(map, PillboxRoof), "on the pillbox's roof, over the terrain");
            Assert.IsTrue(SpawnPoolManager.IsOutdoors(map, SpawnPoolManager.SpreadCentre(map, Pool(map))), "the camp is out in the open");
        }

        [TestMethod]
        public void TheCampAtThePillboxComesOutOnOpenGroundOnly()
        {
            using var harness = WildernessRuntimeTestHarness.Create();
            var map = PoolsMap(harness);
            var pool = Pool(map);

            for (var draw = 0; draw < 300; draw++)
            {
                var at = SpawnPoolManager.SpawnPoint(map, pool, 3);
                Assert.IsTrue(SpawnPoolManager.IsOpenGround(map, at), $"draw {draw} at {at}");
            }

            // Without the cover there is nothing to tell: the draw is anywhere on the navmesh in the disc,
            // and a fair share of it is in or on the pillbox.
            map.Cover = null;
            var cover = Cover(harness);
            var mapWithCover = new MapChannel { Cover = cover };
            var inside = Enumerable.Range(0, 300).Count(_ => !SpawnPoolManager.IsOpenGround(mapWithCover, SpawnPoolManager.SpawnPoint(map, pool, 3)));
            Assert.IsGreaterThan(30, inside, "the pillbox takes draws without the check");
        }

        [TestMethod]
        public void ItsCreaturesStrollOnOpenGroundOnly()
        {
            using var harness = WildernessRuntimeTestHarness.Create();
            var map = PoolsMap(harness);
            PlateauCampResistanceTests.Spawn(map, PillboxCamp);
            var thrax = PlateauCampResistanceTests.Living(map).Where(creature => creature.SpawnPool?.DbId == PillboxCamp).ToList();
            Assert.IsNotEmpty(thrax);

            foreach (var creature in thrax)
            {
                Assert.IsTrue(SpawnPoolManager.IsOpenGround(map, creature.Position), $"{creature.EntityId} came out at {creature.Position}");

                for (var attempt = 0; attempt < 100; attempt++)
                    if (BehaviorManager.PickStroll(map, creature) is Vector3 to && to != creature.HomePos.Position)
                        Assert.IsTrue(SpawnPoolManager.IsOpenGround(map, to), $"{creature.EntityId} strolls to {to}");
            }
        }

        [TestMethod]
        public void TheTerrainSurfaceIsTheGridJoinedUp()
        {
            using var harness = WildernessRuntimeTestHarness.Create();
            var cover = Cover(harness);

            // On a grid point the surface is its height; between two, between theirs.
            var a = cover.TerrainHeight(-780f, -8f).Value;
            var b = cover.TerrainHeight(-778f, -8f).Value;
            Assert.AreEqual(a, cover.TerrainSurface(-780f, -8f).Value, 1e-3f);
            Assert.AreEqual((a + b) / 2f, cover.TerrainSurface(-779f, -8f).Value, 1e-3f);
            Assert.IsNull(cover.TerrainSurface(1e6f, 1e6f));
        }

        private static MapChannel PoolsMap(WildernessRuntimeTestHarness harness)
        {
            var map = harness.Maps.MapChannelArray.TryGetValue(Pools, out var loaded) ? loaded : PlateauInstanceMaps.LoadMap(harness, Pools);
            map.Cover = Cover(harness);
            return map;
        }

        private static SpawnPool Pool(MapChannel map) => map.SpawnPools.Single(pool => pool.DbId == PillboxCamp);

        private static CoverMesh Cover(WildernessRuntimeTestHarness _) =>
            CoverMesh.Read(CoverMesh.PathFor(Path.Combine(Root(), "navmesh"), "adv_foreas_valverde_pools"));

        private static string Root()
        {
            for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
                if (File.Exists(Path.Combine(directory.FullName, "Rasa.NET.sln")))
                    return directory.FullName;

            throw new DirectoryNotFoundException("Rasa.NET.sln");
        }
    }
}
