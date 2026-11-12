using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Rasa.Data;
using Rasa.Managers;
using Rasa.Navigation;
using Rasa.Structures;
using Rasa.Test.TestSupport;

namespace Rasa.Test.World
{
    /// <summary>
    /// The navmeshes Rasa.NavMesh's MapNavFixes rebuilt, against the shipped files: the parts of a map
    /// that no path joined before - which an escort, a guard or a creature after a player could not cross -
    /// are joined, both ways, and a creature walks the Retread Caves' two off-mesh links.
    /// </summary>
    [TestClass, DoNotParallelize]
    public class NavMeshJoinsTests
    {
        private const string TurpisRefinery = "adv_foreas_valverde_marshes_wetlandrefinery";
        private const string SupplyDepot = "adv_foreas_valverde_marshes_banesupplydepot";
        private const string RetreadCaves = "adv_foreas_valverde_pools_retread_caves";

        // Turpis Refinery (1429): the map link's arrival; either side of the tunnel mouth the heightmap closed;
        // Hard Core's three hard drives (PoolsHardCoreV1).
        private static readonly Vector3 RefineryArrival = new(-211.81f, 106.77f, -219.03f);
        private static readonly Vector3 PocketSide = new(-274.8f, 125.1f, -200.4f);
        private static readonly Vector3 PassageSide = new(-278.8f, 125.2f, -200.1f);
        private static readonly Vector3[] HardDrives = { new(-24f, 107.71f, 176f), new(64f, 107.86f, -96f), new(-239.2f, 116.31f, -64f) };

        // Bane Supply Depot (1451): the arrival; inside the chunnel's mouth, and outside it, where the heightmap ran over it.
        private static readonly Vector3 DepotArrival = new(-365.2f, 150.04f, 3.1f);
        private static readonly Vector3 ChunnelInside = new(-276f, 149.64f, 36f);
        private static readonly Vector3 ChunnelOutside = new(-272f, 149.57f, 36f);

        // Retread Caves (1694): the door's arrival, the Retreads, the Western Grotto's Mox pools and the Mox Lair's
        // Mutated Miasma pools (PoolsRetreadCavesNpcsV1).
        private static readonly Vector3 CavesArrival = new(19.32f, 82.54f, -4.40f);
        private static readonly Vector3[] CavesPlaces =
        {
            new(-40f, 62.85f, -184f), new(-40.3f, 62.85f, -176.3f), new(-48f, 62.95f, -192f), new(0f, 47.49f, -200f),
            new(104f, 9.23f, -40f), new(152f, 12.65f, -56f), new(176f, 10.46f, 16f), new(64f, 4.41f, -72f), new(168f, 8.54f, 0f),
            new(104f, 55.43f, -288f), new(144f, 47.65f, -296f), new(128f, 51.44f, -312f), new(96f, 55.82f, -238.8f)
        };

        // Each off-mesh link: a point a few metres back on either side.
        private static readonly (Vector3 Camp, Vector3 Other)[] CavesLinks =
        {
            (new(5.4f, 51.65f, -44.6f), new(12.6f, 50.25f, -37.6f)),
            (new(36.6f, 61.25f, -194.2f), new(41f, 60.85f, -194.2f))
        };

        [TestMethod]
        public void TurpisRefineryArrivalReachesEachHardDriveAcrossTheTunnelMouth()
        {
            var nav = Query(TurpisRefinery);

            AssertRoute(nav, PocketSide, PassageSide);
            AssertRoute(nav, PassageSide, PocketSide);

            foreach (var drive in HardDrives)
            {
                AssertRoute(nav, RefineryArrival, drive);
                AssertRoute(nav, drive, RefineryArrival);
            }
        }

        [TestMethod]
        public void SupplyDepotArrivalWalksThroughTheChunnelIntoTheDepot()
        {
            var nav = Query(SupplyDepot);

            AssertRoute(nav, ChunnelInside, ChunnelOutside);
            AssertRoute(nav, ChunnelOutside, ChunnelInside);
            AssertRoute(nav, DepotArrival, ChunnelOutside);
            Assert.IsGreaterThan(250000f, nav.RegionArea(nav.NearestInColumn(DepotArrival, 4f).Value).Value,
                "the arrival is on the depot's own floor, not a hall of 1,092 m²");
        }

        [TestMethod]
        public void RetreadCavesArrivalReachesTheCampTheGrottoAndTheLair()
        {
            var nav = Query(RetreadCaves);

            foreach (var place in CavesPlaces)
            {
                AssertRoute(nav, CavesArrival, place);
                AssertRoute(nav, place, CavesArrival);
            }

            foreach (var (camp, other) in CavesLinks)
            {
                AssertRoute(nav, camp, other);
                AssertRoute(nav, other, camp);
            }
        }

        /// <summary>A creature sent across each link, by the server's own movement, arrives on the far side, and back.</summary>
        [TestMethod]
        [DataRow(0, false)] [DataRow(0, true)]
        [DataRow(1, false)] [DataRow(1, true)]
        public void ACreatureWalksEachRetreadCavesLink(int link, bool back)
        {
            var nav = Query(RetreadCaves);
            var (camp, other) = CavesLinks[link];
            var from = nav.Nearest(back ? other : camp).Value;
            var to = nav.Nearest(back ? camp : other).Value;

            AssertWalks(nav, 1694, RetreadCaves, from, to);
        }

        /// <summary>And from the Turpis Refinery's arrival out of the entry pocket, through the tunnel mouth, to Charlie.</summary>
        [TestMethod]
        public void ACreatureWalksFromTheRefineryArrivalToCharlie()
        {
            var nav = Query(TurpisRefinery);

            AssertWalks(nav, 1429, TurpisRefinery, nav.Nearest(RefineryArrival).Value, nav.Nearest(HardDrives[2]).Value);
        }

        private static void AssertRoute(NavMeshQuery nav, Vector3 start, Vector3 end)
        {
            var from = nav.NearestInColumn(start, 4f);
            var to = nav.NearestInColumn(end, 4f);

            Assert.IsNotNull(from, $"{start} is on the mesh");
            Assert.IsNotNull(to, $"{end} is on the mesh");
            Assert.AreEqual(true, nav.Reachable(from.Value, to.Value), $"{start} reaches {end}");

            var route = nav.FindPath(from.Value, to.Value, out var complete);

            Assert.IsNotNull(route);
            Assert.IsTrue(complete, $"a whole route from {start} to {end}");
        }

        private static void AssertWalks(NavMeshQuery nav, uint mapContextId, string mapName, Vector3 from, Vector3 to)
        {
            using var world = new WorldTestContext();
            world.Map.MapInfo = new MapInfo(mapContextId, mapName, 1, 0);
            world.Map.NavMesh = nav;
            var actor = new Creature
            {
                Name = "Navmesh joins fixture", EntityClass = EntityClasses.HumanBaseMale,
                State = CharacterState.Idle, MapContextId = mapContextId, RuntimeMapChannel = world.Map,
                Position = from, RunSpeed = 4f, WalkSpeed = 4f, AggroRange = 0,
                AppearanceData = new Dictionary<EquipmentData, AppearanceData>()
            };
            actor.HomePos.Position = from;
            actor.HomePos.MapContextid = mapContextId;
            actor.Attributes[Attributes.Health] = new ActorAttributes(Attributes.Health, 100, 100, 100, 0, 0);
            BehaviorManager.StartWandering(actor, false);
            EntityManager.Instance.RegisterActor(actor.EntityId, actor);
            CellManager.Instance.AddToWorld(world.Map, actor);

            try
            {
                Assert.IsTrue(BehaviorManager.Instance.SetActionScriptedMove(world.Map, actor, to, 0));

                for (var tick = 0; tick < 20000 && !actor.Controller.ScriptedMove.Arrived; tick++)
                    BehaviorManager.Instance.MapChannelThink(world.Map, 250);

                Assert.IsTrue(actor.Controller.ScriptedMove.Arrived, $"from {from} to {to}: stopped at {actor.Position}");
                Assert.IsLessThan(.5f, Vector3.Distance(to, actor.Position));
            }
            finally
            {
                CellManager.Instance.RemoveCreatureFromWorld(world.Map, actor);
                EntityManager.Instance.UnregisterEntity(actor.EntityId);
                EntityManager.Instance.UnregisterCreature(actor.EntityId);
                EntityManager.Instance.UnregisterActor(actor.EntityId);
                EntityManager.Instance.FreeEntity(actor.EntityId);
            }
        }

        internal static NavMeshQuery Query(string map) => TestNavMeshes.Query(NavFile(map));

        internal static string NavFile(string map)
        {
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (root != null && !File.Exists(Path.Combine(root.FullName, "Rasa.NET.sln")))
                root = root.Parent;
            return Path.Combine(root?.FullName ?? throw new AssertFailedException("Repository not found."), "navmesh", map + ".nav");
        }
    }

    /// <summary>
    /// A creature is not put on a pocket of mesh buried under the terrain (NavMeshManager.GroundOverPocket), against
    /// the Valverde Marshes' shipped navmesh: the Retread City pond's bed, 3 m under its bank, joined to nothing.
    /// </summary>
    [TestClass, DoNotParallelize]
    public class BuriedPocketPlacementTests
    {
        private static readonly Vector3 MechAt = new(-196f, 213f, -670f);

        [TestMethod]
        public void ACreaturePutOnThePondBedGoesUpOnTheBank()
        {
            using var world = Marshes();
            var map = world.Map;
            var nav = map.NavMesh;

            var nearest = NavMeshManager.NearestWalkable(map, MechAt).Value;
            Assert.IsTrue(nav.IsUnderground(nearest) && nearest.Y < 214f, $"the nearest walkable point is the pond's bed: {nearest}");
            Assert.IsLessThan(NavMeshManager.UnstickPocketArea, nav.RegionArea(nearest).Value);

            var placed = NavMeshManager.NearestStandable(map, MechAt).Value;
            Assert.IsFalse(nav.IsUnderground(placed), $"{placed} is on the bank");
            Assert.IsLessThan(8f, Vector2.Distance(new Vector2(placed.X, placed.Z), new Vector2(MechAt.X, MechAt.Z)), $"{placed}: beside where it was");
            Assert.IsTrue(placed.Y > 215.5f && placed.Y < 218f, $"{placed}: the bank, not a rock's top over it");
            Assert.IsGreaterThan(NavMeshManager.UnstickPocketArea, nav.RegionArea(placed).Value, "a floor of its own, not another pocket");

            Assert.IsNull(Spacing.Placed(map, nearest), "an escort's place is never the pond's bed");
            Assert.AreEqual(placed, NavMeshManager.GroundOverPocket(map, nearest, MechAt));
        }

        [TestMethod]
        public void OnTheGroundACreatureIsPutWhereItWouldBeAnyway()
        {
            using var world = Marshes();
            var map = world.Map;
            var bank = NavMeshManager.NearestStandable(map, MechAt).Value;

            Assert.AreEqual(NavMeshManager.NearestWalkable(map, bank), NavMeshManager.NearestStandable(map, bank));
            Assert.IsNull(NavMeshManager.GroundOverPocket(map, bank));
            Assert.AreEqual(bank, Spacing.Placed(map, bank));
        }

        private static WorldTestContext Marshes()
        {
            var world = new WorldTestContext();
            world.Map.MapInfo = new MapInfo(1454, "adv_foreas_valverde_marshes", 1, 0);
            world.Map.NavMesh = NavMeshJoinsTests.Query("adv_foreas_valverde_marshes");
            var (lowest, highest) = world.Map.NavMesh.HeightRange().Value;
            world.Map.SafetyFloorY = SafetyFloor.FloorFor(lowest);
            world.Map.TopWalkableY = highest;
            return world;
        }
    }
}
