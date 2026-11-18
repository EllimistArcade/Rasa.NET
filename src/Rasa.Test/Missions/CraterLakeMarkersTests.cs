using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Numerics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Rasa.Data;
using Rasa.Managers;
using Rasa.Navigation;
using Rasa.Packets.LootDispenser.Client;
using Rasa.Services.Preloader;
using Rasa.Structures;
using Rasa.Structures.Char;
using Rasa.Structures.World;
using Rasa.Test.Missions.Wilderness;

namespace Rasa.Test.Missions
{
    using C = CraterLakeContent;
    using M = CraterLakeMarkers;
    using static Wilderness.WildernessTwinPillarsTests;

    /// <summary>
    /// Crater Lake's mission markers (Crater_lake_markers, CraterLakeMarkers): every one sent on its
    /// map and standing on ground walked to from the way in; Ghost in the Machine's return open, with
    /// its markers in the facility and on the Wilderness, until Franja takes it; Velns and the crates
    /// marked in the facility; Up again changes nothing and Down gives V1 and the old markers back.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class CraterLakeMarkersTests
    {
        private const string Migration = "20271202000000_Crater_lake_markers", Previous = "20271201000100_Marshes_kaav_respawn";

        private static readonly uint[] Missions = C.Missions;

        [TestInitialize]
        public void Clock()
        {
            CraterLakeFacility.Reset();
            PlacedUsables.Reset();
        }

        [TestCleanup]
        public void Restore()
        {
            CraterLakeFacility.Reset();
            PlacedUsables.Reset();
        }

        /// <summary>Each marker's map, as the server sends it.</summary>
        private static uint ExpectedMap(uint missionId, uint objectiveId, uint indicatorId) => (missionId, objectiveId, indicatorId) switch
        {
            (C.TheDeadLive, C.SpeakToVelns, M.VelnsMarker) => C.CraterLake,
            (C.TheDeadLive, _, _) => C.Wilderness,
            (C.CentrifugeFuel, C.FindCrystals, MissionIndicator.UnnamedFrom + C.FindCrystals) => C.Wilderness,
            (C.GhostInTheMachine, C.ReturnDogtags, M.FranjaMarker) => C.Wilderness,
            (C.MendingABrokenHeart, _, _) => C.Wilderness,
            _ => C.CraterLake
        };

        [TestMethod]
        public void EveryMarkerIsSentOnItsMapAndStandsOnGroundWalkedTo()
        {
            using var harness = Create();
            var catalog = new MissionContentCatalog(harness, new Dictionary<uint, Mission>(), null);
            catalog.Load();
            var navs = new Dictionary<uint, NavMeshQuery>
            {
                [C.CraterLake] = new(NavMeshFile.Read(NavMeshFile.PathFor(NavMeshDirectory(), "adv_foreas_concordia_wilderness_clrf"))),
                [C.Wilderness] = new(NavMeshFile.Read(NavMeshFile.PathFor(NavMeshDirectory(), "adv_foreas_concordia_wilderness")))
            };
            var from = new Dictionary<uint, Vector3> { [C.CraterLake] = C.Arrival, [C.Wilderness] = C.WildernessDoor };
            var seen = 0;

            foreach (var missionId in Missions)
                foreach (var objective in catalog.Missions[missionId].Objectives.Values)
                    foreach (var pin in objective.Indicators)
                    {
                        var what = $"{missionId}/{objective.ObjectiveId}/{pin.IndicatorId}";
                        var map = MissionMarkerProjection.ResolveMap(catalog, missionId, objective.ObjectiveId, pin);
                        Assert.AreEqual(ExpectedMap(missionId, objective.ObjectiveId, pin.IndicatorId), map, what);
                        var nav = navs[map];
                        var ground = nav.GroundHeight(pin.Position);
                        Assert.IsNotNull(ground, $"{what}: no ground");
                        Assert.IsLessThan(2.1f, Math.Abs(ground.Value - pin.Position.Y), $"{what}: ground {ground}");
                        Assert.AreEqual(true, nav.Reachable(from[map], new Vector3(pin.Position.X, ground.Value, pin.Position.Z)),
                            $"{what}: walked to from the way in");
                        seen++;
                    }

            // 21 authored, and the six this adds: Velns, four crates, Franja.
            Assert.AreEqual(27, seen);

            // What the markers point at is walked to as well: the terminals, the crates.
            foreach (var spot in C.Terminals.Select(t => t.Spot).Concat(C.Crates))
                Assert.AreEqual(true, navs[C.CraterLake].Reachable(C.Arrival, spot), $"{spot}");
        }

        [TestMethod]
        public void GhostInTheMachinesReturnIsOpenWithItsMarkersUntilFranjaTakesIt()
        {
            using var harness = Create();
            Completed(harness, C.TheDeadLive);
            Completed(harness, C.WeBeJammin);
            harness.Manager.OfferAfterTurnIn(harness.Client, C.WeBeJammin);
            Assert.IsTrue(harness.Manager.TryAcceptRadioMission(harness.Client, C.GhostInTheMachine));
            harness.Drain();
            CraterLakeFacility.Worker(harness.Map);
            harness.Drain();

            // Before Casper: his marker only. Neither the door nor the hidden objective is shown.
            var shown = Shown(harness, C.CraterLake);
            CollectionAssert.AreEquivalent(new[] { C.EngageCasper }, shown.Keys.ToArray());
            Assert.AreEqual(MissionIndicator.UnnamedFrom + C.EngageCasper, shown[C.EngageCasper].Single());
            Assert.IsEmpty(Shown(harness, C.Wilderness).SelectMany(o => o.Value).ToList(), "nothing on the Wilderness yet");

            harness.SpawnWorld(C.Casper);
            var casper = harness.Map.MapCellInfo.Cells.Values.SelectMany(cell => cell.CreatureList).Distinct()
                .Single(c => c.DbId == C.Casper && c.State != CharacterState.Dead);
            harness.MoveTo(casper.Position + new Vector3(2, 0, 0));
            harness.Creatures.HandleCreatureKill(harness.Map, casper, harness.Client.Player, CritKill.Own);
            harness.Drain();
            var loot = harness.Map.LootDispensers[casper.CorpseLootEntityId];
            LootDispenserManager.Instance.RequestLootAllFromCorpse(harness.Client, new RequestLootAllFromCorpsePacket { EntityId = loot.EntityId });
            harness.Drain();

            var log = harness.Client.Player.Missions[C.GhostInTheMachine];
            Assert.AreEqual(MissionObjectiveState.Completed, log.Objectives[M.DogtagsHeld].State);
            Assert.AreEqual(MissionObjectiveState.Incomplete, log.Objectives[C.ReturnDogtags].State);
            Assert.IsTrue(log.Completeable);

            // With the dogtags: the way out in the facility, Franja on the Wilderness.
            shown = Shown(harness, C.CraterLake);
            CollectionAssert.AreEquivalent(new[] { M.DoorMarker }, shown[C.ReturnDogtags]);
            Assert.IsFalse(shown.ContainsKey(M.DogtagsHeld), "the client's hidden objective is not sent");
            var outside = Shown(harness, C.Wilderness);
            CollectionAssert.AreEquivalent(new[] { M.FranjaMarker }, outside[C.ReturnDogtags]);

            var franja = Put(harness, C.Franja, harness.Client.Player.Position + new Vector3(2, 0, 0));
            Reward(harness, franja, C.GhostInTheMachine, null, null, 8000, 1500);
            Assert.AreEqual(0L, Held(harness, C.DogtagsTemplate), "Franja takes them");
        }

        [TestMethod]
        public void VelnsAndTheCratesAreMarkedInTheFacility()
        {
            using var harness = Create();
            var franja = Put(harness, C.Franja, C.Arrival + new Vector3(2, 0, 0));
            Accept(harness, franja, C.TheDeadLive);
            var sten = Put(harness, C.Sten, C.Arrival + new Vector3(2, 0, 0));
            Accept(harness, sten, C.CentrifugeFuel);

            var inside = Shown(harness, C.CraterLake);
            CollectionAssert.AreEquivalent(new[] { M.VelnsMarker }, inside[C.SpeakToVelns]);
            CollectionAssert.AreEquivalent(M.CrateMarkers.Select(m => m.Id).ToArray(), inside[C.FindCrystals]);

            var outside = Shown(harness, C.Wilderness);
            CollectionAssert.AreEquivalent(new[] { MissionIndicator.UnnamedFrom + C.SpeakToVelns }, outside[C.SpeakToVelns]);
            CollectionAssert.AreEquivalent(new[] { MissionIndicator.UnnamedFrom + C.FindCrystals }, outside[C.FindCrystals]);
        }

        [TestMethod]
        public void UpAgainChangesNothingAndDownGivesTheOldMarkersAndV1Back()
        {
            using var database = new Rasa.Test.Database.OrdinaryLootTestDatabase();
            var migrator = database.Context.GetService<IMigrator>();

            migrator.Migrate(Previous);
            // Crater_lake ran before the terminal moved: its marker is where the terminal was.
            database.Context.Database.ExecuteSqlRaw($"UPDATE mission_indicator SET pos_x = {M.WasX.ToString(System.Globalization.CultureInfo.InvariantCulture)}, " +
                $"pos_y = {M.WasY.ToString(System.Globalization.CultureInfo.InvariantCulture)}, pos_z = {M.WasZ.ToString(System.Globalization.CultureInfo.InvariantCulture)} " +
                $"WHERE mission_id = {C.DestroyingTheEvidence} AND objective_id = {C.BiologicalLab} AND content_revision = '{C.Revision}'");
            var before = Dump(database);
            Assert.AreEqual(2L, Count(database, $"SELECT COUNT(*) AS Value FROM mission_objective_definition WHERE mission_id = {C.GhostInTheMachine} AND content_revision = '{C.Revision}'"));

            migrator.Migrate(Migration);
            var after = Dump(database);
            Assert.AreEqual(3L, Count(database, $"SELECT COUNT(*) AS Value FROM mission_objective_definition WHERE mission_id = {C.GhostInTheMachine} AND content_revision = '{C.Revision}'"));
            Assert.AreEqual(1L, Count(database, $"SELECT COUNT(*) AS Value FROM mission_objective_definition WHERE mission_id = {C.GhostInTheMachine} " +
                $"AND content_revision = '{C.Revision}' AND objective_id = {C.ReturnDogtags} AND is_required = 0"));
            Assert.AreEqual(2 + M.CrateMarkers.Length, after.Count(r => r.StartsWith("mission_indicator")) - before.Count(r => r.StartsWith("mission_indicator")),
                "Velns's, the crates' and Franja's");
            CollectionAssert.Contains(after, Row("mission_scene_binding", database, C.TheDeadLive).Single());
            StringAssert.Contains(Scene(database, C.TheDeadLive), M.DeadLiveEntryWithVelns);
            StringAssert.Contains(Scene(database, C.CentrifugeFuel), M.CentrifugeMaps);
            StringAssert.Contains(Scene(database, C.GhostInTheMachine), "\"hiddenObjectiveIds\":[3]");
            Assert.AreEqual(1L, Count(database, $"SELECT COUNT(*) AS Value FROM mission_indicator WHERE mission_id = {C.DestroyingTheEvidence} " +
                $"AND objective_id = {C.BiologicalLab} AND ABS(pos_x - {M.NowX.ToString(System.Globalization.CultureInfo.InvariantCulture)}) < 0.01"));

            foreach (var sql in M.Up(mySql: false))
                database.Context.Database.ExecuteSqlRaw(sql.Replace("{", "{{").Replace("}", "}}"));
            CollectionAssert.AreEquivalent(after, Dump(database), "Up again");

            migrator.Migrate(Previous);
            CollectionAssert.AreEquivalent(before, Dump(database), "Down");
        }

        [TestMethod]
        public void OnMySqlTheMarkersGoInAndComeOut()
        {
            using var context = Rasa.Test.Database.PersistenceIntegrationTests.CreateContext(typeof(Rasa.Context.World.MySqlWorldContext), "unused");
            var migrator = context.GetService<IMigrator>();
            var up = migrator.GenerateScript(Previous, Migration);
            var down = migrator.GenerateScript(Migration, Previous);

            foreach (var sql in M.Up(mySql: true))
                StringAssert.Contains(up, sql);
            StringAssert.Contains(up, "CONCAT(");
            foreach (var sql in M.Down())
                StringAssert.Contains(down, sql);
        }

        #region Helpers

        private static readonly string[] Tables =
        {
            "mission_content_definition", "mission_channel_policy", "mission_scene_binding", "mission_evidence", "mission_action",
            "mission_trigger", "mission_scenario_step", "mission_scenario", "mission_indicator", "mission_reward_item",
            "mission_reward_definition", "mission_objective_transition", "mission_objective_definition", "mission_prerequisite"
        };

        /// <summary>Every row of Crater Lake's missions, without the tables' own ids, as text.</summary>
        private static List<string> Dump(Rasa.Test.Database.OrdinaryLootTestDatabase database)
        {
            var rows = new List<string>();
            foreach (var table in Tables)
                foreach (var missionId in Missions)
                    rows.AddRange(Row(table, database, missionId));
            return rows;
        }

        private static List<string> Row(string table, Rasa.Test.Database.OrdinaryLootTestDatabase database, uint missionId)
        {
            var rows = new List<string>();
            var connection = database.Context.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
                connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = $"SELECT * FROM {table} WHERE mission_id = {missionId} AND content_revision = '{C.Revision}'";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var values = Enumerable.Range(0, reader.FieldCount)
                    .Where(i => !string.Equals(reader.GetName(i), "id", StringComparison.OrdinalIgnoreCase))
                    .Select(i => $"{reader.GetName(i)}={Convert.ToString(reader.GetValue(i), System.Globalization.CultureInfo.InvariantCulture)}");
                rows.Add($"{table}: {string.Join("; ", values)}");
            }
            return rows;
        }

        private static string Scene(Rasa.Test.Database.OrdinaryLootTestDatabase database, uint missionId) =>
            database.Context.Set<MissionSceneBindingEntry>().AsNoTracking().Single(b => b.MissionId == missionId && b.ContentRevision == C.Revision).Bindings;

        private static long Count(Rasa.Test.Database.OrdinaryLootTestDatabase database, string sql) =>
            database.Context.Database.SqlQueryRaw<long>(sql).ToArray().Single();

        /// <summary>The markers sent with each objective of the player's missions, on a map.</summary>
        private static Dictionary<uint, uint[]> Shown(WildernessRuntimeTestHarness harness, uint map)
        {
            var player = harness.Client.Player;
            var was = player.MapContextId;
            try
            {
                player.MapContextId = map;
                return harness.Manager.BuildStatusSnapshot(player)
                    .Where(entry => Missions.Contains(entry.Key))
                    .SelectMany(entry => entry.Value.ObjectivesList)
                    .Where(objective => objective.IndicatorList.Count > 0)
                    .ToDictionary(objective => objective.ObjectiveId, objective => objective.IndicatorList.Select(i => i.IndicatorId).ToArray());
            }
            finally
            {
                player.MapContextId = was;
            }
        }

        private static WildernessRuntimeTestHarness Create()
        {
            var harness = WildernessRuntimeTestHarness.Create(mapContextId: C.CraterLake);
            try
            {
                using (var unit = harness.CreateChar())
                    unit.ExecuteTransaction(() => unit.CharacterStartingExperience.Add(
                        new CharacterStartingExperienceEntry(harness.Client.Player.Id, C.Revision, CharacterStartingExperienceState.Skipped)));
                harness.Client.Player.StartingExperienceCompleted = true;
                harness.Drain();
                harness.MoveTo(C.Arrival);
                return harness;
            }
            catch
            {
                harness.Dispose();
                throw;
            }
        }

        private static Creature Put(WildernessRuntimeTestHarness harness, uint dbId, Vector3 position)
        {
            var creature = CreatureManager.Instance.CreateCreature(dbId, null);
            CreatureManager.Instance.SetLocation(creature, NavMeshManager.SnapToGround(harness.Map, position), 0, harness.Map.MapInfo.MapContextId);
            CellManager.Instance.AddToWorld(harness.Map, creature);
            return creature;
        }

        private static void Completed(WildernessRuntimeTestHarness harness, uint missionId)
        {
            using var unit = harness.CreateChar();
            unit.ExecuteTransaction(() => unit.CharacterMissions.Runtime.Archive(
                new CharacterMissionEntry(harness.Client.Player.Id, missionId, (uint)MissionState.Completed)
                {
                    AssignmentId = Guid.NewGuid().ToString("N"),
                    Generation = 1,
                    ContentRevision = C.Revision
                }, harness.UtcNow));
            harness.Client.Player.MissionHistory[missionId] = MissionState.Completed;
            harness.Client.Player.MissionSuccessHistory.Add(missionId);
        }

        private static string NavMeshDirectory()
        {
            var root = AppContext.BaseDirectory;
            while (!Directory.Exists(Path.Combine(root, "navmesh")))
                root = Path.GetDirectoryName(root);
            return Path.Combine(root, "navmesh");
        }

        #endregion
    }
}
