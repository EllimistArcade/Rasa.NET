using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Rasa.Services.Preloader;

using F = Rasa.Services.Preloader.Missions.Plateau.PlateauFinalAssaultV1;

namespace Rasa.Test.Database
{
    // Final Assault's power core ready running and destroyed by the detonator (Final_assault_core).
    [TestClass]
    public class FinalAssaultCoreTests
    {
        private const string Migration = "20271123000000_Final_assault_core";

        private static readonly string Scene =
            $"FROM mission_scene_binding WHERE mission_id = {FinalAssaultCore.Mission} AND content_revision = '{FinalAssaultCore.Revision}'";

        [TestMethod]
        public void TheCoreIsReadyRunningAndSetDestroyed()
        {
            // The client's model: the running sun in TS_STATE_1 (56), destroyed in TS_STATE_0 (55).
            Assert.AreEqual(56U, F.CoreReadyState);
            Assert.AreEqual(55U, F.CoreSetState);

            using var database = new OrdinaryLootTestDatabase();
            database.Context.GetService<IMigrator>().Migrate();

            var scene = Bindings(database);
            Assert.Contains(FinalAssaultCore.InitialNow, scene);
            Assert.Contains(FinalAssaultCore.ArmNow, scene);
        }

        [TestMethod]
        public void UpTurnsTheCoreRoundAndDownTurnsItBack()
        {
            using var database = new OrdinaryLootTestDatabase();
            var migrator = database.Context.GetService<IMigrator>();
            var previous = Previous(database);

            migrator.Migrate(previous);

            // A World that put Final Assault in before the core was turned round.
            AsBefore(database);
            Assert.Contains(FinalAssaultCore.InitialWas, Bindings(database));

            migrator.Migrate(Migration);
            var scene = Bindings(database);
            Assert.Contains(FinalAssaultCore.InitialNow, scene);
            Assert.Contains(FinalAssaultCore.ArmNow, scene);
            Assert.DoesNotContain(FinalAssaultCore.InitialWas, scene);
            Assert.DoesNotContain(FinalAssaultCore.ArmWas, scene);

            foreach (var sql in FinalAssaultCore.Up())
                Exec(database, sql);
            Assert.AreEqual(scene, Bindings(database), "Up twice");

            migrator.Migrate(previous);
            scene = Bindings(database);
            Assert.Contains(FinalAssaultCore.InitialWas, scene);
            Assert.Contains(FinalAssaultCore.ArmWas, scene);
        }

        [TestMethod]
        public void ASceneChangedByHandIsLeftAloneBothWays()
        {
            using var database = new OrdinaryLootTestDatabase();
            var migrator = database.Context.GetService<IMigrator>();
            var previous = Previous(database);

            migrator.Migrate(previous);
            AsBefore(database);

            // Its arming changed by hand: neither fragment is changed.
            Exec(database, $"UPDATE mission_scene_binding SET bindings = REPLACE(bindings, '{FinalAssaultCore.ArmWas}', " +
                           $"'{FinalAssaultCore.ArmWas.Replace($":{F.CoreReadyState},", ":31,")}') WHERE mission_id = {FinalAssaultCore.Mission}");
            var edited = Bindings(database);

            migrator.Migrate(Migration);
            Assert.AreEqual(edited, Bindings(database));

            migrator.Migrate(previous);
            Assert.AreEqual(edited, Bindings(database));
        }

        /// <summary>The scene as an earlier World stored it: the core ready in 55 and set in 56.</summary>
        private static void AsBefore(OrdinaryLootTestDatabase database)
        {
            if (Bindings(database).Contains(FinalAssaultCore.InitialWas))
                return;

            foreach (var sql in FinalAssaultCore.Down())
                Exec(database, sql);
        }

        private static string Previous(OrdinaryLootTestDatabase database)
        {
            var all = database.Context.Database.GetMigrations().ToList();
            return all[all.IndexOf(Migration) - 1];
        }

        private static string Bindings(OrdinaryLootTestDatabase database) =>
            database.Context.Database.SqlQueryRaw<string>("SELECT bindings AS Value " + Scene).AsEnumerable().Single();

        // ExecuteSqlRaw reads braces as its own placeholders.
        private static void Exec(OrdinaryLootTestDatabase database, string sql) =>
            database.Context.Database.ExecuteSqlRaw(Escape(sql));

        private static string Escape(string sql) => sql.Replace("{", "{{").Replace("}", "}}");
    }
}
