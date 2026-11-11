using System;
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

    using O = Rasa.Services.Preloader.DivideRememberObelisk;

    /// <summary>
    /// The obelisk under the We/Us and Remember signs on the Concordia Divide hilltop, which the
    /// client's map leaves out (DivideRememberObelisk): one scenery row, standing on the hill, and
    /// Down takes only it out.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class DivideRememberObeliskTests
    {
        private const string Migration = "20271023000000_Divide_remember_obelisk", Previous = "20271022000000_Attacks_on_their_models";

        [TestMethod]
        public void TheObeliskStandsUnderTheSignsAsScenery()
        {
            using var database = new Rasa.Test.Database.OrdinaryLootTestDatabase();
            database.Context.GetService<IMigrator>().Migrate(Migration);
            var row = database.Context.WorldDestructibleEntries.AsNoTracking().Single(entry => entry.Id == O.Id);

            Assert.AreEqual((1148U, 10260U, 0d), (row.MapContextId, row.ClassId, row.Rotation));
            Assert.AreEqual(new Vector3((float)O.At.X, (float)O.At.Y, (float)O.At.Z), row.Position);
            Assert.IsTrue(PlacedDestructibles.PlainScenery.Contains(row.ClassId), "a plain model the server puts down as scenery");

            // At the signs' x and z, its base 8.4 m under the lower one, as the Divide's other two-sign obelisk.
            Assert.AreEqual((O.LowerSign.X, O.LowerSign.Z), (O.At.X, O.At.Z));
            Assert.AreEqual(8.4, O.LowerSign.Y - O.At.Y, 1e-9);
        }

        [TestMethod]
        public void ItsBaseIsOnTheHilltop()
        {
            var nav = new NavMeshQuery(NavMeshFile.Read(NavMeshFile.PathFor(NavMeshDirectory(), "adv_foreas_concordia_divide")));
            var at = new Vector3((float)O.At.X, (float)O.At.Y, (float)O.At.Z);

            var ground = nav.Nearest(at + new Vector3(0, 0.2f, 0));
            Assert.IsNotNull(ground);
            Assert.IsLessThan(0.1f, new Vector2(ground.Value.X - at.X, ground.Value.Z - at.Z).Length());
            Assert.IsLessThan(0.5f, Math.Abs(ground.Value.Y - at.Y), $"ground {ground.Value.Y}");
        }

        [TestMethod]
        public void DownTakesOnlyTheObeliskOutAndUpPutsItBack()
        {
            using var database = new Rasa.Test.Database.OrdinaryLootTestDatabase();
            var migrator = database.Context.GetService<IMigrator>();
            migrator.Migrate(Migration);
            var all = Rows(database);

            migrator.Migrate(Previous);
            Assert.AreEqual(all - 1, Rows(database));
            Assert.IsFalse(database.Context.WorldDestructibleEntries.AsNoTracking().Any(entry => entry.Id == O.Id));

            migrator.Migrate(Migration);
            Assert.AreEqual(all, Rows(database));
            Assert.IsTrue(database.Context.WorldDestructibleEntries.AsNoTracking().Any(entry => entry.Id == O.Id));
        }

        private static int Rows(Rasa.Test.Database.OrdinaryLootTestDatabase database) =>
            database.Context.WorldDestructibleEntries.AsNoTracking().Count();

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
