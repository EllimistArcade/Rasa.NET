using System;
using System.Collections.Generic;
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
using Rasa.Packets.MapChannel.Client;
using Rasa.Structures;
using Rasa.Structures.Char;
using Rasa.Structures.World;
using Rasa.Test.Missions.Wilderness;

namespace Rasa.Test.Missions
{
    using G = Rasa.Services.Preloader.GuardianProminenceContent;
    using static Wilderness.WildernessTwinPillarsTests;

    /// <summary>
    /// Guardian Prominence as the wiki and the walkthrough tell it (GuardianProminenceContent,
    /// GuardianProminence): its rows, the Wilderness gate's three level 38 guards, the four missions
    /// from level 36, the flight recorder, the Guardian Stone's door, the jamming towers and the
    /// reinforcements, the Juggernauts on the camp and the three commanders.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class GuardianProminenceTests
    {
        private const string Migration = "20271130000000_Guardian_prominence";
        private const string Previous = "20271129000000_Purgas_station_lower_levels";
        private const string MapName = "adv_foreas_concordia_wilderness_guardianprom";
        private const string WildernessMapName = "adv_foreas_concordia_wilderness";

        [TestInitialize]
        public void Clean() => GuardianProminence.Reset();

        [TestCleanup]
        public void Restore() => GuardianProminence.Reset();

        [TestMethod]
        public void TheInstanceHasItsPeopleItsBaneAndItsFourMissions()
        {
            using var harness = Create();
            var world = harness.World;

            foreach (var missionId in G.Missions)
            {
                Assert.IsTrue(harness.Manager.TryGetOperationalMission(missionId, out var mission), $"mission {missionId}");
                Assert.AreEqual(G.Category, mission.CategoryId);
            }

            var packages = world.Set<NpcPackageEntry>().AsNoTracking().ToDictionary(row => row.Id, row => row.PackageId);
            Assert.AreEqual(G.ProfessorPackage, packages[G.Professor]);

            var creatures = world.Set<CreatureEntry>().AsNoTracking().ToDictionary(c => c.Id);
            foreach (var c in G.Creatures.Where(c => c.Faction == G.TargetCategoryRow.Hostile))
                Assert.IsTrue(creatures[c.Id].Level is >= 38 and <= 41, $"{c.Comment} level {creatures[c.Id].Level}");

            var pools = world.Set<SpawnPoolEntry>().AsNoTracking().ToList();
            foreach (var boss in new[] { G.Horga, G.Lurge, G.Zor })
                Assert.AreEqual(1, pools.Count(pool => pool.MapContextId == G.Prominence && pool.Creature1Id == boss), $"{boss}");

            // "The only level 38 Bane in Wilderness are the three guards standing watch in the entrance."
            var guards = pools.Where(pool => pool.MapContextId == G.Wilderness && pool.Creature1Id == G.Soldier).ToList();
            Assert.HasCount(3, guards);
            Assert.AreEqual(38U, creatures[G.Soldier].Level);
            Assert.IsTrue(guards.All(pool => Vector3.Distance(new Vector3((float)pool.PosX, (float)pool.PosY, (float)pool.PosZ), G.WildernessEntrance) < 12f));
            Assert.AreEqual(G.GuardsLootProfile, world.Set<CreatureLootBindingEntry>().AsNoTracking()
                .Single(row => row.MapContextId == G.Wilderness && row.CreatureId == G.Soldier).ProfileKey, "the Wilderness gives every kill its loot");

            var markers = world.Set<MissionIndicatorEntry>().AsNoTracking().Where(row => row.ContentRevision == G.Revision).ToList();
            Assert.IsTrue(markers.All(row => row.IndicatorId > MissionIndicator.UnnamedFrom));
        }

        [TestMethod]
        public void EveryPoolAndObjectStandsOnTheGround()
        {
            using var database = new Rasa.Test.Database.OrdinaryLootTestDatabase();
            database.Context.GetService<IMigrator>().Migrate(Migration);
            var pools = database.Context.SpawnPoolEntries.AsNoTracking().ToList();
            var ours = G.Pools.Select(p => p.Id).ToHashSet();
            var maps = new Dictionary<uint, NavMeshQuery>
            {
                [G.Prominence] = TestNavMeshes.Query(NavMeshFile.PathFor(NavmeshDirectory(), MapName)),
                [G.Wilderness] = TestNavMeshes.Query(NavMeshFile.PathFor(NavmeshDirectory(), WildernessMapName))
            };

            foreach (var pool in pools.Where(pool => ours.Contains(pool.Id)))
            {
                var at = new Vector3((float)pool.PosX, (float)pool.PosY, (float)pool.PosZ);
                var ground = maps[pool.MapContextId].Nearest(at);
                Assert.IsTrue(ground.HasValue, $"{pool.Id} off the ground");
                Assert.IsLessThan(1.5f, Vector2.Distance(new Vector2(at.X, at.Z), new Vector2(ground.Value.X, ground.Value.Z)), $"{pool.Id} over walkable ground");
                Assert.IsLessThan(1f, Math.Abs(ground.Value.Y - at.Y), $"{pool.Id}: the ground {ground.Value.Y - at.Y} m from it");
            }

            foreach (var spot in G.Towers.Select(t => t.At).Concat(G.Reinforcements).Concat(G.JuggernautsFrom)
                         .Append(G.RecorderAt).Append(G.MechAt).Append(G.Camp))
            {
                var ground = maps[G.Prominence].Nearest(spot);
                Assert.IsTrue(ground.HasValue && Vector2.Distance(new Vector2(spot.X, spot.Z), new Vector2(ground.Value.X, ground.Value.Z)) < 1f &&
                    Math.Abs(ground.Value.Y - spot.Y) < 1f, $"{spot} off the ground");
            }

            // The Juggernauts can walk to the camp.
            foreach (var from in G.JuggernautsFrom)
            {
                maps[G.Prominence].FindPath(from, G.Camp, out var complete);
                Assert.IsTrue(complete, $"from {from} to the camp");
            }
        }

        [TestMethod]
        public void TheMigrationGoesInOnceAndDownPutsEverythingBack()
        {
            using var database = new Rasa.Test.Database.OrdinaryLootTestDatabase();
            var migrator = database.Context.GetService<IMigrator>();
            migrator.Migrate(Previous);
            var bindings = Count(database, "SELECT COUNT(*) AS Value FROM creature_loot_binding");

            migrator.Migrate(Migration);
            Assert.AreEqual(G.Missions.Length, Count(database, $"SELECT COUNT(*) AS Value FROM mission_content_definition WHERE content_revision = '{G.Revision}' AND enabled = 1"));
            var rows = Count(database, "SELECT COUNT(*) AS Value FROM creature WHERE id BETWEEN 787000 AND 787999");
            Assert.AreEqual(G.Creatures.Length, rows);
            Assert.AreEqual(G.Pools.Length, Count(database, "SELECT COUNT(*) AS Value FROM spawnpool WHERE id BETWEEN 787000 AND 787999"));
            Assert.AreEqual(bindings + 1, Count(database, "SELECT COUNT(*) AS Value FROM creature_loot_binding"));
            Assert.AreEqual(G.Missions.Length, Count(database,
                $"SELECT COUNT(*) AS Value FROM mission_prerequisite WHERE content_revision = '{G.Revision}' AND kind = 3 AND required_level = {G.MinimumLevel}"));

            foreach (var sql in G.Up().Where(sql => !sql.StartsWith("INSERT INTO mission") && !sql.StartsWith("UPDATE mission")))
                database.Context.Database.ExecuteSqlRaw(sql.Replace("{", "{{").Replace("}", "}}"));
            Assert.AreEqual(rows, Count(database, "SELECT COUNT(*) AS Value FROM creature WHERE id BETWEEN 787000 AND 787999"), "guarded");
            Assert.AreEqual(bindings + 1, Count(database, "SELECT COUNT(*) AS Value FROM creature_loot_binding"), "guarded");

            migrator.Migrate(Previous);
            foreach (var table in new[] { "creature", "creature_action", "creature_stat", "creature_appearance", "spawnpool", "npc_package" })
                Assert.AreEqual(0, Count(database, $"SELECT COUNT(*) AS Value FROM {table} WHERE id BETWEEN 787000 AND 787999"), table);
            Assert.AreEqual(0, Count(database, $"SELECT COUNT(*) AS Value FROM mission_content_definition WHERE content_revision = '{G.Revision}'"));
            Assert.AreEqual(bindings, Count(database, "SELECT COUNT(*) AS Value FROM creature_loot_binding"));

            migrator.Migrate(Migration);
            Assert.AreEqual(G.Missions.Length, Count(database, $"SELECT COUNT(*) AS Value FROM mission_content_definition WHERE content_revision = '{G.Revision}' AND enabled = 1"));
        }

        [TestMethod]
        public void OnMySqlTheMigrationGoesInAndComesOut()
        {
            using var context = Rasa.Test.Database.PersistenceIntegrationTests.CreateContext(typeof(Rasa.Context.World.MySqlWorldContext), "unused");
            var migrator = context.GetService<IMigrator>();
            var up = migrator.GenerateScript(Previous, Migration);
            var down = migrator.GenerateScript(Migration, Previous);

            StringAssert.Contains(up, "Thrax Taskmaster Horga - Guardian Prominence");
            StringAssert.Contains(up, "'guardprom_1_16_5'");
            StringAssert.Contains(down, "DELETE FROM creature_loot_binding");
        }

        /// <summary>"You cannot obtain any missions in that instance until level 36."</summary>
        [TestMethod]
        public void NoMissionBeforeLevelThirtySix()
        {
            using var harness = Create(level: 35);
            var sonia = Put(harness, G.Sonia, G.SoniaAt);
            OpenNativeNpcConversation(harness, sonia);
            Assert.IsFalse(harness.Manager.TryAcceptNpcMission(harness.Client, sonia.EntityId, G.FateOfTheCallisto), "level 35");
            harness.Drain();

            SetLevel(harness, 36);
            Accept(harness, sonia, G.FateOfTheCallisto);
        }

        /// <summary>Sonia's: the flight recorder by the wreck, then her father in the cave; he takes it.</summary>
        [TestMethod]
        public void TheFlightRecorderLeadsToProfessorAnand()
        {
            using var harness = Create();
            var sonia = Put(harness, G.Sonia, G.SoniaAt);
            Accept(harness, sonia, G.FateOfTheCallisto);
            Assert.AreNotEqual(MissionObjectiveState.Incomplete, Objective(harness, G.FateOfTheCallisto, G.FindCrew), "not yet");

            var recorder = harness.Map.DynamicObjects.SingleOrDefault(obj => obj.SceneMissionId == G.FateOfTheCallisto && obj.SceneActorRole == "flight-recorder");
            Assert.IsNotNull(recorder, "the flight recorder");
            Assert.AreEqual(G.RecorderAt.X, recorder.Position.X, 0.5);
            harness.MoveTo(recorder.Position + new Vector3(2, 0, 0));
            harness.Drain();
            new NpcManager(harness, harness.Manager).RequestNpcConverse(harness.Client,
                new RequestNPCConversePacket { EntityId = recorder.EntityId, ActionId = ActionId.Converse, ActionArgId = 1 });
            new NpcManager(harness, harness.Manager).CompleteNPCObjective(harness.Client,
                new CompleteNPCObjectivePacket { EntityId = recorder.EntityId, MissionId = G.FateOfTheCallisto, ObjectiveId = G.FindRecorder, PlayerFlagId = 1 });
            harness.Drain();
            Assert.AreEqual(MissionObjectiveState.Completed, Objective(harness, G.FateOfTheCallisto, G.FindRecorder));
            Assert.AreEqual(MissionObjectiveState.Incomplete, Objective(harness, G.FateOfTheCallisto, G.FindCrew));

            var professor = Put(harness, G.Professor, G.ProfessorAt);
            Talk(harness, professor, G.FateOfTheCallisto, G.FindCrew);
            Reward(harness, professor, G.FateOfTheCallisto, 3, G.ReflectiveVest, G.CallistoExperience, (int)G.CallistoCredits);
        }

        /// <summary>The professor's: the Guardian Stone, the chamber's door opened by it, the Logos within.</summary>
        [TestMethod]
        public void TheGuardianStoneOpensTheChamberOfEternity()
        {
            using var harness = Create();
            Work(harness);
            var door = GuardianProminence.Door(harness.Map);
            Assert.IsNotNull(door);
            Assert.AreEqual(G.DoorName, door.Object.NameOverrideId, "The Chamber Of Eternity");
            var west = G.DoorAt + new Vector3(-2f, 0.5f, 0);
            var east = G.DoorAt + new Vector3(2f, 0.5f, 0);
            Assert.IsTrue(ForceFields.StopsPlayer(harness.Map, west, east), "shut");
            Assert.IsTrue(ForceFields.Stops(harness.Map, Put(harness, G.EliteSoldier, west), west, east), "shut to the Bane too");

            // Without the stone, it stays shut.
            harness.MoveTo(G.DoorAt + new Vector3(-4f, 0.5f, 0));
            Work(harness);
            Assert.IsFalse(GuardianProminence.DoorOpen(harness.Map), "no stone");

            Completed(harness, G.FateOfTheCallisto);
            var professor = Put(harness, G.Professor, G.ProfessorAt);
            Accept(harness, professor, G.ChamberOfEternity);
            Assert.AreEqual(1, Held(harness, G.GuardianStoneTemplate), "the Guardian Stone");

            harness.MoveTo(G.DoorAt + new Vector3(-4f, 0.5f, 0));
            Work(harness);
            Assert.IsTrue(GuardianProminence.DoorOpen(harness.Map), "the stone opens it");
            Assert.IsFalse(ForceFields.StopsPlayer(harness.Map, west, east), "open");

            Assert.AreEqual(MissionObjectiveState.Incomplete, Objective(harness, G.ChamberOfEternity, G.RetrieveLogos));
            Assert.IsTrue(CharacterManager.Instance.TryAddLogos(harness.Client, G.FutureLogos));
            harness.Drain();
            Assert.AreEqual(MissionObjectiveState.Completed, Objective(harness, G.ChamberOfEternity, G.RetrieveLogos));

            harness.MoveTo(G.ProfessorAt + new Vector3(2, 0, 0));
            Reward(harness, professor, G.ChamberOfEternity, 0, G.DirectRepairTool, G.ChamberExperience, (int)G.ChamberCredits);
            Assert.AreEqual(0, Held(harness, G.GuardianStoneTemplate), "the stone goes back");

            Work(harness);
            Assert.IsTrue(GuardianProminence.DoorOpen(harness.Map), "and it stays open");
        }

        /// <summary>Cronauer's: each tower destroyed is its objective and stays down; turned in, his reinforcements come.</summary>
        [TestMethod]
        public void TheJammingTowersDownBringTheReinforcements()
        {
            using var harness = Create();
            var cronauer = Put(harness, G.Cronauer, G.CronauerAt);
            Accept(harness, cronauer, G.NoSoapRadio);
            Work(harness);

            var towers = GuardianProminence.Towers(harness.Map);
            Assert.HasCount(5, towers);
            Assert.IsTrue(towers.All(live => live?.Object?.NameOverrideId == G.TowerName), "Biomechanical Jamming Tower");
            for (var i = 0; i < towers.Count; i++)
            {
                Assert.AreEqual(MissionObjectiveState.Incomplete, Objective(harness, G.NoSoapRadio, G.Towers[i].Objective), G.Towers[i].Where);
                Assert.IsTrue(PlacedDestructibles.Destroy(towers[i], harness.Client.Player));
                Work(harness);
                Work(harness);
                Assert.AreEqual(MissionObjectiveState.Completed, Objective(harness, G.NoSoapRadio, G.Towers[i].Objective), G.Towers[i].Where);
                Assert.AreEqual(long.MaxValue, towers[i].RespawnAt, "it stays down");
            }

            Assert.IsEmpty(GuardianProminence.Reinforcements(harness.Map), "not before he radios in");
            harness.MoveTo(G.CronauerAt + new Vector3(2, 0, 0));
            Reward(harness, cronauer, G.NoSoapRadio, 1, G.LeechGun, G.RadioExperience, (int)G.RadioCredits);

            Work(harness);
            var reinforcements = GuardianProminence.Reinforcements(harness.Map);
            Assert.HasCount(G.Reinforcements.Length + 1, reinforcements, "four soldiers and a mech");
            Assert.IsTrue(reinforcements.All(creature => creature.ImmuneToAllDamage && creature.TargetCategory == TargetCategory.Friendly));
            Work(harness);
            Assert.HasCount(G.Reinforcements.Length + 1, GuardianProminence.Reinforcements(harness.Map), "once");
        }

        /// <summary>Danchekker's: taken, the Juggernauts march on the camp; the three commanders dead; he takes it.</summary>
        [TestMethod]
        public void TheDogsOfWarAndTheThreeCommanders()
        {
            using var harness = Create();
            Work(harness);
            Assert.IsFalse(GuardianProminence.AssaultStarted(harness.Map));

            var danchekker = Put(harness, G.Danchekker, G.DanchekkerAt);
            Assert.IsTrue(danchekker.KeptFromCreatures, "no Bane goes looking for him");
            Accept(harness, danchekker, G.ChainOfCommand);
            Work(harness);
            Assert.IsTrue(GuardianProminence.AssaultStarted(harness.Map));
            var juggernauts = GuardianProminence.Juggernauts(harness.Map);
            Assert.HasCount(G.JuggernautsFrom.Length, juggernauts);
            foreach (var juggernaut in juggernauts)
            {
                Assert.AreEqual(TargetCategory.Hostile, juggernaut.TargetCategory);
                Assert.IsNotNull(juggernaut.Controller.ScriptedMove, "marching");
                Assert.IsLessThan(2f, Vector3.Distance(juggernaut.Controller.ScriptedMove.Destination, G.Camp), "on the camp");
            }

            foreach (var (boss, at, objective) in new[] { (G.Horga, G.HorgaAt, G.KillHorga), (G.Zor, G.ZorAt, G.KillZor), (G.Lurge, G.LurgeAt, G.KillLurge) })
            {
                Assert.AreEqual(MissionObjectiveState.Incomplete, Objective(harness, G.ChainOfCommand, objective));
                WildernessDaghdasUrnTests.Kill(harness, Put(harness, boss, at));
                Assert.AreEqual(MissionObjectiveState.Completed, Objective(harness, G.ChainOfCommand, objective), $"{boss}");
            }

            harness.MoveTo(G.DanchekkerAt + new Vector3(2, 0, 0));
            Reward(harness, danchekker, G.ChainOfCommand, 2, G.EclipseRifle, G.ChainExperience, (int)G.ChainCredits);

            // Once a copy.
            Work(harness);
            Assert.HasCount(G.JuggernautsFrom.Length, GuardianProminence.Juggernauts(harness.Map));
        }

        #region Helpers

        private static long Count(Rasa.Test.Database.OrdinaryLootTestDatabase database, string sql) =>
            database.Context.Database.SqlQueryRaw<long>(sql).ToArray().Single();

        private static WildernessRuntimeTestHarness Create(byte level = 38)
        {
            var harness = WildernessRuntimeTestHarness.Create(mapContextId: G.Prominence);
            try
            {
                using (var unit = harness.CreateChar())
                    unit.ExecuteTransaction(() => unit.CharacterStartingExperience.Add(
                        new CharacterStartingExperienceEntry(harness.Client.Player.Id, G.Revision, CharacterStartingExperienceState.Skipped)));
                harness.Client.Player.StartingExperienceCompleted = true;
                SetLevel(harness, level);
                harness.Drain();
                return harness;
            }
            catch
            {
                harness.Dispose();
                throw;
            }
        }

        private static void SetLevel(WildernessRuntimeTestHarness harness, byte level)
        {
            using var unit = harness.CreateChar();
            unit.Characters.UpdateCharacterProgression(harness.Client.Player.Id, harness.Client.Player.Experience, level);
            harness.Client.Player.Level = level;
        }

        private static MissionObjectiveState Objective(WildernessRuntimeTestHarness harness, uint missionId, uint objectiveId) =>
            harness.Client.Player.Missions.TryGetValue(missionId, out var log) && log.Objectives.TryGetValue(objectiveId, out var objective)
                ? objective.State
                : MissionObjectiveState.NotAssigned;

        private static Creature Put(WildernessRuntimeTestHarness harness, uint dbId, Vector3 position)
        {
            var creature = CreatureManager.Instance.CreateCreature(dbId, null);
            CreatureManager.Instance.SetLocation(creature, NavMeshManager.SnapToGround(harness.Map, position), 0, harness.Map.MapInfo.MapContextId);
            CellManager.Instance.AddToWorld(harness.Map, creature);
            return creature;
        }

        private static void Work(WildernessRuntimeTestHarness harness)
        {
            GuardianProminence.Worker(harness.Map);
            harness.Drain();
        }

        private static void Completed(WildernessRuntimeTestHarness harness, uint missionId)
        {
            using var unit = harness.CreateChar();
            unit.ExecuteTransaction(() => unit.CharacterMissions.Runtime.Archive(
                new CharacterMissionEntry(harness.Client.Player.Id, missionId, (uint)MissionState.Completed)
                {
                    AssignmentId = Guid.NewGuid().ToString("N"),
                    Generation = 1,
                    ContentRevision = G.Revision
                }, harness.UtcNow));
            harness.Client.Player.MissionHistory[missionId] = MissionState.Completed;
            harness.Client.Player.MissionSuccessHistory.Add(missionId);
        }

        private static string NavmeshDirectory()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);

            while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "navmesh")))
                directory = directory.Parent;

            Assert.IsNotNull(directory, "navmesh not found");
            return Path.Combine(directory.FullName, "navmesh");
        }

        #endregion
    }
}
