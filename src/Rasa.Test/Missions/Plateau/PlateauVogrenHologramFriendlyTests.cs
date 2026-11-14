using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Rasa.Services.Preloader.Missions.Plateau;
using Rasa.Test.Database;

namespace Rasa.Test.Missions.Plateau
{
    /// <summary>Vogren's hologram FRIENDLY again, not NEUTRAL (Vogren_hologram_friendly, PlateauVogrenHologramFriendlyV1).</summary>
    [TestClass]
    [DoNotParallelize]
    public class PlateauVogrenHologramFriendlyTests
    {
        private const string Migration = "20271119000000_Vogren_hologram_friendly";
        private const string Previous = "20271118000000_Class_equipment_missions_kept";

        [TestMethod]
        public void TheHologramIsFriendlyAndDownMakesItNeutralAgain()
        {
            using var database = new OrdinaryLootTestDatabase();
            var migrator = database.Context.GetService<IMigrator>();

            migrator.Migrate(Previous);
            var others = Factions(database);
            Assert.AreEqual(PlateauVogrenHologramFriendlyV1.Neutral, others[PlateauVogrenNpcsV1.Hologram]);

            migrator.Migrate(Migration);
            var after = Factions(database);
            Assert.AreEqual(PlateauVogrenHologramFriendlyV1.Friendly, after[PlateauVogrenNpcsV1.Hologram]);
            foreach (var (id, faction) in others.Where(entry => entry.Key != PlateauVogrenNpcsV1.Hologram))
                Assert.AreEqual(faction, after[id], $"creature {id} as it was");

            migrator.Migrate(Previous);
            CollectionAssert.AreEquivalent(others.ToList(), Factions(database).ToList());
        }

        [TestMethod]
        public void AHologramAGmHasMadeHostileIsLeftSo()
        {
            using var database = new OrdinaryLootTestDatabase();
            var migrator = database.Context.GetService<IMigrator>();

            migrator.Migrate(Previous);
            database.Context.Database.ExecuteSqlRaw($"UPDATE creature SET faction = 0 WHERE id = {PlateauVogrenNpcsV1.Hologram}");
            migrator.Migrate(Migration);
            Assert.AreEqual(0u, Factions(database)[PlateauVogrenNpcsV1.Hologram]);
            migrator.Migrate(Previous);
            Assert.AreEqual(0u, Factions(database)[PlateauVogrenNpcsV1.Hologram]);
        }

        [TestMethod]
        public void NoNpcAPlayerMayKillIsBackWithinAMinute()
        {
            // An NPC with a conversation that a player may attack - HOSTILE or NEUTRAL - and is put back by its pool in
            // seconds is experience on tap, as the hologram was. Every other one waits five minutes or more.
            using var database = new OrdinaryLootTestDatabase();
            database.Context.GetService<IMigrator>().Migrate(Migration);

            var quick = database.Context.Database.SqlQueryRaw<string>(
                "SELECT s.id || ' ' || c.comment AS Value FROM spawnpool s JOIN creature c ON c.id IN " +
                "(s.creature_1_Id, s.creature_2_Id, s.creature_3_Id, s.creature_4_Id, s.creature_5_Id, s.creature_6_Id) " +
                $"WHERE c.faction IN (0, {PlateauVogrenHologramFriendlyV1.Neutral}) AND s.mode = 0 AND s.respown_time < {MinimumRespawnTenths} " +
                "AND EXISTS (SELECT 1 FROM npc_package p WHERE p.id = c.id)").ToList();

            Assert.IsEmpty(quick, string.Join("; ", quick));
        }

        /// <summary>A minute, in spawnpool.respown_time's tenths of a second.</summary>
        private const int MinimumRespawnTenths = 600;

        [TestMethod]
        public void OnMySqlTheFactionChangesAndChangesBack()
        {
            using var context = PersistenceIntegrationTests.CreateContext(typeof(Rasa.Context.World.MySqlWorldContext), "unused");
            var migrator = context.GetService<IMigrator>();

            StringAssert.Contains(migrator.GenerateScript(Previous, Migration), PlateauVogrenHologramFriendlyV1.Up().Single());
            StringAssert.Contains(migrator.GenerateScript(Migration, Previous), PlateauVogrenHologramFriendlyV1.Down().Single());
        }

        private static System.Collections.Generic.Dictionary<uint, uint> Factions(OrdinaryLootTestDatabase database) =>
            database.Context.CreatureEntries.AsNoTracking().ToDictionary(row => row.Id, row => row.Faction);
    }
}
