using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Rasa.Services.Preloader;
using Rasa.Structures.World;

namespace Rasa.Test.Database
{
    // The class equipment missions, 2010 and 2011, cannot be abandoned (Class_equipment_missions_kept).
    [TestClass]
    public class ClassEquipmentMissionsKeptTests
    {
        private const string Migration = "20271118000000_Class_equipment_missions_kept";

        [TestMethod]
        public void UpProhibitsAbandoningThemAndDownAllowsItAgain()
        {
            using var database = new OrdinaryLootTestDatabase();
            var migrator = database.Context.GetService<IMigrator>();
            var previous = Previous(database);

            migrator.Migrate(previous);
            Assert.AreEqual(2L, Count(database, MissionAbandonmentPolicy.Allowed));

            migrator.Migrate(Migration);
            Assert.AreEqual(2L, Count(database, MissionAbandonmentPolicy.Prohibited));

            foreach (var sql in ClassEquipmentMissionsKept.Up())
                database.Context.Database.ExecuteSqlRaw(sql);
            Assert.AreEqual(2L, Count(database, MissionAbandonmentPolicy.Prohibited), "again: nothing more");

            migrator.Migrate(previous);
            Assert.AreEqual(2L, Count(database, MissionAbandonmentPolicy.Allowed));
        }

        [TestMethod]
        public void OnlyTheTwoAreChanged()
        {
            using var database = new OrdinaryLootTestDatabase();
            var migrator = database.Context.GetService<IMigrator>();
            var previous = Previous(database);

            migrator.Migrate(previous);
            var prohibited = Scalar(database, $"SELECT COUNT(*) AS Value FROM mission_content_definition WHERE abandonment_policy = {(byte)MissionAbandonmentPolicy.Prohibited}");

            migrator.Migrate(Migration);
            Assert.AreEqual(prohibited + 2, Scalar(database,
                $"SELECT COUNT(*) AS Value FROM mission_content_definition WHERE abandonment_policy = {(byte)MissionAbandonmentPolicy.Prohibited}"));
        }

        private static long Count(OrdinaryLootTestDatabase database, MissionAbandonmentPolicy policy) =>
            Scalar(database, "SELECT COUNT(*) AS Value FROM mission_content_definition " +
                $"WHERE mission_id IN ({ClassEquipmentMissionsKept.Soldier}, {ClassEquipmentMissionsKept.Specialist}) " +
                $"AND content_revision = '{ClassEquipmentMissionsKept.Revision}' AND abandonment_policy = {(byte)policy}");

        private static string Previous(OrdinaryLootTestDatabase database)
        {
            var all = database.Context.Database.GetMigrations().ToList();
            return all[all.IndexOf(Migration) - 1];
        }

        private static long Scalar(OrdinaryLootTestDatabase database, string sql) =>
            database.Context.Database.SqlQueryRaw<long>(sql).AsEnumerable().Single();
    }
}
