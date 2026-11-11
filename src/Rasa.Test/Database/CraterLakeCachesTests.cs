using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Rasa.Services.Preloader;

namespace Rasa.Test.Database
{
    // Reclaiming What's Ours' detonator marker moves with the Crater Lake weapons caches (Crater_lake_caches).
    [TestClass]
    public class CraterLakeCachesTests
    {
        private const string Migration = "20271114000000_Crater_lake_caches";

        private static readonly string Marker =
            $"FROM mission_indicator WHERE mission_id = {CraterLakeCaches.Mission} AND content_revision = '{CraterLakeCaches.Revision}' " +
            $"AND objective_id = {CraterLakeCaches.Objective} AND indicator_id = {CraterLakeCaches.Marker}";

        [TestMethod]
        public void UpMovesTheMarkerToTheFirstCacheAndDownPutsItBack()
        {
            using var database = new OrdinaryLootTestDatabase();
            var migrator = database.Context.GetService<IMigrator>();
            var previous = Previous(database);

            migrator.Migrate(previous);

            // A World that ran the facility's content before the caches moved.
            Exec(database, $"UPDATE mission_indicator SET pos_x = {CraterLakeCaches.WasX}, pos_z = {CraterLakeCaches.WasZ} WHERE {Where}");

            migrator.Migrate(Migration);
            Assert.AreEqual((CraterLakeCaches.NowX, CraterLakeCaches.NowZ), At(database));
            Assert.AreEqual(CraterLakeCaches.NowY, Double(database, "SELECT pos_y AS Value " + Marker), 1e-3);

            foreach (var sql in CraterLakeCaches.Up())
                Exec(database, sql);
            Assert.AreEqual((CraterLakeCaches.NowX, CraterLakeCaches.NowZ), At(database), "Up twice");

            migrator.Migrate(previous);
            Assert.AreEqual((CraterLakeCaches.WasX, CraterLakeCaches.WasZ), At(database));
        }

        [TestMethod]
        public void AMarkerMovedByHandIsLeftAloneBothWays()
        {
            using var database = new OrdinaryLootTestDatabase();
            var migrator = database.Context.GetService<IMigrator>();
            var previous = Previous(database);

            migrator.Migrate(previous);
            Exec(database, $"UPDATE mission_indicator SET pos_x = -280, pos_z = -55 WHERE {Where}");

            migrator.Migrate(Migration);
            Assert.AreEqual((-280.0, -55.0), At(database));

            migrator.Migrate(previous);
            Assert.AreEqual((-280.0, -55.0), At(database));
        }

        private static string Where => Marker.Substring("FROM mission_indicator WHERE ".Length);

        private static string Previous(OrdinaryLootTestDatabase database)
        {
            var all = database.Context.Database.GetMigrations().ToList();
            return all[all.IndexOf(Migration) - 1];
        }

        private static (double, double) At(OrdinaryLootTestDatabase database) =>
            (Double(database, "SELECT pos_x AS Value " + Marker), Double(database, "SELECT pos_z AS Value " + Marker));

        private static void Exec(OrdinaryLootTestDatabase database, string sql) => database.Context.Database.ExecuteSqlRaw(sql);

        private static double Double(OrdinaryLootTestDatabase database, string sql) =>
            database.Context.Database.SqlQueryRaw<double>(sql).AsEnumerable().Single();
    }
}
