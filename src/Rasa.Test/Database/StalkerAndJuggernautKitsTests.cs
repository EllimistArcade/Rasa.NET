using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Rasa.Services.Preloader;

namespace Rasa.Test.Database
{
    // The Stalkers' egg reach, the Juggernauts' mortars and blackhole, the Plateau's Stalkers from orbit (Stalker_and_juggernaut_kits).
    [TestClass]
    public class StalkerAndJuggernautKitsTests
    {
        private const string Migration = "20271111000000_Stalker_and_juggernaut_kits";

        [TestMethod]
        public void UpGivesThemTheirKitsAndDownTakesThemOff()
        {
            using var database = new OrdinaryLootTestDatabase();
            var migrator = database.Context.GetService<IMigrator>();
            var previous = Previous(database);

            migrator.Migrate(previous);
            AsBefore(database);

            migrator.Migrate(Migration);
            AsAfter(database);

            foreach (var sql in StalkerAndJuggernautKits.Up())
                database.Context.Database.ExecuteSqlRaw(sql);
            AsAfter(database);
            Assert.AreEqual(1L, Long(database, $"SELECT COUNT(*) AS Value FROM creature_action WHERE id = {StalkerAndJuggernautKits.PrototypeMortars}"), "Up twice is once");

            migrator.Migrate(previous);
            AsBefore(database);
            Assert.AreEqual(0L, Long(database, "SELECT COUNT(*) AS Value FROM creature_action WHERE id BETWEEN 790001 AND 790004"), "the new rows gone");
        }

        [TestMethod]
        public void WhatIsThereByHandIsLeftAloneBothWays()
        {
            using var database = new OrdinaryLootTestDatabase();
            var migrator = database.Context.GetService<IMigrator>();
            var previous = Previous(database);

            migrator.Migrate(previous);
            database.Context.Database.ExecuteSqlRaw("UPDATE creature_action SET range_max = 30 WHERE id = 65042");
            database.Context.Database.ExecuteSqlRaw($"UPDATE creature SET action3 = 750027 WHERE id = {StalkerAndJuggernautKits.PrototypeJuggernaut}");
            database.Context.Database.ExecuteSqlRaw("UPDATE spawnpool SET anim_type = 1 WHERE id = 750024");

            migrator.Migrate(Migration);
            Assert.AreEqual(30.0, Double(database, "SELECT range_max AS Value FROM creature_action WHERE id = 65042"));
            Assert.AreEqual(750027L, Slot(database, StalkerAndJuggernautKits.PrototypeJuggernaut, "action3"));
            Assert.AreEqual(1L, Long(database, "SELECT anim_type AS Value FROM spawnpool WHERE id = 750024"));
            Assert.AreEqual((long)StalkerAndJuggernautKits.PrototypeMortars, Slot(database, StalkerAndJuggernautKits.PrototypeJuggernaut, "action2"));

            migrator.Migrate(previous);
            Assert.AreEqual(30.0, Double(database, "SELECT range_max AS Value FROM creature_action WHERE id = 65042"));
            Assert.AreEqual(750027L, Slot(database, StalkerAndJuggernautKits.PrototypeJuggernaut, "action3"));
            Assert.AreEqual(1L, Long(database, "SELECT anim_type AS Value FROM spawnpool WHERE id = 750024"));
            Assert.AreEqual(0L, Long(database, $"SELECT COUNT(*) AS Value FROM creature_action WHERE id = {StalkerAndJuggernautKits.PrototypeBlackhole}"),
                "the row no slot took goes with the rest");
        }

        private static string Previous(OrdinaryLootTestDatabase database)
        {
            var all = database.Context.Database.GetMigrations().ToList();
            return all[all.IndexOf(Migration) - 1];
        }

        private static void AsBefore(OrdinaryLootTestDatabase database)
        {
            foreach (var id in StalkerAndJuggernautKits.Ovulates)
                Assert.AreEqual(StalkerAndJuggernautKits.ChargeReach, Double(database, $"SELECT range_max AS Value FROM creature_action WHERE id = {id}"), $"ovulate {id}");

            foreach (var (creature, slot, _) in StalkerAndJuggernautKits.Slots)
                Assert.AreEqual(0L, Slot(database, creature, slot), $"{creature} {slot}");

            foreach (var pool in StalkerAndJuggernautKits.OrbitalPools)
                Assert.AreEqual(0L, Long(database, $"SELECT anim_type AS Value FROM spawnpool WHERE id = {pool}"), $"pool {pool}");
        }

        private static void AsAfter(OrdinaryLootTestDatabase database)
        {
            foreach (var id in StalkerAndJuggernautKits.Ovulates)
                Assert.AreEqual(StalkerAndJuggernautKits.EggReach, Double(database, $"SELECT range_max AS Value FROM creature_action WHERE id = {id}"), $"ovulate {id}");

            foreach (var (creature, slot, action) in StalkerAndJuggernautKits.Slots)
                Assert.AreEqual((long)action, Slot(database, creature, slot), $"{creature} {slot}");

            foreach (var a in StalkerAndJuggernautKits.Added)
                Assert.AreEqual((long)a.ActionId, Long(database, $"SELECT action_id AS Value FROM creature_action WHERE id = {a.Id}"), a.Description);

            foreach (var pool in StalkerAndJuggernautKits.OrbitalPools)
                Assert.AreEqual(3L, Long(database, $"SELECT anim_type AS Value FROM spawnpool WHERE id = {pool}"), $"pool {pool}");

            Assert.AreEqual(0L, Long(database, "SELECT anim_type AS Value FROM spawnpool WHERE id = 750021"), "the other two stand where they are");
            Assert.AreEqual(0L, Long(database, "SELECT anim_type AS Value FROM spawnpool WHERE id = 750023"));
        }

        private static long Slot(OrdinaryLootTestDatabase database, uint creature, string slot) =>
            Long(database, $"SELECT {slot} AS Value FROM creature WHERE id = {creature}");

        private static long Long(OrdinaryLootTestDatabase database, string sql) =>
            database.Context.Database.SqlQueryRaw<long>(sql).AsEnumerable().Single();

        private static double Double(OrdinaryLootTestDatabase database, string sql) =>
            database.Context.Database.SqlQueryRaw<double>(sql).AsEnumerable().Single();
    }
}
