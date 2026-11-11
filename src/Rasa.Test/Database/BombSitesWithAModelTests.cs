using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Rasa.Services.Preloader;

namespace Rasa.Test.Database
{
    // The bomb sites a detonator is set on have a model the client draws and a player can click (Bomb_sites_with_a_model).
    [TestClass]
    public class BombSitesWithAModelTests
    {
        private const string Migration = "20271115000000_Bomb_sites_with_a_model";

        /// <summary>The mesh every class without a model of its own has: the client draws default.geo for it.</summary>
        private const long NoModel = 776;

        /// <summary>The bomb augmentation (AugmentationType.Bomb): a usable a detonator is set on.</summary>
        private const string BombAugmentation = "42";

        [TestMethod]
        public void EveryBombSiteHasAModel()
        {
            using var database = new OrdinaryLootTestDatabase();
            database.Context.GetService<IMigrator>().Migrate();

            foreach (var classId in new[] { CraterLakeContent.BombClass, DevilsDenContent.BombClass })
                Assert.AreNotEqual(NoModel, Long(database, $"SELECT mesh_id AS Value FROM entityclass WHERE id = {classId}"), $"class {classId}");

            var bare = database.Context.Database.SqlQueryRaw<long>(
                "SELECT u.id AS Value FROM world_usable u JOIN entityclass e ON e.id = u.class_id " +
                $"WHERE (',' || e.aug_list || ',') LIKE '%,{BombAugmentation},%' AND e.mesh_id = {NoModel}").ToList();

            Assert.IsEmpty(bare, "bomb sites with nothing to click: " + string.Join(", ", bare));
            Assert.AreEqual(3L, Long(database,
                $"SELECT COUNT(*) AS Value FROM world_usable WHERE id IN ({string.Join(", ", BombSitesWithAModel.DevilsDenBombs)}) AND class_id = {BombSitesWithAModel.BombClass}"));
        }

        [TestMethod]
        public void UpMakesDevilsDensSitesTheAfsChargeAndDownPutsThemBack()
        {
            using var database = new OrdinaryLootTestDatabase();
            var migrator = database.Context.GetService<IMigrator>();
            var previous = Previous(database);

            migrator.Migrate(previous);

            // A World that put Devil's Den in before its bomb sites changed class.
            Exec(database, $"UPDATE world_usable SET class_id = {BombSitesWithAModel.ForeanBombClass} WHERE id IN ({Ids})");

            migrator.Migrate(Migration);
            Assert.AreEqual(3L, Count(database, BombSitesWithAModel.BombClass));

            foreach (var sql in BombSitesWithAModel.Up())
                Exec(database, sql);
            Assert.AreEqual(3L, Count(database, BombSitesWithAModel.BombClass), "Up twice");

            migrator.Migrate(previous);
            Assert.AreEqual(3L, Count(database, BombSitesWithAModel.ForeanBombClass));
        }

        [TestMethod]
        public void ASiteChangedByHandIsLeftAloneBothWays()
        {
            using var database = new OrdinaryLootTestDatabase();
            var migrator = database.Context.GetService<IMigrator>();
            var previous = Previous(database);
            var door = DevilsDenContent.DoorBomb;

            migrator.Migrate(previous);
            Exec(database, $"UPDATE world_usable SET class_id = {BombSitesWithAModel.ForeanBombClass} WHERE id IN ({Ids})");
            Exec(database, $"UPDATE world_usable SET class_id = 10124 WHERE id = {door}");

            migrator.Migrate(Migration);
            Assert.AreEqual(10124L, Long(database, $"SELECT class_id AS Value FROM world_usable WHERE id = {door}"));

            migrator.Migrate(previous);
            Assert.AreEqual(10124L, Long(database, $"SELECT class_id AS Value FROM world_usable WHERE id = {door}"));
        }

        private static string Ids => string.Join(", ", BombSitesWithAModel.DevilsDenBombs);

        private static string Previous(OrdinaryLootTestDatabase database)
        {
            var all = database.Context.Database.GetMigrations().ToList();
            return all[all.IndexOf(Migration) - 1];
        }

        private static long Count(OrdinaryLootTestDatabase database, uint classId) =>
            Long(database, $"SELECT COUNT(*) AS Value FROM world_usable WHERE id IN ({Ids}) AND class_id = {classId}");

        private static void Exec(OrdinaryLootTestDatabase database, string sql) => database.Context.Database.ExecuteSqlRaw(sql);

        private static long Long(OrdinaryLootTestDatabase database, string sql) =>
            database.Context.Database.SqlQueryRaw<long>(sql).AsEnumerable().Single();
    }
}
