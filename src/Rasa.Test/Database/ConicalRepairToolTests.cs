using System;
using System.IO;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Rasa.Test.Database
{
    using Rasa.Context.Char;
    using Rasa.Context.World;
    using Rasa.Services.Preloader;

    /// <summary>
    /// Too Quiet's Conical Repair Tool (item template 121310, class DELETEME_BROKEN, which the
    /// client does not make an item of) is the Direct Repair Tool of the same band, 3869, in the
    /// mission's rewards and in the items already given (ConicalRepairTool).
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class ConicalRepairToolTests
    {
        private const string WorldMigration = "20271204000000_Conical_repair_tool";
        private const string WorldBefore = "20271203000000_Instance_inside_markers";
        private const string CharMigration = "20271204000000_Conical_repair_tool_items";
        private const string CharBefore = "20271103000100_Valverde_targets_of_opportunity";

        [TestMethod]
        public void TheReplacementIsTheSameItemClassAndQualityInTheEquipmentTab()
        {
            using var database = new OrdinaryLootTestDatabase();
            database.Context.GetService<IMigrator>().Migrate(WorldMigration);

            Assert.AreEqual(28699L, Long(database.Context, $"SELECT itemClassId AS Value FROM itemtemplate_itemclass WHERE itemTemplateId = {ConicalRepairTool.Broken}"));
            Assert.AreEqual("DELETEME_BROKEN", database.Context.Database.SqlQueryRaw<string>("SELECT class_name AS Value FROM entityclass WHERE id = 28699").Single());
            Assert.AreEqual(12797L, Long(database.Context, $"SELECT itemClassId AS Value FROM itemtemplate_itemclass WHERE itemTemplateId = {ConicalRepairTool.Replacement}"));
            Assert.AreEqual("Tool_Avatar_Field_Repair_Direct_CMN_05_to_09",
                database.Context.Database.SqlQueryRaw<string>("SELECT class_name AS Value FROM entityclass WHERE id = 12797").Single());

            string Row(uint template) => database.Context.Database.SqlQueryRaw<string>(
                "SELECT quality_id || ' ' || buy_price || ' ' || sell_price AS Value FROM itemtemplate WHERE id = " + template).Single();
            string Class(uint id) => database.Context.Database.SqlQueryRaw<string>(
                "SELECT inventory_icon_string_id || ' ' || loot_value || ' ' || max_hp || ' ' || stack_size AS Value FROM itemclass WHERE id = " + id).Single();

            Assert.AreEqual(Row(ConicalRepairTool.Broken), Row(ConicalRepairTool.Replacement), "quality and prices");
            Assert.AreEqual(Class(28699), Class(12797), "icon, value, condition and stack");
            Assert.AreEqual(1L, Long(database.Context, $"SELECT inventory_category AS Value FROM itemtemplate WHERE id = {ConicalRepairTool.Replacement}"), "Equipment");
            Assert.AreEqual(1L, Long(database.Context, $"SELECT COUNT(*) AS Value FROM itemtemplate_weapon WHERE id = {ConicalRepairTool.Replacement}"), "a tool's weapon row");
        }

        [TestMethod]
        public void TooQuietRewardsTheDirectRepairToolAndDownPutsTheOldOneBack()
        {
            using var database = new OrdinaryLootTestDatabase();
            var migrator = database.Context.GetService<IMigrator>();

            migrator.Migrate(WorldBefore);
            CollectionAssert.AreEquivalent(new[] { ConicalRepairTool.Broken, 13356u }, TooQuietRewards(database.Context));

            migrator.Migrate(WorldMigration);
            CollectionAssert.AreEquivalent(new[] { ConicalRepairTool.Replacement, 13356u }, TooQuietRewards(database.Context));

            // Nothing hands out the broken template any more.
            foreach (var (table, column) in new[]
                     {
                         ("mission_reward_item", "item_template_id"), ("loot_item", "item_template_id"),
                         ("legacy_flat_loot_group_item", "item_template_id"), ("vendor_item", "item_template_id")
                     })
                Assert.AreEqual(0L, Long(database.Context, $"SELECT COUNT(*) AS Value FROM {table} WHERE {column} = {ConicalRepairTool.Broken}"), table);

            foreach (var statement in ConicalRepairTool.WorldUp)
                Assert.AreEqual(0, database.Context.Database.ExecuteSqlRaw(statement), "a second run changes nothing");

            migrator.Migrate(WorldBefore);
            CollectionAssert.AreEquivalent(new[] { ConicalRepairTool.Broken, 13356u }, TooQuietRewards(database.Context));

            migrator.Migrate(WorldMigration);
            CollectionAssert.AreEquivalent(new[] { ConicalRepairTool.Replacement, 13356u }, TooQuietRewards(database.Context));
        }

        [TestMethod]
        public void TheToolsAlreadyGivenAreMadeTheDirectRepairTool()
        {
            WithCharDatabase(context =>
            {
                var migrator = context.GetService<IMigrator>();
                migrator.Migrate(CharBefore);

                // Rows of their own, without the characters they would belong to.
                context.Database.OpenConnection();
                context.Database.ExecuteSqlRaw("PRAGMA foreign_keys = OFF");
                context.Database.ExecuteSqlRaw(
                    "insert into items (item_id, item_template_id, stack_size, current_hp, color, ammo_count, bound_character_id, crafter_name, created_at) values " +
                    $"(1, {ConicalRepairTool.Broken}, 1, 100, 0, 0, 0, '', '2026-01-01 00:00:00'), " +
                    $"(2, 13356, 1, 55, 0, 0, 0, '', '2026-01-01 00:00:00'), " +
                    $"(3, {ConicalRepairTool.Broken}, 1, 40, 0, 0, 7, 'Someone', '2026-01-01 00:00:00')");
                context.Database.ExecuteSqlRaw(
                    "insert into character_inventory (item_id, account_id, character_id, invenotry_type, slot_id) values (1, 1, 1, 1, 224)");

                migrator.Migrate(CharMigration);

                Assert.AreEqual(ConicalRepairTool.Replacement, Template(context, 1));
                Assert.AreEqual(13356u, Template(context, 2), "another item");
                Assert.AreEqual(ConicalRepairTool.Replacement, Template(context, 3));
                Assert.AreEqual(40L, Long(context, "SELECT current_hp AS Value FROM items WHERE item_id = 3"), "its condition kept");
                Assert.AreEqual(7L, Long(context, "SELECT bound_character_id AS Value FROM items WHERE item_id = 3"), "its binding kept");
                Assert.AreEqual(224L, Long(context, "SELECT slot_id AS Value FROM character_inventory WHERE item_id = 1"),
                    "the slot is the loader's to move");

                foreach (var statement in ConicalRepairTool.CharUp)
                    Assert.AreEqual(0, context.Database.ExecuteSqlRaw(statement), "a second run changes nothing");

                migrator.Migrate(CharBefore);
                Assert.AreEqual(ConicalRepairTool.Replacement, Template(context, 1), "Down leaves them");
            });
        }

        [TestMethod]
        public void OnMySqlTheSameStatementsAreMade()
        {
            using var world = PersistenceIntegrationTests.CreateContext(typeof(MySqlWorldContext), "unused");
            var worldUp = world.GetService<IMigrator>().GenerateScript(WorldBefore, WorldMigration);
            var worldDown = world.GetService<IMigrator>().GenerateScript(WorldMigration, WorldBefore);

            StringAssert.Contains(worldUp, ConicalRepairTool.WorldUp.Single());
            StringAssert.Contains(worldDown, ConicalRepairTool.WorldDown.Single());
            Assert.IsFalse(world.Database.HasPendingModelChanges());

            using var chars = PersistenceIntegrationTests.CreateContext(typeof(MySqlCharContext), "unused");
            var charUp = chars.GetService<IMigrator>().GenerateScript(CharBefore, CharMigration);

            StringAssert.Contains(charUp, ConicalRepairTool.CharUp.Single());
            Assert.IsFalse(chars.Database.HasPendingModelChanges());
        }

        private static uint[] TooQuietRewards(DbContext context) => context.Database.SqlQueryRaw<long>(
                $"SELECT item_template_id AS Value FROM mission_reward_item WHERE mission_id = {CavesOfDonnContent.TooQuiet} " +
                $"AND content_revision = '{CavesOfDonnContent.Revision}'")
            .ToArray().Select(id => (uint)id).ToArray();

        private static uint Template(DbContext context, uint itemId) =>
            (uint)Long(context, $"SELECT item_template_id AS Value FROM items WHERE item_id = {itemId}");

        private static long Long(DbContext context, string sql) =>
            context.Database.SqlQueryRaw<long>(sql).ToArray().Single();

        private static void WithCharDatabase(Action<DbContext> body)
        {
            var path = Path.Combine(AppContext.BaseDirectory, "TestDatabases", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);

            try
            {
                using var context = PersistenceIntegrationTests.CreateContext(typeof(SqliteCharContext), Path.Combine(path, "database"));
                body(context);
            }
            finally
            {
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

                try
                {
                    Directory.Delete(path, true);
                }
                catch (IOException)
                {
                }
            }
        }
    }
}
