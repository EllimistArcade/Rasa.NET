using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Rasa.Services.Preloader;

namespace Rasa.Test.Database
{
    // One Way Trip's Special Projects chamber on its floor, not under it (Maligo_chamber_floor).
    [TestClass]
    public class MaligoChamberFloorTests
    {
        private const string Migration = "20271117000000_Maligo_chamber_floor";

        /// <summary>The chamber's places in the stored scene: the device, the charge, Homburg, the four prisoners, their march points, the transfer in.</summary>
        private const int ChamberHeights = 28;

        private static readonly string Scene =
            $"FROM mission_scene_binding WHERE mission_id = {MaligoChamberFloor.Mission} AND content_revision = '{MaligoChamberFloor.Revision}'";

        private static readonly string Marker =
            $"FROM mission_indicator WHERE mission_id = {MaligoChamberFloor.Mission} AND content_revision = '{MaligoChamberFloor.Revision}' " +
            $"AND objective_id = {MaligoChamberFloor.Objective} AND indicator_id = {MaligoChamberFloor.Marker}";

        [TestMethod]
        public void UpPutsTheChamberOnItsFloorAndDownPutsItBack()
        {
            using var database = new OrdinaryLootTestDatabase();
            var migrator = database.Context.GetService<IMigrator>();
            var previous = Previous(database);

            migrator.Migrate(previous);
            UnderTheFloor(database);
            AsBefore(database);

            migrator.Migrate(Migration);
            AsAfter(database);

            foreach (var sql in MaligoChamberFloor.Up())
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
            UnderTheFloor(database);
            Exec(database, $"UPDATE mission_indicator SET pos_x = -240 {Where(Marker)}");
            Exec(database, $"UPDATE teleporter SET pos_y = -90 WHERE id = {MaligoChamberFloor.Pad}");

            migrator.Migrate(Migration);
            Assert.AreEqual(-240.0, Double(database, "SELECT pos_x AS Value " + Marker));
            Assert.AreEqual(-90.0, Double(database, $"SELECT pos_y AS Value FROM teleporter WHERE id = {MaligoChamberFloor.Pad}"));

            migrator.Migrate(previous);
            Assert.AreEqual(-240.0, Double(database, "SELECT pos_x AS Value " + Marker));
            Assert.AreEqual(-90.0, Double(database, $"SELECT pos_y AS Value FROM teleporter WHERE id = {MaligoChamberFloor.Pad}"));
        }

        /// <summary>As a World that put One Way Trip in before its chamber moved has it.</summary>
        private static void UnderTheFloor(OrdinaryLootTestDatabase database)
        {
            foreach (var sql in MaligoChamberFloor.Down())
                Exec(database, sql);
        }

        private static void AsBefore(OrdinaryLootTestDatabase database)
        {
            var json = Json(database);
            foreach (var (was, now) in MaligoChamberFloor.Places())
            {
                StringAssert.Contains(json, MaligoChamberFloor.Json(was));
                Assert.IsFalse(json.Contains(MaligoChamberFloor.Json(now)), MaligoChamberFloor.Json(now));
            }
            Assert.AreEqual(ChamberHeights, Regex.Matches(json, "\"y\":-104.28,").Count);
            Near((-246.0, -104.28, 128.0), At(database));
            Assert.AreEqual(-104.28, Double(database, $"SELECT pos_y AS Value FROM teleporter WHERE id = {MaligoChamberFloor.Pad}"), 1e-3);
        }

        private static void AsAfter(OrdinaryLootTestDatabase database)
        {
            var json = Json(database);
            foreach (var (was, now) in MaligoChamberFloor.Places())
            {
                StringAssert.Contains(json, MaligoChamberFloor.Json(now));
                Assert.IsFalse(json.Contains(MaligoChamberFloor.Json(was)), MaligoChamberFloor.Json(was));
            }
            Assert.AreEqual(0, Regex.Matches(json, "\"y\":-104.28,").Count, "nothing left under the floor");
            Near((MaligoChamberFloor.NowX, MaligoChamberFloor.NowY, MaligoChamberFloor.NowZ), At(database));
            Assert.AreEqual(MaligoChamberFloor.PadNowY, Double(database, $"SELECT pos_y AS Value FROM teleporter WHERE id = {MaligoChamberFloor.Pad}"), 1e-3);
        }

        private static string Json(OrdinaryLootTestDatabase database) =>
            database.Context.Database.SqlQueryRaw<string>("SELECT bindings AS Value " + Scene).AsEnumerable().Single();

        private static (double, double, double) At(OrdinaryLootTestDatabase database) =>
            (Double(database, "SELECT pos_x AS Value " + Marker), Double(database, "SELECT pos_y AS Value " + Marker),
                Double(database, "SELECT pos_z AS Value " + Marker));

        private static void Near((double X, double Y, double Z) expected, (double X, double Y, double Z) actual)
        {
            Assert.AreEqual(expected.X, actual.X, 1e-3, "x");
            Assert.AreEqual(expected.Y, actual.Y, 1e-3, "y");
            Assert.AreEqual(expected.Z, actual.Z, 1e-3, "z");
        }

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
