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
using Rasa.Structures;
using Rasa.Structures.World;
using Rasa.Test.Missions.Wilderness;

namespace Rasa.Test.Missions
{
    using M = Rasa.Services.Preloader.InstanceMarkerMaps;

    /// <summary>
    /// The instance missions' markers that were shown on the wrong map or on none (Instance_marker_maps,
    /// InstanceMarkerMaps): each sent on its map and standing on that map's ground; the two scenes put in
    /// run nothing and change no rule; Down gives the scenes back as they were.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class InstanceMarkerMapsTests
    {
        private const string Migration = "20271113000000_Instance_marker_maps", Previous = "20271112000000_Devils_den_markers";

        [TestMethod]
        public void EachMarkerIsSentOnItsMapAndStandsOnItsGround()
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
                    var objective = catalog.Missions[m.MissionId].Objectives[m.Objective];
                    var pin = objective.Indicators.Single(i => i.IndicatorId == m.Indicator);
                    var what = $"{m.MissionId}/{m.Objective}/{m.Indicator}";

                    Assert.AreEqual(m.Map, MissionMarkerProjection.ResolveMap(catalog, m.MissionId, m.Objective, pin), what);
                    var ground = nav.NearestInColumn(pin.Position, 60f);
                    Assert.IsNotNull(ground, $"{what}: no ground on {m.Map}");
                    Assert.IsLessThan(2.1f, new Vector2(ground.Value.X - pin.Position.X, ground.Value.Z - pin.Position.Z).Length(), what);
                    Assert.IsLessThan(1.5f, Math.Abs(ground.Value.Y - pin.Position.Y), $"{what}: ground {ground.Value.Y}");
                }
            }

            // Every other marker of these missions still has a map.
            foreach (var missionId in M.Markers.Select(m => m.MissionId).Distinct())
                foreach (var objective in catalog.Missions[missionId].Objectives.Values)
                    foreach (var pin in objective.Indicators)
                        Assert.AreNotEqual(0U, MissionMarkerProjection.ResolveMap(catalog, missionId, objective.ObjectiveId, pin),
                            $"{missionId}/{objective.ObjectiveId}/{pin.IndicatorId}");
        }

        [TestMethod]
        public void TheScenesPutInRunNothingAndChangeNoRule()
        {
            using var harness = WildernessAliaBranchesTests.CreateHarness();
            var catalog = new MissionContentCatalog(harness, new Dictionary<uint, Mission>(), null);
            catalog.Load();

            foreach (var missionId in M.NewScenes)
            {
                var scene = catalog.SceneBindings[missionId];
                Assert.IsNull(scene.Script, $"{missionId}");
                Assert.IsEmpty(scene.Actors, $"{missionId}");
                Assert.IsEmpty(scene.Sequences, $"{missionId}");
                var mission = catalog.Missions[missionId];
                Assert.IsTrue(mission.IsOperational, $"{missionId}");
                Assert.IsNull(mission.Requirement, $"{missionId}");
                Assert.IsNull(mission.TurnInRequirement, $"{missionId}");
                Assert.IsEmpty(mission.Items, $"{missionId}");
            }
        }

        [TestMethod]
        public void UpAgainChangesNothingAndDownGivesTheScenesBack()
        {
            using var database = new Rasa.Test.Database.OrdinaryLootTestDatabase();
            var migrator = database.Context.GetService<IMigrator>();
            var missions = M.Markers.Select(m => m.MissionId).Distinct().ToArray();
            Dictionary<(uint, string), string> Scenes() => database.Context.Set<MissionSceneBindingEntry>().AsNoTracking()
                .Where(b => missions.Contains(b.MissionId)).ToList().ToDictionary(b => (b.MissionId, b.ContentRevision), b => b.Bindings);

            migrator.Migrate(Previous);
            var before = Scenes();
            foreach (var missionId in M.NewScenes)
                Assert.IsFalse(before.Keys.Any(k => k.Item1 == missionId), $"{missionId} had no scene");

            migrator.Migrate(Migration);
            var after = Scenes();
            Assert.AreEqual(before.Count + M.NewScenes.Length, after.Count);
            foreach (var missionId in missions)
                StringAssert.Contains(after.Single(k => k.Key.Item1 == missionId).Value, M.Key, $"{missionId}");
            StringAssert.Contains(after.Single(k => k.Key.Item1 == 1056).Value, M.ReclaimingEntryWithGenerator);

            foreach (var sql in M.Up(mySql: false))
                database.Context.Database.ExecuteSqlRaw(sql.Replace("{", "{{").Replace("}", "}}"));
            CollectionAssert.AreEquivalent(after.ToList(), Scenes().ToList(), "Up again");

            migrator.Migrate(Previous);
            CollectionAssert.AreEquivalent(before.ToList(), Scenes().ToList(), "Down");
        }

        [TestMethod]
        public void OnMySqlTheMapsGoInAndComeOut()
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
    }
}
