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
    using Rasa.Test.Database;

    using O = Rasa.Services.Preloader.MissingElohObelisks;

    /// <summary>
    /// The six Eloh obelisks the client's maps leave out under their Logos signs (MissingElohObelisks):
    /// scenery rows, standing on their floors under their signs; a World that ran the Divide's under its
    /// old id keeps one row and loses that id; Down takes only them out.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class MissingElohObelisksTests
    {
        private const string Migration = "20271025000000_Missing_eloh_obelisks", Previous = "20271024000000_Pools_off_pockets";

        private static readonly Dictionary<uint, string> Maps = new()
        {
            [1148] = "adv_foreas_concordia_divide",
            [2084] = "adv_foreas_concordia_elohcommtower2",
            [2103] = "adv_arieki_ligo_thunderhead_faultlever",
            [1773] = "adv_arieki_torden_plains_attacolony",
            [1823] = "adv_foreas_valverde_plateau_sanctusgrotto",
        };

        [TestMethod]
        public void EachObeliskIsAPlainSceneryRowUnderItsSigns()
        {
            using var database = new OrdinaryLootTestDatabase();
            database.Context.GetService<IMigrator>().Migrate(Migration);
            var rows = database.Context.WorldDestructibleEntries.AsNoTracking().Where(row => row.Id >= 572001 && row.Id <= 572006).ToDictionary(row => row.Id);

            Assert.AreEqual(6, O.All.Length);
            CollectionAssert.AreEquivalent(O.All.Select(o => o.Id).ToArray(), rows.Keys.ToArray());
            Assert.IsTrue(PlacedDestructibles.PlainScenery.Contains(O.ObeliskClass), "a plain model the server puts down as scenery");

            foreach (var o in O.All)
            {
                var row = rows[o.Id];
                Assert.AreEqual((o.MapContextId, O.ObeliskClass), (row.MapContextId, row.ClassId), o.Comment);
                Assert.AreEqual(new Vector3((float)o.X, (float)o.Y, (float)o.Z), row.Position, o.Comment);
                Assert.AreEqual(o.Rotation, row.Rotation, 1e-9, o.Comment);

                // It faces opposite its signs, and its lowest sign is where a sign-topped obelisk's is.
                Assert.AreEqual(0, Math.Abs(Math.IEEERemainder(o.SignYaw - Math.PI - o.Rotation, 2 * Math.PI)), 0.001, o.Comment);
                Assert.IsTrue(o.LowerSignY - o.Y is >= 7.1 and <= 8.5, $"{o.Comment}: lowest sign {o.LowerSignY - o.Y} m up");
            }
        }

        [TestMethod]
        public void EachBaseIsOnItsMapsFloor()
        {
            foreach (var group in O.All.GroupBy(o => o.MapContextId))
            {
                var nav = new NavMeshQuery(NavMeshFile.Read(NavMeshFile.PathFor(NavMeshDirectory(), Maps[group.Key])));

                foreach (var o in group)
                {
                    var at = new Vector3((float)o.X, (float)o.Y, (float)o.Z);
                    var ground = nav.Nearest(at + new Vector3(0, 0.2f, 0));

                    Assert.IsNotNull(ground, o.Comment);
                    // The Communication Tower's two stand at the platform's outer corners, inside the navmesh's
                    // margin from the edge.
                    Assert.IsLessThan(1f, new Vector2(ground.Value.X - at.X, ground.Value.Z - at.Z).Length(), o.Comment);
                    Assert.IsLessThan(0.5f, Math.Abs(ground.Value.Y - at.Y), $"{o.Comment}: ground {ground.Value.Y}");
                }
            }
        }

        [TestMethod]
        public void AWorldThatRanTheDivideOneUnderItsOldIdKeepsOneRowAndForgetsTheOldId()
        {
            using var database = new OrdinaryLootTestDatabase();
            var migrator = database.Context.GetService<IMigrator>();
            migrator.Migrate(Previous);

            var divide = O.All[0];
            database.Context.Database.ExecuteSqlRaw(O.Up().First());
            database.Context.Database.ExecuteSqlRaw(
                $"INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES ('{O.OldId}', '10.0.0');");
            database.Context.Database.ExecuteSqlRaw($"UPDATE world_destructible SET comment = 'as it was' WHERE id = {divide.Id};");

            migrator.Migrate(Migration);

            Assert.AreEqual(1, database.Context.WorldDestructibleEntries.AsNoTracking().Count(row => row.Id == divide.Id));
            Assert.AreEqual("as it was", database.Context.WorldDestructibleEntries.AsNoTracking().Single(row => row.Id == divide.Id).Comment);
            Assert.AreEqual(6, database.Context.WorldDestructibleEntries.AsNoTracking().Count(row => row.Id >= 572001 && row.Id <= 572006));
            Assert.AreEqual(0, History(database, O.OldId));
            Assert.AreEqual(1, History(database, Migration));
        }

        [TestMethod]
        public void DownTakesOnlyTheObelisksOutAndUpPutsThemBack()
        {
            using var database = new OrdinaryLootTestDatabase();
            var migrator = database.Context.GetService<IMigrator>();
            migrator.Migrate(Migration);
            var all = Rows(database);

            migrator.Migrate(Previous);
            Assert.AreEqual(all - 6, Rows(database));
            Assert.IsFalse(database.Context.WorldDestructibleEntries.AsNoTracking().Any(row => row.Id >= 572001 && row.Id <= 572006));

            migrator.Migrate(Migration);
            Assert.AreEqual(all, Rows(database));
        }

        [TestMethod]
        public void OnMySqlTheObelisksGoInAndComeOut()
        {
            using var context = PersistenceIntegrationTests.CreateContext(typeof(Rasa.Context.World.MySqlWorldContext), "unused");
            var migrator = context.GetService<IMigrator>();
            var up = migrator.GenerateScript(Previous, Migration);
            var down = migrator.GenerateScript(Migration, Previous);

            foreach (var sql in O.Up())
                StringAssert.Contains(up, sql);
            StringAssert.Contains(up, O.ForgetOldId);
            StringAssert.Contains(up, $"'{Migration}'");
            foreach (var sql in O.Down())
                StringAssert.Contains(down, sql);
        }

        private static int Rows(OrdinaryLootTestDatabase database) =>
            database.Context.WorldDestructibleEntries.AsNoTracking().Count();

        private static int History(OrdinaryLootTestDatabase database, string id) => database.Context.Database
                .SqlQueryRaw<int>($"SELECT COUNT(*) AS Value FROM __EFMigrationsHistory WHERE MigrationId = '{id}'").AsEnumerable().Single();

        private static string NavMeshDirectory()
        {
            for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            {
                var navmesh = Path.Combine(dir.FullName, "navmesh");

                if (Directory.Exists(navmesh))
                    return navmesh;
            }

            throw new DirectoryNotFoundException("navmesh");
        }
    }
}
