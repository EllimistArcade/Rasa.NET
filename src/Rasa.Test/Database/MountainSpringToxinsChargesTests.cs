using System.Linq;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Rasa.Missions.Content;
using Rasa.Missions.Scenes;
using Rasa.Services.Preloader;

namespace Rasa.Test.Database
{
    using M = Rasa.Services.Preloader.Missions.Plateau.PlateauMountainSpringToxinsV1;

    // Mountain Spring Toxins' chemical towers with a charge at each one's foot (Mountain_spring_toxins_charges).
    [TestClass]
    public class MountainSpringToxinsChargesTests
    {
        private const string Migration = "20271124000000_Mountain_spring_toxins_charges";

        private static readonly string Scene =
            $"FROM mission_scene_binding WHERE mission_id = {MountainSpringToxinsCharges.Mission} AND content_revision = '{MountainSpringToxinsCharges.Revision}'";

        [TestMethod]
        public void UpPutsInTheChargesAndDownTakesThemOut()
        {
            using var database = new OrdinaryLootTestDatabase();
            var migrator = database.Context.GetService<IMigrator>();
            var previous = Previous(database);

            // As a World that put Mountain Spring Toxins in before the charges has it.
            migrator.Migrate(previous);
            foreach (var sql in MountainSpringToxinsCharges.Down())
                Exec(database, sql);
            Assert.AreEqual(0, Charges(database));

            migrator.Migrate(Migration);
            Assert.AreEqual(M.Towers.Length, Charges(database));
            Assert.AreEqual(MountainSpringToxinsCharges.After, Json(database));

            foreach (var sql in MountainSpringToxinsCharges.Up())
                Assert.AreEqual(0, Exec(database, sql), "a scene with its charges is left as it is");

            migrator.Migrate(previous);
            Assert.AreEqual(0, Charges(database));
            Assert.AreEqual(MountainSpringToxinsCharges.Before, Json(database));
        }

        /// <summary>
        /// Each charge is used to its tower's objective, by the tower's own sequence, which sets it and takes it out of
        /// use only if it is there: a scene started before the charges has none, and its towers are still used.
        /// </summary>
        [TestMethod]
        public void EachChargeBlowsItsTowerAndTheTowerStillCan()
        {
            var scene = M.Scene();
            for (var index = 0; index < M.Towers.Length; index++)
            {
                var tower = scene.Actors[M.TowerRole(index)];
                var charge = scene.Actors[M.ChargeRole(index)];
                Assert.AreEqual(M.ChargeClass, charge.TemplateId);
                Assert.AreEqual(tower.UseAction, charge.UseAction, "the same objective and sequence");
                var blow = scene.Sequences[tower.UseAction.SequenceId];
                var done = blow.World.OfType<SetInteractionIntent>().Single(intent => intent.Role == charge.Role);
                Assert.IsTrue(done.IfPresent);
                Assert.IsFalse(done.Enabled);
                Assert.AreEqual(M.ChargeSetState, done.ObjectState);
                Assert.IsFalse(blow.World.Any(intent => intent.Role == charge.Role && intent is not SetInteractionIntent),
                    "nothing else of the charge's: it is not there in a scene started before it");
                Assert.IsTrue(scene.Sequences[0].World.OfType<EnsureActorIntent>().Any(intent => intent.Role == charge.Role));
            }

            var before = M.Scene(charges: false);
            Assert.IsFalse(before.Actors.Keys.Any(role => role.StartsWith("tower-charge-")));
            Assert.AreEqual(JsonSerializer.Serialize(before, MissionContentCodec.Options), MountainSpringToxinsCharges.Before);
        }

        private static long Charges(OrdinaryLootTestDatabase database)
        {
            var json = Json(database);
            return Enumerable.Range(0, M.Towers.Length).Count(index => json.Contains($"\"{M.ChargeRole(index)}\""));
        }

        private static string Json(OrdinaryLootTestDatabase database) =>
            database.Context.Database.SqlQueryRaw<string>("SELECT bindings AS Value " + Scene).AsEnumerable().Single();

        private static string Previous(OrdinaryLootTestDatabase database)
        {
            var all = database.Context.Database.GetMigrations().ToList();
            return all[all.IndexOf(Migration) - 1];
        }

        // ExecuteSqlRaw takes braces for its parameters; the scene's JSON has them.
        private static int Exec(OrdinaryLootTestDatabase database, string sql) =>
            database.Context.Database.ExecuteSqlRaw(sql.Replace("{", "{{").Replace("}", "}}"));
    }
}
