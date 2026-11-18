using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Rasa.Managers;
using Rasa.Navigation;
using Rasa.Services.Preloader;
using Rasa.Structures;
using Rasa.Structures.Char;
using Rasa.Structures.World;
using Rasa.Test.Missions.Wilderness;

namespace Rasa.Test.Missions
{
    using M = InstanceInsideMarkers;

    /// <summary>
    /// The objectives marked only at their instance's entrance (Instance_inside_markers,
    /// InstanceInsideMarkers): each marker in the instance sent there and standing on its ground by
    /// what it marks, the entrance's still sent outside; Up again changes nothing and Down gives the
    /// markers and the scenes back as they were.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class InstanceInsideMarkersTests
    {
        private const string Migration = "20271203000000_Instance_inside_markers", Previous = "20271202000000_Crater_lake_markers";

        /// <summary>The entrance's marker of each, and the map it is sent on.</summary>
        private static readonly (uint MissionId, uint Objective, uint Indicator, uint Map)[] Entrances =
        {
            (MinosCavernsMissions.RetrievalForRecon, 1, MinosCavernsMissions.EntranceMarker, MinosCavernsContent.Divide),
            (MinosCavernsMissions.WarnetBarrier, 1, MinosCavernsMissions.EntranceMarker, MinosCavernsContent.Divide),
            (PravusResearchContent.Escapist, 1, PravusResearchContent.FacilityEntranceMarker, PravusResearchContent.Wilderness),
            (OjasaHiveContent.FriedAtta, 1, 278, OjasaHiveContent.Incline),
            (WarnetCavernsMissions.Scarecrow, 1, WarnetCavernsMissions.EntranceMarker, WarnetCavernsContent.Palisades),
            (WarnetCavernsMissions.TrophyCase, 2, WarnetCavernsMissions.EntranceMarker, WarnetCavernsContent.Palisades)
        };

        [TestMethod]
        public void EachMarkerIsSentInItsInstanceAndStandsOnItsGroundByWhatItMarks()
        {
            using var harness = WildernessAliaBranchesTests.CreateHarness();
            var catalog = new MissionContentCatalog(harness, new Dictionary<uint, Mission>(), null);
            catalog.Load();
            var names = harness.World.Set<MapInfoEntry>().AsNoTracking().ToList().ToDictionary(m => m.Id, m => m.MapName);

            foreach (var group in M.Markers.GroupBy(m => m.Map))
            {
                var nav = new NavMeshQuery(NavMeshFile.Read(NavMeshFile.PathFor(NavMeshDirectory(), names[group.Key])));
                foreach (var m in group)
                {
                    var what = $"{m.MissionId}/{m.Objective}/{m.Indicator} {m.Where}";
                    var pin = catalog.Missions[m.MissionId].Objectives[m.Objective].Indicators.Single(i => i.IndicatorId == m.Indicator);
                    Assert.AreEqual(m.Map, MissionMarkerProjection.ResolveMap(catalog, m.MissionId, m.Objective, pin), what);
                    Assert.AreEqual(m.Radius, pin.Radius, what);
                    var ground = nav.NearestInColumn(pin.Position, 20f);
                    Assert.IsNotNull(ground, $"{what}: no ground");
                    Assert.IsLessThan(0.5f, new Vector2(ground.Value.X - pin.Position.X, ground.Value.Z - pin.Position.Z).Length(), what);
                    Assert.IsLessThan(1.5f, Math.Abs(ground.Value.Y - pin.Position.Y), $"{what}: ground {ground.Value.Y}");
                }
            }

            // What each marks is inside its radius: the people on the spot, the nests and the pools by it.
            static void Within(M.Marker m, double x, double z) =>
                Assert.IsLessThanOrEqualTo(m.Radius, Vector2.Distance(new Vector2(m.At.X, m.At.Z), new Vector2((float)x, (float)z)), m.Where);
            foreach (var nest in MinosCavernsContent.Nests)
                Assert.IsTrue(M.Markers.Where(m => m.MissionId == MinosCavernsMissions.WarnetBarrier)
                    .Any(m => Vector2.Distance(new Vector2(m.At.X, m.At.Z), new Vector2((float)nest.X, (float)nest.Z)) <= m.Radius), nest.Where);
            var pools = harness.World.Set<SpawnPoolEntry>().AsNoTracking().Where(p => p.MapContextId == M.WarnetCaverns).ToList();
            foreach (var m in M.Markers.Where(m => m.Map == M.WarnetCaverns))
                Assert.IsTrue(pools.Any(p => Vector2.Distance(new Vector2(m.At.X, m.At.Z), new Vector2((float)p.PosX, (float)p.PosZ)) < 1), m.Where);
            Within(M.Markers.Single(m => m.MissionId == MinosCavernsMissions.RetrievalForRecon), MinosCavernsMissions.TylerSpot.X, MinosCavernsMissions.TylerSpot.Z);

            // The entrances' markers are still sent outside.
            foreach (var (missionId, objective, indicator, map) in Entrances)
            {
                var pin = catalog.Missions[missionId].Objectives[objective].Indicators.Single(i => i.IndicatorId == indicator);
                Assert.AreEqual(map, MissionMarkerProjection.ResolveMap(catalog, missionId, objective, pin), $"{missionId}/{objective}/{indicator}");
            }
        }

        [TestMethod]
        public void InTheCavernsTheNestsAndTylerAreMarkedAndOnTheDivideTheMouth()
        {
            using var harness = Create(MinosCavernsContent.Map, MinosCavernsMissions.Revision);
            var foster = Put(harness, MinosCavernsContent.Foster, MinosCaverns.Entrance + new Vector3(3, 0, 0));
            WildernessTwinPillarsTests.Accept(harness, foster, MinosCavernsMissions.WarnetBarrier);
            var sebastian = Put(harness, MinosCavernsContent.Sebastian, MinosCaverns.Entrance + new Vector3(3, 0, 0));
            WildernessTwinPillarsTests.Accept(harness, sebastian, MinosCavernsMissions.RetrievalForRecon);

            var inside = Shown(harness, MinosCavernsContent.Map);
            CollectionAssert.AreEquivalent(Ids(MinosCavernsMissions.WarnetBarrier), inside[MinosCavernsMissions.WarnetBarrier]);
            CollectionAssert.AreEquivalent(Ids(MinosCavernsMissions.RetrievalForRecon), inside[MinosCavernsMissions.RetrievalForRecon]);

            var outside = Shown(harness, MinosCavernsContent.Divide);
            CollectionAssert.AreEquivalent(new[] { MinosCavernsMissions.EntranceMarker }, outside[MinosCavernsMissions.WarnetBarrier]);
            CollectionAssert.AreEquivalent(new[] { MinosCavernsMissions.EntranceMarker }, outside[MinosCavernsMissions.RetrievalForRecon]);
        }

        [TestMethod]
        public void InTheWarnetCavernsTheMiasmaAndTheWarnetsAreMarkedAndOnThePalisadesTheMouth()
        {
            using var harness = Create(WarnetCavernsContent.Map, WarnetCavernsMissions.Revision);
            var barrasso = Put(harness, WarnetCavernsMissions.Barrasso, harness.Client.Player.Position + new Vector3(2, 0, 0));
            WildernessTwinPillarsTests.Accept(harness, barrasso, WarnetCavernsMissions.Scarecrow);
            var mullen = Put(harness, WarnetCavernsMissions.Mullen, harness.Client.Player.Position + new Vector3(2, 0, 0));
            WildernessTwinPillarsTests.Accept(harness, mullen, WarnetCavernsMissions.TrophyCase);

            var inside = Shown(harness, WarnetCavernsContent.Map);
            CollectionAssert.AreEquivalent(Ids(WarnetCavernsMissions.Scarecrow), inside[WarnetCavernsMissions.Scarecrow]);
            CollectionAssert.AreEquivalent(Ids(WarnetCavernsMissions.TrophyCase), inside[WarnetCavernsMissions.TrophyCase]);

            var outside = Shown(harness, WarnetCavernsContent.Palisades);
            CollectionAssert.AreEquivalent(new[] { WarnetCavernsMissions.EntranceMarker }, outside[WarnetCavernsMissions.Scarecrow]);
            CollectionAssert.AreEquivalent(new[] { WarnetCavernsMissions.EntranceMarker }, outside[WarnetCavernsMissions.TrophyCase]);
        }

        [TestMethod]
        public void UpAgainChangesNothingAndDownGivesTheMarkersAndScenesBack()
        {
            using var database = new Rasa.Test.Database.OrdinaryLootTestDatabase();
            var migrator = database.Context.GetService<IMigrator>();
            var missions = M.Markers.Select(m => m.MissionId).Distinct().ToArray();
            Dictionary<(uint, string), string> Scenes() => database.Context.Set<MissionSceneBindingEntry>().AsNoTracking()
                .Where(b => missions.Contains(b.MissionId)).ToList().ToDictionary(b => (b.MissionId, b.ContentRevision), b => b.Bindings);
            List<string> Indicators() => database.Context.Set<MissionIndicatorEntry>().AsNoTracking()
                .Where(i => missions.Contains(i.MissionId)).ToList()
                .Select(i => $"{i.MissionId}/{i.ContentRevision}/{i.ObjectiveId}/{i.IndicatorId} {i.PosX:0.##},{i.PosY:0.##},{i.PosZ:0.##} r{i.Radius}").ToList();

            migrator.Migrate(Previous);
            var scenes = Scenes();
            var indicators = Indicators();
            foreach (var missionId in M.AppendedScenes)
                StringAssert.DoesNotMatch(scenes.Single(s => s.Key.Item1 == missionId).Value, new System.Text.RegularExpressions.Regex("indicatorMaps"));
            foreach (var (missionId, was, _) in M.Entries())
                StringAssert.Contains(scenes.Single(s => s.Key.Item1 == missionId).Value, was);

            migrator.Migrate(Migration);
            var after = Scenes();
            var afterIndicators = Indicators();
            Assert.AreEqual(indicators.Count + M.Markers.Length, afterIndicators.Count);
            foreach (var missionId in M.AppendedScenes)
                StringAssert.EndsWith(after.Single(s => s.Key.Item1 == missionId).Value, M.Maps(missionId) + "}");
            foreach (var (missionId, _, now) in M.Entries())
                StringAssert.Contains(after.Single(s => s.Key.Item1 == missionId).Value, now);

            foreach (var sql in M.Up(mySql: false))
                database.Context.Database.ExecuteSqlRaw(sql.Replace("{", "{{").Replace("}", "}}"));
            CollectionAssert.AreEquivalent(after.ToList(), Scenes().ToList(), "Up again");
            CollectionAssert.AreEquivalent(afterIndicators, Indicators(), "Up again");

            migrator.Migrate(Previous);
            CollectionAssert.AreEquivalent(scenes.ToList(), Scenes().ToList(), "Down");
            CollectionAssert.AreEquivalent(indicators, Indicators(), "Down");
        }

        [TestMethod]
        public void OnMySqlTheMarkersGoInAndComeOut()
        {
            using var context = Rasa.Test.Database.PersistenceIntegrationTests.CreateContext(typeof(Rasa.Context.World.MySqlWorldContext), "unused");
            var migrator = context.GetService<IMigrator>();
            var up = migrator.GenerateScript(Previous, Migration);
            var down = migrator.GenerateScript(Migration, Previous);

            foreach (var sql in M.Up(mySql: true))
                StringAssert.Contains(up, sql);
            StringAssert.Contains(up, "CONCAT(");
            foreach (var sql in M.Down())
                StringAssert.Contains(down, sql);
        }

        #region Helpers

        private static uint[] Ids(uint missionId) => M.Markers.Where(m => m.MissionId == missionId).Select(m => m.Indicator).ToArray();

        /// <summary>The markers sent for the objective this marks of each mission here, on a map.</summary>
        private static Dictionary<uint, uint[]> Shown(WildernessRuntimeTestHarness harness, uint map)
        {
            var player = harness.Client.Player;
            var was = player.MapContextId;
            try
            {
                player.MapContextId = map;
                return harness.Manager.BuildStatusSnapshot(player)
                    .Where(entry => M.Markers.Any(m => m.MissionId == entry.Key))
                    .ToDictionary(entry => entry.Key, entry => entry.Value.ObjectivesList
                        .Where(o => o.ObjectiveId == M.Markers.First(m => m.MissionId == entry.Key).Objective)
                        .SelectMany(o => o.IndicatorList.Select(i => i.IndicatorId)).ToArray());
            }
            finally
            {
                player.MapContextId = was;
            }
        }

        private static WildernessRuntimeTestHarness Create(uint map, string revision)
        {
            var harness = WildernessRuntimeTestHarness.Create(mapContextId: map);
            try
            {
                using var unit = harness.CreateChar();
                unit.ExecuteTransaction(() => unit.CharacterStartingExperience.Add(
                    new CharacterStartingExperienceEntry(harness.Client.Player.Id, revision, CharacterStartingExperienceState.Skipped)));
                harness.Client.Player.StartingExperienceCompleted = true;
                harness.Drain();
                return harness;
            }
            catch
            {
                harness.Dispose();
                throw;
            }
        }

        private static Creature Put(WildernessRuntimeTestHarness harness, uint dbId, Vector3 position)
        {
            var creature = CreatureManager.Instance.CreateCreature(dbId, null);
            CreatureManager.Instance.SetLocation(creature, NavMeshManager.SnapToGround(harness.Map, position), 0, harness.Map.MapInfo.MapContextId);
            CellManager.Instance.AddToWorld(harness.Map, creature);
            return creature;
        }

        private static string NavMeshDirectory()
        {
            for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            {
                var navmesh = Path.Combine(dir.FullName, "navmesh");
                if (Directory.Exists(navmesh))
                    return navmesh;
            }

            throw new DirectoryNotFoundException("navmesh");
        }

        #endregion
    }
}
