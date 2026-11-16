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
    using B = Rasa.Services.Preloader.BrannWaterRefineryContent;

    /// <summary>
    /// The navmeshes Rasa.NavMesh's MapNavFixes rebuilt, against the shipped files: the parts of a map
    /// that no path joined before - which an escort, a guard or a creature after a player could not cross -
    /// are joined, both ways, and a creature walks the Retread Caves' and the Brann Water Refinery's off-mesh links;
    /// six private instances the 0.4 m grid cut apart between their rooms, and seven the heightmap cut apart over a
    /// tunnel's mouth, are one walkable region each; and a creature walks the links added on three of those.
    /// </summary>
    [TestClass, DoNotParallelize]
    public class NavMeshJoinsTests
    {
        private const string TurpisRefinery = "adv_foreas_valverde_marshes_wetlandrefinery";
        private const string SupplyDepot = "adv_foreas_valverde_marshes_banesupplydepot";
        private const string RetreadCaves = "adv_foreas_valverde_pools_retread_caves";
        private const string Marshes = "adv_foreas_valverde_marshes";
        private const string VillageRuins = "adv_foreas_valverde_marshes_villageruins";
        private const string WaterRefinery = "adv_arieki_torden_plains_brannwaterrefinery";

        // Brann Water Refinery (2093): its missions' route, in order (BrannWaterRefineryContent), to the last crate
        // and back to the way in; and the two links, each side.
        private static readonly Vector3[] RefineryRoute =
        {
            B.Arrival, B.MelokanAt, B.KillroyAt, B.JenksAt, B.Modules[0].At, B.CaolotsAt, B.Modules[1].At, B.Modules[2].At,
            B.ArbocAt, B.Bombs[0].At, B.Bombs[3].At, B.Bombs[6].At, B.Bombs[8].At, B.Bombs[9].At, B.CoenAt, B.NamistaiAt,
            B.SkybridgeMiddle, B.NorthernQuarters, B.FalloraksAt, B.SouthernQuarters, B.IfilionAt, B.Greenhouse, B.KraxorAt,
            new Vector3(-284f, 399.6f, -259f), B.Arrival
        };
        // Private instances the 0.4 m grid cut apart between their rooms, joined by the fine grid alone: the map link's
        // arrival, then one pool from each part that held pools on the shipped mesh.
        private static readonly Dictionary<string, Vector3[]> FineJoined = new()
        {
            ["adv_foreas_concordia_wilderness_cavesofdonn02"] = new Vector3[]
                { new(-275.6f, 24.25f, -374.1f), new(-160.0f, 32.45f, -307.0f), new(-84.0f, 17.87f, -296.0f), new(150.0f, 1.51f, -302.0f) },
            ["adv_foreas_concordia_palisades_devilsden"] = new Vector3[]
                { new(16.8f, 122.93f, -398.2f), new(-80.3f, 169.96f, 327.5f), new(310.4f, 128.02f, 461.4f), new(14.6f, 107.49f, -376.5f), new(48.0f, 78.35f, -100.0f) },
            ["adv_foreas_concordia_divide_purgasstation2"] = new Vector3[]
                { new(87.8f, 65.93f, 221.6f), new(-268.9f, 32.13f, -147.2f), new(-159.5f, 8.53f, -218.2f) },
            ["adv_arieki_ligo_thunderhead_rivasaattacolony"] = new Vector3[]
                { new(36.8f, 152.40f, -453.9f), new(-60.8f, 65.36f, 392.2f) },
            ["adv_foreas_concordia_wilderness_clrf"] = new Vector3[]
                { new(8.5f, 140.24f, 158.3f), new(67.6f, 127.05f, -136.8f) },
            ["adv_arieki_ligo_ashendesert_indracaverns"] = new Vector3[]
                { new(-40.7f, 9.18f, -421.6f), new(-320.7f, 128.98f, -22.6f), new(-85.8f, 65.23f, -117.7f) },
            // Tahrendra Base: the arrival, the Guard Lounge, the barracks' doors, the three security stations, the
            // AFS teleporter, the wormhole room's mark and a balcony (TahrendraBaseContent).
            ["adv_arieki_torden_mires_tahrendrabase"] = new Vector3[]
            {
                new(196f, 120.6f, 10.5f), new(28f, 120.6f, -75f), new(15f, 120.6f, 52f), new(31f, 120.6f, -28f), new(-76f, 120.2f, 48.6f),
                new(-89f, 120.2f, 61f), new(-2f, 80.3f, 72f), new(-46f, 80.6f, 70f), new(-134f, 144.4f, -64f), new(-150f, 112.2f, 12f),
                new(-166f, 120.6f, -16f)
            }
        };

        // Private instances the heightmap cut apart where it runs over a tunnel's mouth or ramp (MapNavFixes'
        // terrain cuts): the map link's arrival, then one pool from each part that held pools on the shipped mesh.
        private static readonly Dictionary<string, Vector3[]> MouthsJoined = new()
        {
            ["adv_foreas_concordia_divide_timoramines"] = new Vector3[]
                { new(-120.0f, 205.75f, 546.9f), new(-128.8f, 205.74f, 517.6f), new(308.4f, 205.11f, -250.7f) },
            ["adv_foreas_concordia_wilderness_pravusresearch"] = new Vector3[]
            {
                new(-193.5f, 10.39f, -257.4f), new(-237.9f, 6.94f, -125.2f), new(144.2f, 10.42f, 151.4f), new(-145.4f, 30.72f, 17.9f),
                new(-71.2f, 39.34f, -188.0f), new(-41.5f, 41.34f, -242.6f), new(-185.0f, 5.14f, -123.0f), new(3.4f, 33.42f, -15.0f),
                new(35.0f, 46.01f, -60.0f)
            },
            ["adv_arieki_ligo_ashendesert_baneconscriptfacility"] = new Vector3[]
                { new(7.2f, 153.67f, -669.6f), new(7.3f, 153.47f, -636.7f), new(-233.5f, 96.76f, -145.2f), new(22.6f, 136.87f, -433.4f) },
            ["adv_arieki_ligo_thunderhead_quassostation"] = new Vector3[]
                { new(9.9f, 46.37f, -197.1f), new(8.3f, 46.17f, -177.1f), new(-102.5f, 15.6f, 356.5f) },
            ["adv_foreas_howlingmaw_cuthahbase"] = new Vector3[]
                { new(-25.5f, 211.51f, -460.1f), new(64.0f, 204.93f, -531.5f), new(23.0f, 165.71f, -511.5f) },
            ["adv_arieki_torden_incline_ojasaattahive"] = new Vector3[]
            {
                new(553.7f, 88.54f, 146.5f), new(439.2f, 72.73f, 126.3f), new(185.7f, 126.28f, -273.0f), new(-326.5f, 128.38f, -30.4f),
                new(-95.6f, 120.29f, -390.6f), new(22.6f, 65.8f, -30.2f), new(-528.8f, 105.22f, -231.7f)
            },
            ["adv_arieki_torden_plains_attacolony"] = new Vector3[]
                { new(-25.7f, 312.46f, 184.0f), new(-12.9f, 313.73f, 181.3f), new(428.0f, 188.63f, 290.0f) }
        };

        // The links MapNavFixes adds there, each a point a few metres back on either side: down off the Pravus outpost
        // wall, up onto the Conscription Facility's loading dock, down off the Ojasa hive's ledge and its steep ramp.
        private static readonly (string Map, uint Context, Vector3 Up, Vector3 Down)[] MouthsLinks =
        {
            ("adv_foreas_concordia_wilderness_pravusresearch", 1430, new(2.6f, 34.6f, -18.5f), new(-2.0f, 32.1f, -15.5f)),
            ("adv_arieki_ligo_ashendesert_baneconscriptfacility", 1988, new(76.2f, 137.5f, -427.0f), new(73.0f, 136.8f, -431.5f)),
            ("adv_arieki_torden_incline_ojasaattahive", 1865, new(8.8f, 60.5f, -13.2f), new(2.6f, 56.2f, -13.3f)),
            ("adv_arieki_torden_incline_ojasaattahive", 1865, new(-248.0f, 128.4f, -32f), new(-258.5f, 121.5f, -32f))
        };

        private static readonly (Vector3 East, Vector3 West)[] RefineryLinks =
        {
            (new Vector3(-40.0f, 399.6f, -278.2f), new Vector3(-41.8f, 399.6f, -278.6f)),
            (new Vector3(-184.0f, 399.6f, -241.4f), new Vector3(-185.6f, 399.6f, -241.6f))
        };

        // Valverde Marshes (1454): the zone's side and the crate area's side of the 1.2 m passage on the tile edge
        // z = 28.8; the crate area's side and the temple's upper floor's side of the passage between them.
        private static readonly Vector3 ZoneSide = new(-349f, 217.55f, 29.6f);
        private static readonly Vector3 CrateAreaSide = new(-343f, 217.03f, 28.2f);
        private static readonly Vector3 CrateAreaInner = new(-252f, 228.08f, 119.8f);
        private static readonly Vector3 TempleUpperFloor = new(-240f, 228.08f, 119.8f);

        // Village Ruins (1743): the map link's arrival; the Healing Shaman's hospital in its hut.
        private static readonly Vector3 RuinsArrival = new(-138f, 63.17f, -360f);
        private static readonly Vector3 RuinsHospital = new(-117.8f, 60.17f, -273.4f);

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

        [TestMethod]
        public void MarshesZoneReachesTheCrateAreaAndTheTemplesUpperFloor()
        {
            var nav = Query(Marshes);

            foreach (var (from, to) in new[] { (ZoneSide, CrateAreaSide), (CrateAreaInner, TempleUpperFloor), (ZoneSide, TempleUpperFloor) })
            {
                AssertRoute(nav, from, to);
                AssertRoute(nav, to, from);
            }

            Assert.IsGreaterThan(1000000f, nav.RegionArea(nav.NearestInColumn(TempleUpperFloor, 4f).Value).Value, "on the zone's own mesh");
        }

        [TestMethod]
        public void VillageRuinsArrivalWalksToTheHospital()
        {
            var nav = Query(VillageRuins);

            AssertRoute(nav, RuinsArrival, RuinsHospital);
            AssertRoute(nav, RuinsHospital, RuinsArrival);

            var route = nav.FindPath(nav.NearestInColumn(RuinsArrival, 4f).Value, nav.NearestInColumn(RuinsHospital, 4f).Value, out _);
            var length = 0f;
            var at = nav.NearestInColumn(RuinsArrival, 4f).Value;
            foreach (var corner in route) { length += Vector3.Distance(at, corner); at = corner; }
            Assert.IsTrue(length is > 100f and < 130f, $"{length:0} m: through the village, not round it");
            Assert.IsGreaterThan(50000f, nav.RegionArea(nav.NearestInColumn(RuinsArrival, 4f).Value).Value, "not an 85 m² part of its own");
        }

        /// <summary>Every leg of the refinery's missions, from the way in through each room they send a player to, both ways.</summary>
        [TestMethod]
        public void BrannWaterRefineryRouteRunsFromTheWayInToTheLastCrate()
        {
            var nav = Query(WaterRefinery);

            for (var i = 1; i < RefineryRoute.Length; i++)
            {
                AssertRoute(nav, RefineryRoute[i - 1], RefineryRoute[i]);
                AssertRoute(nav, RefineryRoute[i], RefineryRoute[i - 1]);
            }
        }

        /// <summary>
        /// Caves of Donn, Devil's Den, Purgas Station, Rivasa Atta Colony, the Crater Lake Research Facility, Indra
        /// Caverns and Tahrendra Base: from the arrival, each part that held pools is on the arrival's walkable region
        /// now, and back.
        /// </summary>
        [TestMethod]
        [DataRow("adv_foreas_concordia_wilderness_cavesofdonn02")]
        [DataRow("adv_foreas_concordia_palisades_devilsden")]
        [DataRow("adv_foreas_concordia_divide_purgasstation2")]
        [DataRow("adv_arieki_ligo_thunderhead_rivasaattacolony")]
        [DataRow("adv_foreas_concordia_wilderness_clrf")]
        [DataRow("adv_arieki_ligo_ashendesert_indracaverns")]
        [DataRow("adv_arieki_torden_mires_tahrendrabase")]
        public void TheFineGridJoinsTheInstancesRooms(string map)
        {
            var nav = Query(map);
            var places = FineJoined[map];
            var arrival = nav.NearestInColumn(places[0], 4f);

            Assert.IsNotNull(arrival, $"{map}: the arrival is on the mesh");
            Assert.IsGreaterThan(1000f, nav.RegionArea(arrival.Value).Value, $"{map}: the arrival is not on a pocket of its own");

            foreach (var place in places.Skip(1))
            {
                var at = nav.NearestInColumn(place, 4f);

                Assert.IsNotNull(at, $"{map}: {place} is on the mesh");
                Assert.AreEqual(true, nav.Reachable(arrival.Value, at.Value), $"{map}: the arrival reaches {place}");
                Assert.AreEqual(true, nav.Reachable(at.Value, arrival.Value), $"{map}: {place} reaches the arrival");
            }
        }

        /// <summary>
        /// Timora Mines, Pravus Research, the Bane Conscription Facility, Quasso Station, Cuthah Base, the Ojasa Atta Hive
        /// and the Plains Atta Colony: from the arrival, each part that held pools is on the arrival's walkable region
        /// now, and back.
        /// </summary>
        [TestMethod]
        [DataRow("adv_foreas_concordia_divide_timoramines")]
        [DataRow("adv_foreas_concordia_wilderness_pravusresearch")]
        [DataRow("adv_arieki_ligo_ashendesert_baneconscriptfacility")]
        [DataRow("adv_arieki_ligo_thunderhead_quassostation")]
        [DataRow("adv_foreas_howlingmaw_cuthahbase")]
        [DataRow("adv_arieki_torden_incline_ojasaattahive")]
        [DataRow("adv_arieki_torden_plains_attacolony")]
        public void TheTunnelMouthsJoinTheInstancesRooms(string map)
        {
            var nav = Query(map);
            var places = MouthsJoined[map];
            var arrival = nav.NearestInColumn(places[0], 4f);

            Assert.IsNotNull(arrival, $"{map}: the arrival is on the mesh");

            foreach (var place in places.Skip(1))
            {
                var at = nav.NearestInColumn(place, 4f);

                Assert.IsNotNull(at, $"{map}: {place} is on the mesh");
                Assert.AreEqual(true, nav.Reachable(arrival.Value, at.Value), $"{map}: the arrival reaches {place}");
                Assert.AreEqual(true, nav.Reachable(at.Value, arrival.Value), $"{map}: {place} reaches the arrival");
            }
        }

        /// <summary>A creature walks each of those links, both ways.</summary>
        [TestMethod]
        [DataRow(0, false)] [DataRow(0, true)]
        [DataRow(1, false)] [DataRow(1, true)]
        [DataRow(2, false)] [DataRow(2, true)]
        [DataRow(3, false)] [DataRow(3, true)]
        public void ACreatureWalksEachTunnelMouthMapsLink(int link, bool back)
        {
            var (map, context, up, down) = MouthsLinks[link];
            var nav = Query(map);

            AssertWalks(nav, context, map, nav.Nearest(back ? down : up).Value, nav.Nearest(back ? up : down).Value);
        }

        /// <summary>A creature walks the refinery's two links: into the first hacking module's walk, and off the skybridge.</summary>
        [TestMethod]
        [DataRow(0, false)] [DataRow(0, true)]
        [DataRow(1, false)] [DataRow(1, true)]
        public void ACreatureWalksEachWaterRefineryLink(int link, bool back)
        {
            var nav = Query(WaterRefinery);
            var (east, west) = RefineryLinks[link];

            AssertWalks(nav, B.Refinery, WaterRefinery, nav.Nearest(back ? west : east).Value, nav.Nearest(back ? east : west).Value);
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
