using System;
using System.IO;
using System.Linq;
using System.Numerics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Rasa.Managers;
using Rasa.Navigation;
using Rasa.Services.Preloader;

namespace Rasa.Test.Database
{
    // Two Thrax packs by Pravus Research's HQ on the ground, not on the plane under it (Pravus_hq_ground).
    [TestClass]
    public class PravusHqGroundTests
    {
        private const string Migration = "20271122000000_Pravus_hq_ground";

        [TestMethod]
        public void EachPoolIsOnTheGroundAndWasOnAnIslandUnderIt()
        {
            var navMesh = TestNavMeshes.Query(NavMeshFile.PathFor(NavMeshDirectory(), "adv_foreas_concordia_wilderness_pravusresearch"));

            foreach (var (pool, x, z, wasY) in PravusHqGround.Under)
            {
                var now = new Vector3((float)x, (float)PravusHqGround.NowY(pool), (float)z);
                var ground = navMesh.Nearest(now).Value;

                Assert.AreEqual(now.Y, ground.Y, 0.1f, $"{pool} on the ground");
                Assert.AreEqual(ground.Y, navMesh.Nearest(now + new Vector3(0, 6, 0)).Value.Y, 0.1f, $"{pool}: nothing walkable over it");
                Assert.IsGreaterThan(100000f, navMesh.RegionArea(ground).Value, $"{pool}: the compound's ground");

                var under = navMesh.Nearest(new Vector3(now.X, (float)wasY, now.Z)).Value;
                Assert.AreEqual(wasY, under.Y, 0.1, $"{pool} was on the plane");
                Assert.IsGreaterThan(NavMeshManager.UnstickPocketArea, navMesh.RegionArea(under).Value, $"{pool}: too large to be taken off");
                Assert.IsFalse(navMesh.Connected(under, ground) == true, $"{pool}: the plane is not walked to");
            }
        }

        [TestMethod]
        public void TheContentPutsThePoolsOnTheGround()
        {
            using var database = new OrdinaryLootTestDatabase();
            database.Context.GetService<IMigrator>().Migrate();

            foreach (var (pool, _, _, _) in PravusHqGround.Under)
                Assert.AreEqual(PravusHqGround.NowY(pool), Y(database, pool), 1e-3, $"{pool}");
        }

        [TestMethod]
        public void UpPutsThePoolsOnTheGroundAndDownPutsThemBack()
        {
            using var database = new OrdinaryLootTestDatabase();
            var migrator = database.Context.GetService<IMigrator>();
            var previous = Previous(database);

            migrator.Migrate(previous);

            // A World that put Pravus Research in before the pools were raised.
            UnderTheGround(database);

            migrator.Migrate(Migration);
            foreach (var (pool, _, _, _) in PravusHqGround.Under)
                Assert.AreEqual(PravusHqGround.NowY(pool), Y(database, pool), 1e-3, $"{pool}");

            foreach (var sql in PravusHqGround.Up())
                Exec(database, sql);
            foreach (var (pool, _, _, _) in PravusHqGround.Under)
                Assert.AreEqual(PravusHqGround.NowY(pool), Y(database, pool), 1e-3, $"{pool}: Up twice");

            migrator.Migrate(previous);
            foreach (var (pool, _, _, wasY) in PravusHqGround.Under)
                Assert.AreEqual(wasY, Y(database, pool), 1e-3, $"{pool}");
        }

        [TestMethod]
        public void APoolMovedByHandIsLeftAloneBothWays()
        {
            using var database = new OrdinaryLootTestDatabase();
            var migrator = database.Context.GetService<IMigrator>();
            var previous = Previous(database);
            var (moved, _, _, _) = PravusHqGround.Under[0];
            var (other, _, _, otherWasY) = PravusHqGround.Under[1];

            migrator.Migrate(previous);
            UnderTheGround(database);
            Exec(database, $"UPDATE spawnpool SET pos_y = 40 WHERE id = {moved}");

            migrator.Migrate(Migration);
            Assert.AreEqual(40.0, Y(database, moved), 1e-3);
            Assert.AreEqual(PravusHqGround.NowY(other), Y(database, other), 1e-3, "the other moved");

            migrator.Migrate(previous);
            Assert.AreEqual(40.0, Y(database, moved), 1e-3);
            Assert.AreEqual(otherWasY, Y(database, other), 1e-3, "the other back");
        }

        private static void UnderTheGround(OrdinaryLootTestDatabase database)
        {
            foreach (var (pool, _, _, wasY) in PravusHqGround.Under)
            {
                Assert.AreEqual(1L, Long(database, $"SELECT COUNT(*) AS Value FROM spawnpool WHERE id = {pool}"), $"{pool} there");
                Exec(database, $"UPDATE spawnpool SET pos_y = {wasY.ToString(System.Globalization.CultureInfo.InvariantCulture)} WHERE id = {pool}");
            }
        }

        private static string Previous(OrdinaryLootTestDatabase database)
        {
            var all = database.Context.Database.GetMigrations().ToList();
            return all[all.IndexOf(Migration) - 1];
        }

        private static double Y(OrdinaryLootTestDatabase database, uint pool) =>
            database.Context.Database.SqlQueryRaw<double>($"SELECT pos_y AS Value FROM spawnpool WHERE id = {pool}").AsEnumerable().Single();

        private static long Long(OrdinaryLootTestDatabase database, string sql) =>
            database.Context.Database.SqlQueryRaw<long>(sql).AsEnumerable().Single();

        private static void Exec(OrdinaryLootTestDatabase database, string sql) => database.Context.Database.ExecuteSqlRaw(sql);

        private static string NavMeshDirectory()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Rasa.NET.sln")))
                directory = directory.Parent;
            return Path.Combine(directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found."), "navmesh");
        }
    }
}
