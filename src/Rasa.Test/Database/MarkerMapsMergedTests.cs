using System.Collections.Generic;
using System.Linq;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Rasa.Services.Preloader;
using Rasa.Structures.World;

namespace Rasa.Test.Database
{
    /// <summary>
    /// Marker_maps_merged (MarkerMapsMerged): the scenes Mission_marker_maps, Devils_den_markers and
    /// Instance_marker_maps map end with every map the three name, in a world made from scratch and in
    /// one that had the two later ones first.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class MarkerMapsMergedTests
    {
        private const string Migration = "20271201000000_Marker_maps_merged", Previous = "20271130000000_Guardian_prominence";

        private static readonly (uint MissionId, string Revision)[] Scenes =
        {
            (381, DevilsDenContent.Revision), (670, DevilsDenContent.Revision), (672, DevilsDenContent.Revision), (1308, "pools_1_16_5")
        };

        private static Dictionary<(uint, string), string> Read(OrdinaryLootTestDatabase database) =>
            database.Context.Set<MissionSceneBindingEntry>().AsNoTracking().ToList()
                .Where(b => Scenes.Contains((b.MissionId, b.ContentRevision))).ToDictionary(b => (b.MissionId, b.ContentRevision), b => b.Bindings);

        private static void Run(OrdinaryLootTestDatabase database, IEnumerable<string> statements)
        {
            foreach (var sql in statements)
                database.Context.Database.ExecuteSqlRaw(sql.Replace("{", "{{").Replace("}", "}}"));
        }

        private static string Union(uint missionId) => missionId == 1308
            ? MissionMarkerMaps.Property(missionId)
            : DevilsDenMarkers.Key + DevilsDenMarkers.MapsJson(missionId);

        [TestMethod]
        public void TheFourScenesAreTheOnesMappedTwoWays()
        {
            CollectionAssert.AreEquivalent(Scenes, MarkerMapsMerged.Replacements().Select(r => (r.MissionId, r.Revision)).Distinct().ToArray());
        }

        [TestMethod]
        public void ANewWorldGetsEveryMapAndDownGivesItsScenesBack()
        {
            using var database = new OrdinaryLootTestDatabase();
            var migrator = database.Context.GetService<IMigrator>();

            migrator.Migrate(Previous);
            var before = Read(database);
            foreach (var (missionId, revision) in Scenes)
                StringAssert.Contains(before[(missionId, revision)], MissionMarkerMaps.Property(missionId), $"{missionId}, Mission_marker_maps' maps");

            migrator.Migrate(Migration);
            var after = Read(database);
            foreach (var (missionId, revision) in Scenes)
                StringAssert.Contains(after[(missionId, revision)], Union(missionId), $"{missionId}");
            StringAssert.Contains(after[(672, DevilsDenContent.Revision)], $"\"{DevilsDenMarkers.NodeCMarker}\":{DevilsDenContent.Den}", "the third node's marker in the Den");

            Run(database, MarkerMapsMerged.Up());
            CollectionAssert.AreEquivalent(after.ToList(), Read(database).ToList(), "Up again");

            migrator.Migrate(Previous);
            CollectionAssert.AreEquivalent(before.ToList(), Read(database).ToList(), "Down");
        }

        [TestMethod]
        public void AWorldThatHadTheDensAndTheInstancesMapsFirstEndsTheSame()
        {
            using var database = new OrdinaryLootTestDatabase();
            var migrator = database.Context.GetService<IMigrator>();
            migrator.Migrate(Migration);
            var merged = Read(database);
            migrator.Migrate(Previous);

            // As Mission_marker_maps leaves them where the other two ran first: theirs kept, the shrine's marker put in.
            var theirs = new Dictionary<uint, string>
            {
                [381] = DevilsDenMarkers.Key + DevilsDenMarkers.MapsJson(381),
                [670] = DevilsDenMarkers.Key + DevilsDenMarkers.MapsJson(670),
                [672] = DevilsDenMarkers.Key + DevilsDenMarkers.MapsJson(672),
                [1308] = InstanceMarkerMaps.Key + InstanceMarkerMaps.MapsJson(1308)
            };
            foreach (var (missionId, revision) in Scenes)
                database.Context.Database.ExecuteSqlRaw("UPDATE mission_scene_binding SET bindings = REPLACE(bindings, {0}, {1}) " +
                    "WHERE mission_id = {2} AND content_revision = {3}", MissionMarkerMaps.Property(missionId), theirs[missionId], missionId, revision);
            foreach (var (missionId, revision) in Scenes)
                StringAssert.Contains(Read(database)[(missionId, revision)], theirs[missionId]);

            Run(database, MarkerMapsMerged.Up());
            CollectionAssert.AreEquivalent(merged.ToList(), Read(database).ToList());
        }

        [TestMethod]
        public void OnMySqlTheStatementsAreInTheScript()
        {
            using var context = PersistenceIntegrationTests.CreateContext(typeof(Rasa.Context.World.MySqlWorldContext), "unused");
            var migrator = context.GetService<IMigrator>();
            var up = migrator.GenerateScript(Previous, Migration);
            var down = migrator.GenerateScript(Migration, Previous);

            Assert.HasCount(4, MarkerMapsMerged.Up().ToList());
            foreach (var sql in MarkerMapsMerged.Up())
                StringAssert.Contains(up, sql);
            Assert.HasCount(3, MarkerMapsMerged.Down().ToList());
            foreach (var sql in MarkerMapsMerged.Down())
                StringAssert.Contains(down, sql);
        }
    }
}
