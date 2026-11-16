using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Rasa.Data;
using Rasa.Managers;
using Rasa.Missions.Definitions;
using Rasa.Missions.Runtime;
using Rasa.Packets.MapChannel.Client;
using Rasa.Packets.MapChannel.Server;
using Rasa.Structures;
using Rasa.Structures.Char;
using Rasa.Structures.World;
using Rasa.Test.Database;
using Rasa.Test.Missions.Wilderness;

namespace Rasa.Test.Missions
{
    using T = Rasa.Services.Preloader.PurgasStationContent;
    using M = Rasa.Services.Preloader.Missions.Purgas.PurgasStationMissionsV1;
    using P = Rasa.Managers.PurgasStation;

    /// <summary>
    /// Purgas Station's top floor as Ten Ton Hammer's walkthrough tells it (Purgas_station_level_one:
    /// PurgasStationContent, PurgasStationMissionsV1; the Game's PurgasStation): its rows and the
    /// station's Bane at level 40 and over, A New Plan of Attack through the CommLink's pages, Off Grid
    /// with the waypoint it activates, and Chain Reaction's five Deathtribe.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class PurgasStationTests
    {
        private const string Migration = "20271128000000_Purgas_station_level_one";
        private const string Previous = "20271127000000_Tahrendra_base";

        private static readonly string[] Tables =
        {
            "creature", "creature_stat", "creature_appearance", "npc_package", "spawnpool", "world_usable", "world_destructible",
            "teleporter", "mission_content_definition", "mission_objective_definition", "mission_trigger", "mission_action",
            "mission_scene_binding", "mission_channel_policy", "mission_indicator", "mission_reward_item"
        };

        private long _now = 1_000_000;
        private readonly List<uint> _granted = new();

        [TestInitialize]
        public void Clock()
        {
            P.Reset();
            P.Now = () => _now;
            P.GrantWaypoint = (client, id) => _granted.Add(id);
        }

        [TestCleanup]
        public void Restore()
        {
            P.Reset();
            P.Now = () => Environment.TickCount64;
            P.GrantWaypoint = (client, id) => DynamicObjectManager.Instance.CheckPlayerWaypoint(client, new WaypointInfo(id, false, WaypointType.Waypoint));
            PlacedUsables.Load(null);
            PlacedUsables.Reset();
            PlacedDestructibles.Load(null);
            PlacedDestructibles.Reset();
        }

        #region The rows

        [TestMethod]
        public void TheTopFloorGoesInAndDownTakesItOutExactly()
        {
            using var database = new OrdinaryLootTestDatabase();
            var migrator = database.Context.GetService<IMigrator>();

            migrator.Migrate(Previous);
            var before = Tables.ToDictionary(table => table, table => Dump(database, table));

            migrator.Migrate(Migration);

            Assert.AreEqual(T.Creatures.Length, Count(database, "SELECT COUNT(*) FROM creature WHERE id BETWEEN 581001 AND 581099"));
            Assert.AreEqual(T.Pools.Length, Count(database, "SELECT COUNT(*) FROM spawnpool WHERE id BETWEEN 581101 AND 581199 AND map_context_id = 1806"));
            Assert.AreEqual(T.Usables.Length, Count(database, "SELECT COUNT(*) FROM world_usable WHERE id BETWEEN 581001 AND 581099 AND map_context_id = 1806"));
            Assert.AreEqual(1, Count(database, "SELECT COUNT(*) FROM world_destructible WHERE id = 581001 AND map_context_id = 1806 AND sealed = 1"));
            Assert.AreEqual(1, Count(database, $"SELECT COUNT(*) FROM npc_package WHERE id = {T.Keen} AND package_id = {T.KeenPackage}"));
            Assert.AreEqual(1, Count(database, $"SELECT COUNT(*) FROM npc_package WHERE id = {T.Taylor} AND package_id = {T.TaylorPackage}"));
            Assert.AreEqual(1, Count(database, "SELECT COUNT(*) FROM teleporter WHERE id = 79 AND map_context_id = 1806 AND type = 2 AND class_id = 29648"));

            // The station's Bane no longer the Divide's level 15-18: every hostile creature its pools put down is 40 or over.
            Assert.AreEqual(0, Count(database,
                "SELECT COUNT(*) FROM spawnpool s JOIN creature c ON c.id IN (s.creature_1_Id, s.creature_2_Id, s.creature_3_Id, s.creature_4_Id, s.creature_5_Id, s.creature_6_Id) " +
                "WHERE s.map_context_id = 1806 AND c.faction = 0 AND c.level < 40"));
            Assert.AreEqual(7, Count(database, $"SELECT COUNT(*) FROM spawnpool WHERE id BETWEEN 592245 AND 592251 AND creature_1_Id = 592012"));

            Assert.AreEqual(3, Count(database, $"SELECT COUNT(*) FROM mission_content_definition WHERE content_revision = '{M.Revision}' AND enabled = 1"));

            // The world's rows a second time: nothing.
            foreach (var sql in T.Up())
                Assert.AreEqual(0, database.Context.Database.ExecuteSqlRaw(sql), sql);

            migrator.Migrate(Previous);

            foreach (var table in Tables)
                CollectionAssert.AreEqual(before[table], Dump(database, table), $"Down puts {table} back as it was");
        }

        [TestMethod]
        public void OnMySqlTheTopFloorGoesInAndComesOut()
        {
            using var context = PersistenceIntegrationTests.CreateContext(typeof(Rasa.Context.World.MySqlWorldContext), "unused");
            var migrator = context.GetService<IMigrator>();
            var up = migrator.GenerateScript(Previous, Migration);
            var down = migrator.GenerateScript(Migration, Previous);

            foreach (var sql in T.Up().Concat(M.Up()))
                StringAssert.Contains(up, sql);
            foreach (var sql in M.Down().Concat(T.Down()))
                StringAssert.Contains(down, sql);
        }

        [TestMethod]
        public void TheThreeMissionsAreOperationalAndTheCommLinkSpeaksOnlyHere()
        {
            using var harness = Create();

            foreach (var missionId in M.All)
            {
                Assert.IsTrue(harness.Manager.TryGetOperationalMission(missionId, out var mission), $"mission {missionId}");
                Assert.AreEqual(M.Category, mission.CategoryId);
            }

            // Stevens at Foreas Base has the CommLink's package: its pages are this map's alone.
            Assert.IsTrue(harness.Manager.TryGetOperationalMission(M.NewPlanOfAttack, out var plan));
            Assert.AreEqual(4, plan.Dialogue.Count);
            foreach (var topic in plan.Dialogue)
                Assert.IsTrue(((AllRequirements)topic.Requirement).Items.OfType<MapRequirement>()
                    .Any(map => map.Maps.SequenceEqual(new[] { T.Map })), $"flag {topic.PlayerFlagId}");
        }

        [TestMethod]
        public void EveryMarkerIsSentInTheStationAndStandsOnItsGround()
        {
            using var harness = Create();
            var catalog = new MissionContentCatalog(harness, new Dictionary<uint, Mission>(), null);
            catalog.Load();
            var nav = harness.Map.NavMesh;
            var markers = 0;

            foreach (var missionId in M.All)
                foreach (var objective in catalog.Missions[missionId].Objectives.Values)
                    foreach (var pin in objective.Indicators)
                    {
                        var what = $"{missionId}/{objective.ObjectiveId}/{pin.IndicatorId}";
                        Assert.AreEqual(T.Map, MissionMarkerProjection.ResolveMap(catalog, missionId, objective.ObjectiveId, pin), what);
                        var ground = nav.GroundHeight(pin.Position + new Vector3(0, 0.5f, 0));
                        Assert.IsNotNull(ground, what);
                        Assert.AreEqual(pin.Position.Y, ground.Value, 1.5, what);
                        markers++;
                    }

            Assert.IsGreaterThan(5, markers);
        }

        [TestMethod]
        public void EveryPoolAndPlaceOfTheTopFloorStandsOnItsNavmesh()
        {
            using var harness = Create();
            var nav = harness.Map.NavMesh;
            var spots = T.Pools.Select(pool => (pool.At, pool.Comment))
                .Concat(new[] { (T.CommLinkAt, "the CommLink"), (T.ReleaseAt, "the Force Field Controller"), (T.TerminalAt, "the Waypoint Activation Terminal"),
                    (T.StorageChamberAt, "the Level 2 waypoint"), (T.CellDoor, "the holding room's doorway") });

            foreach (var (at, what) in spots)
            {
                var ground = nav.GroundHeight(new Vector3((float)at.X, (float)at.Y + 0.5f, (float)at.Z));
                Assert.IsNotNull(ground, what);
                Assert.AreEqual(at.Y, ground.Value, 1.0, what);
            }

            // The holding room's inside is walked to only through the doorway the field stands in.
            var keen = new Vector3((float)T.KeenAt.X, (float)T.KeenAt.Y, (float)T.KeenAt.Z);
            var path = nav.FindPath(nav.Nearest(new Vector3(87.8f, 65.9f, 221.6f)).Value, nav.Nearest(keen).Value, out var complete);
            Assert.IsTrue(complete, "the holding room from the entrance");
            var crossings = path.Zip(path.Skip(1), (a, b) => (a, b)).Where(leg => (leg.a.Z - (float)T.CellDoor.Z) * (leg.b.Z - (float)T.CellDoor.Z) <= 0)
                .Select(leg => leg.a.X + (leg.b.X - leg.a.X) * ((float)T.CellDoor.Z - leg.a.Z) / (leg.b.Z - leg.a.Z == 0 ? 1 : leg.b.Z - leg.a.Z))
                .Where(x => Math.Abs(x - (float)T.CellDoor.X) < 16).ToList();
            Assert.IsNotEmpty(crossings, "across the doorway's line, by the room");
            Assert.IsTrue(crossings.All(x => Math.Abs(x - (float)T.CellDoor.X) < 4), $"through its doorway: {string.Join(", ", crossings)}");
        }

        #endregion

        #region A New Plan of Attack

        [TestMethod]
        public void TheSquadFoundReleasedTheCodeFromKeenAndHqContactedAtTheCommLink()
        {
            using var harness = Create();
            var state = Enter(harness);
            var stevens = Put(harness, T.Stevens, new Vector3(150f, 73.73f, 128f));

            WildernessTwinPillarsTests.Accept(harness, stevens, M.NewPlanOfAttack);
            var commLink = harness.Map.DynamicObjects.SingleOrDefault(obj => obj.SceneMissionId == M.NewPlanOfAttack && obj.SceneActorRole == M.CommLinkRole);
            Assert.IsNotNull(commLink, "the CommLink");
            Assert.AreEqual(M.CommLinkClass, (uint)commLink.EntityClassId);

            // The holding room, then the CommLink.
            harness.MoveTo(new Vector3(192f, 65.5f, 152f));
            Work(harness);
            Assert.AreEqual(MissionObjectiveState.Completed, Objective(harness, M.NewPlanOfAttack, 4));
            Assert.AreEqual(MissionObjectiveState.Incomplete, Objective(harness, M.NewPlanOfAttack, 9));
            Assert.AreEqual(MissionObjectiveState.Inactive, Objective(harness, M.NewPlanOfAttack, M.ContactHq));

            harness.MoveTo(commLink.Position + new Vector3(0, 0, 2));
            Work(harness);
            Assert.AreEqual(MissionObjectiveState.Completed, Objective(harness, M.NewPlanOfAttack, 11));
            Assert.AreEqual(MissionObjectiveState.Incomplete, Objective(harness, M.NewPlanOfAttack, M.ContactHq));

            // Not fully decrypted: a random code does nothing; decrypting again melts the device into it.
            Assert.AreEqual((M.ContactHq, M.RootFlag), Page(OpenCommLink(harness, commLink)));
            Assert.AreEqual((M.ContactHq, M.RandomCodeFlag), Page(Choose(harness, commLink, M.RootFlag, 2)));
            Assert.AreEqual((M.ContactHq, M.RootFlag), Page(Choose(harness, commLink, M.RandomCodeFlag, 1)));
            Assert.AreEqual((M.ContactHq, M.MeltedFlag), Ambient(Choose(harness, commLink, M.RootFlag, 1)));
            Assert.AreEqual(MissionObjectiveState.Incomplete, Objective(harness, M.NewPlanOfAttack, M.ContactHq));

            // The field cannot be shot down; the controller takes it down.
            var field = PlacedDestructibles.LiveOf(harness.Map, T.CellField);
            Assert.IsTrue(field.Field.Sealed, "the holding room's field is sealed");
            Use(harness, PlacedUsables.LiveOf(harness.Map, T.ReleaseSwitch).Object);
            Work(harness, 1100);
            Assert.IsTrue(state.Released);
            Assert.IsTrue(field.Field.IsDestroyed, "the field is down");
            Assert.AreEqual(MissionObjectiveState.Completed, Objective(harness, M.NewPlanOfAttack, 9));
            Assert.AreEqual(MissionObjectiveState.Incomplete, Objective(harness, M.NewPlanOfAttack, M.ObtainCode));
            Assert.Contains(PlayerMessage.PmUseObjectNotUsable, UseAndHearRefusals(harness, PlacedUsables.LiveOf(harness.Map, T.ReleaseSwitch).Object), "once only");

            // Keen's code, and HQ.
            var keen = Spawn(harness, T.KeenPool, T.Keen);
            WildernessTwinPillarsTests.Talk(harness, keen, M.NewPlanOfAttack, M.ObtainCode);
            Assert.AreEqual(MissionObjectiveState.Completed, Objective(harness, M.NewPlanOfAttack, M.ObtainCode));

            var completion = OpenCommLink(harness, commLink);
            Assert.IsNotNull(completion, "the CommLink with the code");
            var completeable = (List<CompleteableObjectives>)completion.ConvoDataDict[ConversationType.ObjectiveComplete];
            Assert.AreEqual((int)M.CodeEnteredFlag, completeable.Single().PlayerFlagId);
            new NpcManager(harness, harness.Manager).CompleteNPCObjective(harness.Client, new CompleteNPCObjectivePacket
            {
                EntityId = commLink.EntityId, MissionId = M.NewPlanOfAttack, ObjectiveId = M.ContactHq, PlayerFlagId = M.CodeEnteredFlag
            });
            harness.Drain();
            Assert.AreEqual(MissionObjectiveState.Completed, Objective(harness, M.NewPlanOfAttack, M.ContactHq));
            Assert.IsTrue(harness.Manager.TryCompleteRadioMission(harness.Client, M.NewPlanOfAttack, 0, null), "radioed in");
        }

        [TestMethod]
        public void TheControllerWorksForAnyoneAndTheSquadLaterCaughtUp()
        {
            using var harness = Create();
            var state = Enter(harness);

            Use(harness, PlacedUsables.LiveOf(harness.Map, T.ReleaseSwitch).Object);
            Assert.IsTrue(state.Released, "the field down without the mission");

            var stevens = Put(harness, T.Stevens, new Vector3(150f, 73.73f, 128f));
            WildernessTwinPillarsTests.Accept(harness, stevens, M.NewPlanOfAttack);
            harness.MoveTo(new Vector3(192f, 65.5f, 152f));
            Work(harness, 1100);
            Work(harness, 1100);

            Assert.AreEqual(MissionObjectiveState.Completed, Objective(harness, M.NewPlanOfAttack, 9), "released already");
            Assert.AreEqual(MissionObjectiveState.Incomplete, Objective(harness, M.NewPlanOfAttack, M.ObtainCode));
        }

        #endregion

        #region Off Grid

        [TestMethod]
        public void OffGridTaylorTheWaypointTheKeypassAndTheFarSide()
        {
            using var harness = Create();
            var state = Enter(harness);

            // The terminal waits for Off Grid.
            Assert.Contains(PlayerMessage.PmCannotPerformActionNow, UseAndHearRefusals(harness, PlacedUsables.LiveOf(harness.Map, T.WaypointTerminal).Object));
            Assert.IsFalse(state.WaypointActive);

            Completed(harness, M.NewPlanOfAttack);
            Work(harness, 1100);
            Assert.IsTrue(state.CrewCalled);
            Assert.IsTrue(Pool(harness, T.TaylorPool).Woken, "Taylor ported in");
            Assert.IsTrue(harness.Manager.TryAcceptRadioMission(harness.Client, M.OffGrid), "offered by radio here");
            harness.Drain();

            var taylor = Spawn(harness, T.TaylorPool, T.Taylor);
            WildernessTwinPillarsTests.Talk(harness, taylor, M.OffGrid, 6);
            Assert.AreEqual(MissionObjectiveState.Incomplete, Objective(harness, M.OffGrid, 10));

            Use(harness, PlacedUsables.LiveOf(harness.Map, T.WaypointTerminal).Object);
            Work(harness, 1100);
            Assert.IsTrue(state.WaypointActive);
            Assert.Contains(T.StorageChamberWaypoint, _granted, "the Level 2 waypoint given");
            Assert.AreEqual(MissionObjectiveState.Completed, Objective(harness, M.OffGrid, 10));
            Assert.AreEqual(MissionObjectiveState.Incomplete, Objective(harness, M.OffGrid, 11));

            Give(harness, M.KeypassTemplate);
            Work(harness, 1100);
            Assert.AreEqual(MissionObjectiveState.Completed, Objective(harness, M.OffGrid, 11));

            harness.MoveTo(new Vector3((float)T.FarSideAt.X, (float)T.FarSideAt.Y, (float)T.FarSideAt.Z));
            Work(harness);
            Assert.AreEqual(MissionObjectiveState.Completed, Objective(harness, M.OffGrid, 9));
            Assert.IsTrue(harness.Manager.TryCompleteRadioMission(harness.Client, M.OffGrid, 0, null), "radioed in");
        }

        #endregion

        #region Chain Reaction

        [TestMethod]
        public void TheFirstDeathtribeDeadOffersChainReactionAndAllFiveCountForIt()
        {
            using var harness = Create();
            var state = Enter(harness);

            Work(harness, 1100);
            Assert.IsFalse(harness.Manager.TryAcceptRadioMission(harness.Client, M.ChainReaction), "not before one is dead");

            Pool(harness, T.DeathtribePool(1)).HasSpawned = true;
            Work(harness, 1100);
            Assert.IsTrue(state.DeathtribeDown[0]);
            Assert.IsTrue(harness.Manager.TryAcceptRadioMission(harness.Client, M.ChainReaction), "radioed in on the first");
            harness.Drain();
            Work(harness, 1100);
            Assert.AreEqual(MissionObjectiveState.Incomplete, Objective(harness, M.ChainReaction, 1), "one of five, the one before it was taken");

            for (uint n = 2; n <= 5; n++)
                Pool(harness, T.DeathtribePool(n)).HasSpawned = true;
            Work(harness, 1100);
            Work(harness, 1100);

            Assert.AreEqual(MissionObjectiveState.Completed, Objective(harness, M.ChainReaction, 1));
            var stevens = Put(harness, T.Stevens, new Vector3(150f, 73.73f, 128f));
            WildernessTwinPillarsTests.Reward(harness, stevens, M.ChainReaction, 0, 19286, 61500, 6150);
        }

        #endregion

        #region Helpers

        private static WildernessRuntimeTestHarness Create()
        {
            var harness = WildernessRuntimeTestHarness.Create(mapContextId: T.Map);
            try
            {
                using var unit = harness.CreateChar();
                unit.ExecuteTransaction(() => unit.CharacterStartingExperience.Add(
                    new CharacterStartingExperienceEntry(harness.Client.Player.Id, M.Revision,
                        CharacterStartingExperienceState.Skipped)));
                harness.Client.Player.StartingExperienceCompleted = true;
                harness.Drain();
                return harness;
            }
            catch
            {
                harness.Dispose();
                throw;
            }
        }

        /// <summary>The station's usables and the field down on the copy, as its coming up puts them, and its state.</summary>
        private P.State Enter(WildernessRuntimeTestHarness harness)
        {
            PlacedUsables.Load(harness.World.Set<WorldUsableEntry>().AsNoTracking().Where(row => row.MapContextId == T.Map).ToList());
            PlacedUsables.Place(harness.Map);
            PlacedDestructibles.Load(harness.World.Set<WorldDestructibleEntry>().AsNoTracking().Where(row => row.MapContextId == T.Map).ToList());
            PlacedDestructibles.Place(harness.Map);

            Work(harness);
            return P.StateOf(harness.Map);
        }

        private void Work(WildernessRuntimeTestHarness harness, long ms = 100)
        {
            _now += ms;
            P.Worker(harness.Map);
            harness.Drain();
        }

        private static SpawnPool Pool(WildernessRuntimeTestHarness harness, uint id) => harness.Map.SpawnPools.Single(pool => pool.DbId == id);

        private static Creature Spawn(WildernessRuntimeTestHarness harness, uint poolId, uint creatureId)
        {
            harness.SpawnWorld(poolId);
            return WildernessTwinPillarsTests.RequireNpc(harness, poolId, creatureId);
        }

        private static Creature Put(WildernessRuntimeTestHarness harness, uint dbId, Vector3 position)
        {
            var creature = CreatureManager.Instance.CreateCreature(dbId, null);
            CreatureManager.Instance.SetLocation(creature, NavMeshManager.SnapToGround(harness.Map, position), 0, T.Map);
            CellManager.Instance.AddToWorld(harness.Map, creature);
            return creature;
        }

        private static void Give(WildernessRuntimeTestHarness harness, uint template)
        {
            var item = ItemManager.Instance.CreateFromTemplateId(template, 1);
            Assert.IsNotNull(InventoryManager.Instance.GrantItemToInventory(harness.Client, item), $"template {template}");
            harness.Drain();
        }

        private static void Use(WildernessRuntimeTestHarness harness, DynamicObject obj)
        {
            harness.MoveTo(obj.Position + new Vector3(1, 0, 0));
            harness.Objects.RequestUseObjectPacket(harness.Client, new RequestUseObjectPacket
            {
                EntityId = obj.EntityId,
                ActionId = ActionId.UseObject,
                ActionArgId = DynamicObjectManager.FootlockerUseArgId
            });
            ActorActionManager.Instance.DoWork(harness.Map, 10000);
            harness.Drain();
        }

        private static List<PlayerMessage> UseAndHearRefusals(WildernessRuntimeTestHarness harness, DynamicObject obj)
        {
            harness.MoveTo(obj.Position + new Vector3(1, 0, 0));
            harness.Drain();
            harness.Objects.RequestUseObjectPacket(harness.Client, new RequestUseObjectPacket
            {
                EntityId = obj.EntityId,
                ActionId = ActionId.UseObject,
                ActionArgId = DynamicObjectManager.FootlockerUseArgId
            });
            ActorActionManager.Instance.DoWork(harness.Map, 10000);
            return harness.Drain().OfType<UserActionFailedPacket>().Where(failed => failed.MsgId.HasValue).Select(failed => failed.MsgId.Value).ToList();
        }

        private static ConversePacket OpenCommLink(WildernessRuntimeTestHarness harness, DynamicObject commLink)
        {
            harness.MoveTo(commLink.Position + new Vector3(0, 0, 1));
            harness.Drain();
            new NpcManager(harness, harness.Manager).RequestNpcConverse(harness.Client,
                new RequestNPCConversePacket { EntityId = commLink.EntityId, ActionId = ActionId.Converse, ActionArgId = 1 });
            return harness.Drain().OfType<ConversePacket>().LastOrDefault(packet =>
                packet.ConvoDataDict.ContainsKey(ConversationType.ObjectiveChoice) ||
                packet.ConvoDataDict.ContainsKey(ConversationType.ObjectiveAmbient) ||
                packet.ConvoDataDict.ContainsKey(ConversationType.ObjectiveComplete));
        }

        /// <summary>A choice at the CommLink; the page it shows after it.</summary>
        private static ConversePacket Choose(WildernessRuntimeTestHarness harness, DynamicObject commLink, uint flag, int choice)
        {
            var npcs = new NpcManager(harness, harness.Manager);
            harness.MoveTo(commLink.Position + new Vector3(0, 0, 1));
            harness.Drain();
            npcs.RequestNpcConverse(harness.Client, new RequestNPCConversePacket { EntityId = commLink.EntityId, ActionId = ActionId.Converse, ActionArgId = 1 });
            harness.Drain();
            npcs.PerformNPCChoice(harness.Client, new PerformNPCChoicePacket
            {
                EntityId = commLink.EntityId, MissionId = M.NewPlanOfAttack, ObjectiveId = M.ContactHq, PlayerFlagId = flag, ChoiceIdx = choice
            });
            harness.Drain();
            return OpenCommLink(harness, commLink);
        }

        private static (uint, uint) Page(ConversePacket packet)
        {
            Assert.IsNotNull(packet, "a page shown");
            var page = ((List<ChoiceObjectives>)packet.ConvoDataDict[ConversationType.ObjectiveChoice]).Single();
            return ((uint)page.ObjectiveId, (uint)page.PlayerFlagId);
        }

        private static (uint, uint) Ambient(ConversePacket packet)
        {
            Assert.IsNotNull(packet, "a line shown");
            var line = ((List<AmbientObjectives>)packet.ConvoDataDict[ConversationType.ObjectiveAmbient]).Single();
            return ((uint)line.ObjectiveId, (uint)line.PlayerFlagId);
        }

        private static MissionObjectiveState Objective(WildernessRuntimeTestHarness harness, uint missionId, uint objectiveId) =>
            harness.Client.Player.Missions[missionId].Objectives[objectiveId].State;

        /// <summary>The mission done before, as a history row the server keeps.</summary>
        private static void Completed(WildernessRuntimeTestHarness harness, uint missionId)
        {
            using var unit = harness.CreateChar();
            unit.ExecuteTransaction(() => unit.CharacterMissions.Runtime.Archive(
                new CharacterMissionEntry(harness.Client.Player.Id, missionId, (uint)MissionState.Completed)
                {
                    AssignmentId = Guid.NewGuid().ToString("N"),
                    Generation = 1,
                    ContentRevision = M.Revision
                }, harness.UtcNow));
            harness.Client.Player.MissionHistory[missionId] = MissionState.Completed;
            harness.Client.Player.MissionSuccessHistory.Add(missionId);
        }

        private static long Count(OrdinaryLootTestDatabase database, string sql)
        {
            using var command = database.Context.Database.GetDbConnection().CreateCommand();
            command.CommandText = sql;
            database.Context.Database.OpenConnection();
            return Convert.ToInt64(command.ExecuteScalar());
        }

        private static List<string> Dump(OrdinaryLootTestDatabase database, string table)
        {
            using var command = database.Context.Database.GetDbConnection().CreateCommand();
            command.CommandText = $"SELECT * FROM {table} ORDER BY 1, 2";
            database.Context.Database.OpenConnection();
            using var reader = command.ExecuteReader();
            var rows = new List<string>();

            while (reader.Read())
                rows.Add(string.Join("|", Enumerable.Range(0, reader.FieldCount).Select(i => Convert.ToString(reader.GetValue(i), System.Globalization.CultureInfo.InvariantCulture))));

            return rows;
        }

        #endregion
    }
}
