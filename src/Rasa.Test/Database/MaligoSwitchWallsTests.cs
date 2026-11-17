using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Rasa.Services.Preloader;

namespace Rasa.Test.Database
{
    // Self Preservation's red switch in front of its wall (Maligo_switch_walls); the yellow one is Plateau_self_preservation_charges'.
    [TestClass]
    public class MaligoSwitchWallsTests
    {
        private const string Migration = "20271120000000_Maligo_switch_walls";

        private static readonly string Scene =
            $"FROM mission_scene_binding WHERE mission_id = {MaligoSwitchWalls.Mission} AND content_revision = '{MaligoSwitchWalls.Revision}'";

        private static string Marker(int index) =>
            $"FROM mission_indicator WHERE mission_id = {MaligoSwitchWalls.Mission} AND content_revision = '{MaligoSwitchWalls.Revision}' " +
            $"AND objective_id = {MaligoSwitchWalls.Objective} AND indicator_id = {MaligoSwitchWalls.Marker(index)}";

        [TestMethod]
        public void UpPutsTheSwitchesInFrontOfTheirWallsAndDownPutsThemBack()
        {
            using var database = new OrdinaryLootTestDatabase();
            var migrator = database.Context.GetService<IMigrator>();
            var previous = Previous(database);

            migrator.Migrate(previous);
            BehindTheWalls(database);
            AsBefore(database);

            migrator.Migrate(Migration);
            AsAfter(database);

            foreach (var sql in MaligoSwitchWalls.Up())
                Exec(database, sql);
            AsAfter(database);

            migrator.Migrate(previous);
            AsBefore(database);
        }

        [TestMethod]
        public void WhatIsThereByHandIsLeftAloneBothWays()
        {
            using var database = new OrdinaryLootTestDatabase();
            var migrator = database.Context.GetService<IMigrator>();
            var previous = Previous(database);

            migrator.Migrate(previous);
            BehindTheWalls(database);
            Exec(database, $"UPDATE mission_indicator SET pos_x = -40 {Where(Marker(2))}");

            migrator.Migrate(Migration);
            Assert.AreEqual(-40.0, Double(database, "SELECT pos_x AS Value " + Marker(2)));
            StringAssert.Contains(Json(database), MaligoSwitchWalls.Json(MaligoSwitchWalls.Now(2), MaligoSwitchWalls.NowFacing(2)), "the switch moved");

            migrator.Migrate(previous);
            Assert.AreEqual(-40.0, Double(database, "SELECT pos_x AS Value " + Marker(2)));
        }

        /// <summary>As a World that put Self Preservation in before its switches moved has it.</summary>
        private static void BehindTheWalls(OrdinaryLootTestDatabase database)
        {
            foreach (var sql in MaligoSwitchWalls.Down())
                Exec(database, sql);
        }

        private static void AsBefore(OrdinaryLootTestDatabase database)
        {
            var json = Json(database);
            foreach (var (index, was, wasFacing) in MaligoSwitchWalls.Moved)
            {
                StringAssert.Contains(json, MaligoSwitchWalls.Json(was, wasFacing));
                Assert.IsFalse(json.Contains(MaligoSwitchWalls.Json(MaligoSwitchWalls.Now(index), MaligoSwitchWalls.NowFacing(index))));
                Assert.AreEqual(was.X, Double(database, "SELECT pos_x AS Value " + Marker(index)), 1e-3);
                Assert.AreEqual(was.Z, Double(database, "SELECT pos_z AS Value " + Marker(index)), 1e-3);
            }
        }

        private static void AsAfter(OrdinaryLootTestDatabase database)
        {
            var json = Json(database);
            foreach (var (index, was, wasFacing) in MaligoSwitchWalls.Moved)
            {
                StringAssert.Contains(json, MaligoSwitchWalls.Json(MaligoSwitchWalls.Now(index), MaligoSwitchWalls.NowFacing(index)));
                Assert.IsFalse(json.Contains(MaligoSwitchWalls.Json(was, wasFacing)));
                Assert.AreEqual(MaligoSwitchWalls.Now(index).X, Double(database, "SELECT pos_x AS Value " + Marker(index)), 1e-3);
                Assert.AreEqual(MaligoSwitchWalls.Now(index).Z, Double(database, "SELECT pos_z AS Value " + Marker(index)), 1e-3);
            }
        }

        private static string Json(OrdinaryLootTestDatabase database) =>
            database.Context.Database.SqlQueryRaw<string>("SELECT bindings AS Value " + Scene).AsEnumerable().Single();

        private static string Where(string from) => from.Substring(from.IndexOf("WHERE"));

        private static string Previous(OrdinaryLootTestDatabase database)
        {
            var all = database.Context.Database.GetMigrations().ToList();
            return all[all.IndexOf(Migration) - 1];
        }

        // ExecuteSqlRaw takes braces for its parameters; the scene's JSON has them.
        private static void Exec(OrdinaryLootTestDatabase database, string sql) =>
            database.Context.Database.ExecuteSqlRaw(sql.Replace("{", "{{").Replace("}", "}}"));

        private static double Double(OrdinaryLootTestDatabase database, string sql) =>
            database.Context.Database.SqlQueryRaw<double>(sql).AsEnumerable().Single();
    }
}
