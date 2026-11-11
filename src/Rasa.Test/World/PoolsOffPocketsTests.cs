using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Rasa.Test.World
{
    using Rasa.Managers;
    using Rasa.Navigation;
    using Rasa.Services.Preloader;
    using Rasa.Structures;
    using Rasa.Structures.World;
    using Rasa.Test.Database;
    using Rasa.Test.TestSupport;

    /// <summary>Pools moved off the pockets of navmesh their creatures could not leave, and one made scripted (Pools_off_pockets, PoolsOffPockets).</summary>
    [TestClass]
    [DoNotParallelize]
    public class PoolsOffPocketsTests
    {
        private const string Migration = "20271024000000_Pools_off_pockets";
        private const string Previous = "20271023000000_Crater_lake_pen_generator";

        private static readonly Dictionary<uint, string> Maps = new()
        {
            [2190] = "adv_arieki_torden_abyss_dybukkar",
            [1734] = "adv_arieki_ligo_ashendesert",
            [1304] = "adv_foreas_valverde_pools",
            [1394] = "adv_foreas_concordia_palisades_devilsden",
            [1384] = "adv_foreas_concordia_palisades_warnetcaverns",
            [1764] = "adv_arieki_torden_plains",
        };

        /// <summary>The AFS turrets of the post at the Brann viro-relay tower's foot.</summary>
        private static readonly uint[] RelayTurrets = { 550056, 550057, 550058 };

        [TestMethod]
        public void EachPoolMovesAndTheMantasAreScriptedAndDownPutsThemBack()
        {
            using var database = new OrdinaryLootTestDatabase();
            var migrator = database.Context.GetService<IMigrator>();

            migrator.Migrate(Previous);
            var before = Rows(database);
            foreach (var m in PoolsOffPockets.Moves)
            {
                var row = before[m.Id];
                Assert.AreEqual((m.WasX, m.WasY, m.WasZ), (Math.Round(row.PosX, 2), Math.Round(row.PosY, 2), Math.Round(row.PosZ, 2)), $"{m.Name} where it was");
                Assert.AreEqual(m.MapContextId, row.MapContextId, m.Name);
            }
            Assert.AreEqual(PoolsOffPockets.Automatic, (short)before[PoolsOffPockets.Retired].Mode);

            migrator.Migrate(Migration);
            var after = Rows(database);
            foreach (var m in PoolsOffPockets.Moves)
                Assert.AreEqual((m.X, m.Y, m.Z), (Math.Round(after[m.Id].PosX, 2), Math.Round(after[m.Id].PosY, 2), Math.Round(after[m.Id].PosZ, 2)), $"{m.Name} moved");
            Assert.AreEqual(PoolsOffPockets.Scripted, (short)after[PoolsOffPockets.Retired].Mode, "the Mires mantas not put down");

            // Nothing else changed.
            foreach (var (id, row) in after.Where(entry => entry.Key != PoolsOffPockets.Retired && PoolsOffPockets.Moves.All(m => m.Id != entry.Key)))
                Assert.AreEqual(Key(before[id]), Key(row), $"pool {id}");

            migrator.Migrate(Previous);
            var back = Rows(database);
            foreach (var (id, row) in before)
                Assert.AreEqual(Key(row), Key(back[id]), $"Down puts pool {id} back exactly");
        }

        [TestMethod]
        public void APoolAGmHasMovedIsLeftWhereTheyPutIt()
        {
            using var database = new OrdinaryLootTestDatabase();
            var migrator = database.Context.GetService<IMigrator>();

            migrator.Migrate(Previous);
            database.Context.Database.ExecuteSqlRaw("UPDATE spawnpool SET pos_x = 60.0 WHERE id = 520001");
            database.Context.Database.ExecuteSqlRaw($"UPDATE spawnpool SET mode = {PoolsOffPockets.Scripted} WHERE id = 540089");

            migrator.Migrate(Migration);
            Assert.AreEqual(60.0, Rows(database)[520001].PosX, "where the GM put it");
            Assert.AreEqual(-285.53, Rows(database)[520007].PosX, 0.001, "the rest moved");

            // A GM's own mantas scripted, then set going again after the move: Down leaves both.
            database.Context.Database.ExecuteSqlRaw($"UPDATE spawnpool SET mode = {PoolsOffPockets.Automatic} WHERE id = {PoolsOffPockets.Retired}");
            migrator.Migrate(Previous);
            Assert.AreEqual(60.0, Rows(database)[520001].PosX, "and Down leaves it");
            Assert.AreEqual(PoolsOffPockets.Automatic, (short)Rows(database)[PoolsOffPockets.Retired].Mode);
            Assert.AreEqual(PoolsOffPockets.Scripted, (short)Rows(database)[540089].Mode, "a pool Up never touched");
        }

        [TestMethod]
        public void EachWasOnAPocketAndIsOnOpenGround()
        {
            var root = RepositoryRoot();

            foreach (var m in PoolsOffPockets.Moves)
            {
                var navMesh = TestNavMeshes.Query(NavMeshFile.PathFor(Path.Combine(root, "navmesh"), Maps[m.MapContextId]));
                var was = navMesh.Nearest(new Vector3((float)m.WasX, (float)m.WasY, (float)m.WasZ));
                var now = navMesh.Nearest(new Vector3((float)m.X, (float)m.Y, (float)m.Z));

                Assert.IsTrue(was.HasValue && navMesh.RegionArea(was.Value) < NavMeshManager.UnstickPocketArea, $"{m.Name}: a pocket before");
                Assert.IsTrue(now.HasValue && navMesh.RegionArea(now.Value) >= NavMeshManager.UnstickPocketArea, $"{m.Name}: open ground now");
                Assert.IsLessThan(0.5f, Vector3.Distance(now.Value, new Vector3((float)m.X, (float)m.Y, (float)m.Z)), $"{m.Name}: on it, not over it");

                // A step in from its edge, not on it.
                for (var i = 0; i < 8; i++)
                {
                    var step = now.Value + new Vector3(MathF.Cos(i * MathF.PI / 4), 0, MathF.Sin(i * MathF.PI / 4));
                    Assert.AreEqual(true, navMesh.Reachable(now.Value, navMesh.Nearest(step) ?? step), $"{m.Name}: ground all round it");
                }
            }
        }

        [TestMethod]
        public void TheRelayTowerFlaregashersStandOnTheirSistersFloorClearOfTheTurrets()
        {
            var root = RepositoryRoot();
            var plains = new MapChannel
            {
                MapInfo = new MapInfo(1764, Maps[1764], 0, 0),
                NavMesh = TestNavMeshes.Query(NavMeshFile.PathFor(Path.Combine(root, "navmesh"), Maps[1764]))
            };
            var move = PoolsOffPockets.Moves.Single(m => m.Id == 540111);
            var pool = new Rasa.Structures.SpawnPool { DbId = 540111, Position = new Vector3((float)move.X, (float)move.Y, (float)move.Z), Radius = 15f, Mode = SpawnPoolManager.ModeAutomatic };
            var sister = new Rasa.Structures.SpawnPool { DbId = 540113, Position = new Vector3(822.2f, 574.6f, -58.2f), Radius = 15f, Mode = SpawnPoolManager.ModeAutomatic };

            Assert.AreEqual(true, plains.NavMesh.Reachable(SpawnPoolManager.SpawnPoint(plains, pool, 3), SpawnPoolManager.SpawnPoint(plains, sister, 3)), "the canyon floor its sister's pack is on");

            using var database = new OrdinaryLootTestDatabase();
            database.Context.GetService<IMigrator>().Migrate(Migration);
            var rows = Rows(database);
            foreach (var turret in RelayTurrets)
            {
                var gap = Vector2.Distance(new Vector2((float)rows[turret].PosX, (float)rows[turret].PosZ), new Vector2((float)move.X, (float)move.Z)) - pool.Radius;

                Assert.IsGreaterThanOrEqualTo(SpawnPoolManager.TurretScan, gap, $"turret {turret}: {gap} m from the pack's area");
            }
        }

        // It was 20271023000000, Crater_lake_pen_generator's timestamp: a World that ran it under that id
        // runs it again under this one, changing nothing, and loses the old id's history row.
        [TestMethod]
        public void AWorldThatRanItUnderItsOldIdRunsItAgainAsItIsAndForgetsTheOldId()
        {
            using var database = new OrdinaryLootTestDatabase();
            var migrator = database.Context.GetService<IMigrator>();
            migrator.Migrate(Previous);

            foreach (var sql in PoolsOffPockets.Up())
                database.Context.Database.ExecuteSqlRaw(sql);
            database.Context.Database.ExecuteSqlRaw(
                $"INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES ('{PoolsOffPockets.OldId}', '10.0.0');");
            var ran = Rows(database).ToDictionary(pair => pair.Key, pair => Key(pair.Value));

            migrator.Migrate(Migration);

            CollectionAssert.AreEquivalent(ran.ToList(), Rows(database).ToDictionary(pair => pair.Key, pair => Key(pair.Value)).ToList());
            Assert.AreEqual(0, History(database, PoolsOffPockets.OldId));
            Assert.AreEqual(1, History(database, Migration));
        }

        private static int History(OrdinaryLootTestDatabase database, string id) => database.Context.Database
                .SqlQueryRaw<int>($"SELECT COUNT(*) AS Value FROM __EFMigrationsHistory WHERE MigrationId = '{id}'").AsEnumerable().Single();

        [TestMethod]
        public void OnMySqlTheMovesGoInAndComeOut()
        {
            using var context = PersistenceIntegrationTests.CreateContext(typeof(Rasa.Context.World.MySqlWorldContext), "unused");
            var migrator = context.GetService<IMigrator>();
            var up = migrator.GenerateScript(Previous, Migration);
            var down = migrator.GenerateScript(Migration, Previous);

            foreach (var sql in PoolsOffPockets.Up())
                StringAssert.Contains(up, sql);
            StringAssert.Contains(up, $"'{Migration}'");
            foreach (var sql in PoolsOffPockets.Down())
                StringAssert.Contains(down, sql);
        }

        private static (double, double, double, byte) Key(SpawnPoolEntry row) =>
            (Math.Round(row.PosX, 3), Math.Round(row.PosY, 3), Math.Round(row.PosZ, 3), row.Mode);

        private static Dictionary<uint, SpawnPoolEntry> Rows(OrdinaryLootTestDatabase database) =>
            database.Context.SpawnPoolEntries.AsNoTracking().ToDictionary(row => row.Id);

        private static string RepositoryRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);

            while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "navmesh")))
                directory = directory.Parent;

            return directory?.FullName ?? AppContext.BaseDirectory;
        }
    }
}
