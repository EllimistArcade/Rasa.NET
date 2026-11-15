using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Rasa.Services.Preloader;

namespace Rasa.Test.Database
{
    // The Caves of Donn's Chantry tunnel egg cluster out of the narrows before Undone Fairway (Donn_egg_cluster_narrows).
    [TestClass]
    public class DonnEggClusterNarrowsTests
    {
        private const string Migration = "20271125000000_Donn_egg_cluster_narrows";

        private static readonly string Row = $"FROM world_destructible WHERE id = {DonnEggClusterNarrows.Now.Id}";

        private static readonly string Marker =
            $"FROM mission_indicator WHERE mission_id = {DonnEggClusterNarrows.Mission} AND content_revision = '{DonnEggClusterNarrows.Revision}' " +
            $"AND objective_id = {DonnEggClusterNarrows.Objective} AND indicator_id = {DonnEggClusterNarrows.Marker}";

        [TestMethod]
        public void UpMovesTheClusterAndItsMarkerAndDownPutsThemBack()
        {
            using var database = new OrdinaryLootTestDatabase();
            var migrator = database.Context.GetService<IMigrator>();
            var previous = Previous(database);

            // As a World that put the Caves of Donn in with the cluster in the narrows has it.
            migrator.Migrate(previous);
            foreach (var sql in DonnEggClusterNarrows.Down())
                Exec(database, sql);
            At(database, DonnEggClusterNarrows.Was.X, DonnEggClusterNarrows.Was.Z);

            migrator.Migrate(Migration);
            At(database, DonnEggClusterNarrows.Now.X, DonnEggClusterNarrows.Now.Z);
            Assert.AreEqual(DonnEggClusterNarrows.Now.Y, Double(database, "SELECT pos_y AS Value " + Row), 1e-3);

            foreach (var sql in DonnEggClusterNarrows.Up())
                Assert.AreEqual(0, Exec(database, sql), "moved already: nothing");

            migrator.Migrate(previous);
            At(database, DonnEggClusterNarrows.Was.X, DonnEggClusterNarrows.Was.Z);
        }

        [TestMethod]
        public void ARowMovedByHandIsLeftAloneBothWays()
        {
            using var database = new OrdinaryLootTestDatabase();
            var migrator = database.Context.GetService<IMigrator>();
            var previous = Previous(database);

            migrator.Migrate(previous);
            foreach (var sql in DonnEggClusterNarrows.Down())
                Exec(database, sql);
            Exec(database, $"UPDATE world_destructible SET pos_x = -70 WHERE id = {DonnEggClusterNarrows.Now.Id}");

            migrator.Migrate(Migration);
            Assert.AreEqual(-70.0, Double(database, "SELECT pos_x AS Value " + Row));
            Assert.AreEqual(DonnEggClusterNarrows.Now.X, Double(database, "SELECT pos_x AS Value " + Marker), 1e-3, "the marker moved");

            migrator.Migrate(previous);
            Assert.AreEqual(-70.0, Double(database, "SELECT pos_x AS Value " + Row));
        }

        private static void At(OrdinaryLootTestDatabase database, double x, double z)
        {
            Assert.AreEqual(x, Double(database, "SELECT pos_x AS Value " + Row), 1e-3);
            Assert.AreEqual(z, Double(database, "SELECT pos_z AS Value " + Row), 1e-3);
            Assert.AreEqual(x, Double(database, "SELECT pos_x AS Value " + Marker), 1e-3);
            Assert.AreEqual(z, Double(database, "SELECT pos_z AS Value " + Marker), 1e-3);
        }

        private static string Previous(OrdinaryLootTestDatabase database)
        {
            var all = database.Context.Database.GetMigrations().ToList();
            return all[all.IndexOf(Migration) - 1];
        }

        private static int Exec(OrdinaryLootTestDatabase database, string sql) => database.Context.Database.ExecuteSqlRaw(sql);

        private static double Double(OrdinaryLootTestDatabase database, string sql) =>
            database.Context.Database.SqlQueryRaw<double>(sql).AsEnumerable().Single();
    }
}
