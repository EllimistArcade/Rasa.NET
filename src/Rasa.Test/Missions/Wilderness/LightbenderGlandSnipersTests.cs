using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Rasa.Services.Preloader;
using Rasa.Test.Database;

namespace Rasa.Test.Missions.Wilderness
{
    /// <summary>Lightbender Glands' glands drop from the Light Bender Snipers too (Lightbender_gland_snipers, LightbenderGlandSnipers).</summary>
    [TestClass]
    [DoNotParallelize]
    public class LightbenderGlandSnipersTests
    {
        private const string Migration = "20271204000000_Lightbender_gland_snipers";
        private const string Previous = "20271203000000_Instance_inside_markers";

        [TestMethod]
        public void TheDropNamesTheSnipersAndDownTakesThemOff()
        {
            using var database = new OrdinaryLootTestDatabase();
            var migrator = database.Context.GetService<IMigrator>();

            migrator.Migrate(Previous);
            var before = Bindings(database);
            var others = Others(database);
            StringAssert.Contains(before, "\"drop\":{\"creatureIds\":[630071],\"objectiveId\":3,\"mapContextId\":1220,\"chancePercent\":100,\"quantity\":1}");

            migrator.Migrate(Migration);
            var after = Bindings(database);
            StringAssert.Contains(after, "\"drop\":{\"creatureIds\":[630071,630046],\"objectiveId\":3,\"mapContextId\":1220,\"chancePercent\":100,\"quantity\":1}");
            Assert.AreEqual(before.Replace("[630071]", "[630071,630046]"), after, "nothing else changes");
            CollectionAssert.AreEqual(others, Others(database), "nor any other mission's scene");

            foreach (var sql in LightbenderGlandSnipers.Up())
                Assert.AreEqual(0, database.Context.Database.ExecuteSqlRaw(sql.Replace("{", "{{").Replace("}", "}}")), "twice");

            migrator.Migrate(Previous);
            Assert.AreEqual(before, Bindings(database), "Down");
        }

        [TestMethod]
        public void OnMySqlTheDropChangesAndChangesBack()
        {
            using var context = PersistenceIntegrationTests.CreateContext(typeof(Rasa.Context.World.MySqlWorldContext), "unused");
            var migrator = context.GetService<IMigrator>();

            foreach (var sql in LightbenderGlandSnipers.Up())
                StringAssert.Contains(migrator.GenerateScript(Previous, Migration), sql);
            foreach (var sql in LightbenderGlandSnipers.Down())
                StringAssert.Contains(migrator.GenerateScript(Migration, Previous), sql);
        }

        private static string Bindings(OrdinaryLootTestDatabase database) =>
            database.Context.Database.SqlQueryRaw<string>(
                    $"SELECT bindings AS Value FROM mission_scene_binding WHERE mission_id = {LightbenderGlandSnipers.MissionId} AND content_revision = '{LightbenderGlandSnipers.Revision}'")
                .AsEnumerable().Single();

        private static string[] Others(OrdinaryLootTestDatabase database) =>
            database.Context.Database.SqlQueryRaw<string>(
                    $"SELECT bindings AS Value FROM mission_scene_binding WHERE mission_id <> {LightbenderGlandSnipers.MissionId} ORDER BY mission_id, content_revision")
                .AsEnumerable().ToArray();
    }
}
