using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Rasa.Services.Preloader;

namespace Rasa.Test.Database
{
    // Two of Bug City's Hanging Hives move out of the rock (Warnet_caverns_hives).
    [TestClass]
    public class WarnetCavernsHivesTests
    {
        private const string Migration = "20271116000000_Warnet_caverns_hives";

        [TestMethod]
        public void UpMovesBothHivesOutOfTheRockAndDownPutsThemBack()
        {
            using var database = new OrdinaryLootTestDatabase();
            var migrator = database.Context.GetService<IMigrator>();
            var previous = Previous(database);

            migrator.Migrate(previous);

            // A World that ran the caverns' content before the hives moved.
            foreach (var m in WarnetCavernsHives.Moves)
                Exec(database, $"UPDATE world_destructible SET pos_x = {m.WasX}, pos_y = {m.WasY}, pos_z = {m.WasZ}, " +
                    $"comment = '{WarnetCavernsHives.Comment(m.WasWhere)}' WHERE id = {m.Id}");

            migrator.Migrate(Migration);
            foreach (var m in WarnetCavernsHives.Moves)
            {
                Assert.AreEqual((m.Now.X, m.Now.Y, m.Now.Z), At(database, m.Id), m.WasWhere);
                Assert.AreEqual(WarnetCavernsHives.Comment(m.Now.Where), Comment(database, m.Id));
            }

            foreach (var sql in WarnetCavernsHives.Up())
                Exec(database, sql);
            foreach (var m in WarnetCavernsHives.Moves)
                Assert.AreEqual((m.Now.X, m.Now.Y, m.Now.Z), At(database, m.Id), "Up twice");

            // The other four are where they were.
            foreach (var hive in WarnetCavernsContent.Hives.Where(h => WarnetCavernsHives.Moves.All(m => m.Id != h.Id)))
                Assert.AreEqual((hive.X, hive.Y, hive.Z), At(database, hive.Id), hive.Where);

            migrator.Migrate(previous);
            foreach (var m in WarnetCavernsHives.Moves)
            {
                Assert.AreEqual((m.WasX, m.WasY, m.WasZ), At(database, m.Id), m.WasWhere);
                Assert.AreEqual(WarnetCavernsHives.Comment(m.WasWhere), Comment(database, m.Id));
            }
        }

        [TestMethod]
        public void AWorldMadeAfterTheMoveHasThemOutOfTheRock()
        {
            using var database = new OrdinaryLootTestDatabase();
            database.Context.GetService<IMigrator>().Migrate(Migration);

            foreach (var hive in WarnetCavernsContent.Hives)
                Assert.AreEqual((hive.X, hive.Y, hive.Z), At(database, hive.Id), hive.Where);
        }

        [TestMethod]
        public void AHiveMovedByHandIsLeftAloneBothWays()
        {
            using var database = new OrdinaryLootTestDatabase();
            var migrator = database.Context.GetService<IMigrator>();
            var previous = Previous(database);
            var id = WarnetCavernsHives.Moves[0].Id;

            migrator.Migrate(previous);
            Exec(database, $"UPDATE world_destructible SET pos_x = 265, pos_y = 53, pos_z = 72 WHERE id = {id}");

            migrator.Migrate(Migration);
            Assert.AreEqual((265.0, 53.0, 72.0), At(database, id));

            migrator.Migrate(previous);
            Assert.AreEqual((265.0, 53.0, 72.0), At(database, id));
        }

        private static string Previous(OrdinaryLootTestDatabase database)
        {
            var all = database.Context.Database.GetMigrations().ToList();
            return all[all.IndexOf(Migration) - 1];
        }

        private static (double, double, double) At(OrdinaryLootTestDatabase database, uint id) =>
            (Double(database, $"SELECT pos_x AS Value FROM world_destructible WHERE id = {id}"),
             Double(database, $"SELECT pos_y AS Value FROM world_destructible WHERE id = {id}"),
             Double(database, $"SELECT pos_z AS Value FROM world_destructible WHERE id = {id}"));

        private static string Comment(OrdinaryLootTestDatabase database, uint id) =>
            database.Context.Database.SqlQueryRaw<string>($"SELECT comment AS Value FROM world_destructible WHERE id = {id}").AsEnumerable().Single();

        private static void Exec(OrdinaryLootTestDatabase database, string sql) => database.Context.Database.ExecuteSqlRaw(sql);

        private static double Double(OrdinaryLootTestDatabase database, string sql) =>
            database.Context.Database.SqlQueryRaw<double>(sql).AsEnumerable().Single();
    }
}
