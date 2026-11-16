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
    using D = Rasa.Services.Preloader.PurgasStationDepthsContent;
    using M = Rasa.Services.Preloader.Missions.Purgas.PurgasStationMissionsV1;
    using L = Rasa.Services.Preloader.Missions.Purgas.PurgasStationDepthsMissionsV1;
    using P = Rasa.Managers.PurgasStation;

    /// <summary>
    /// Purgas Station below the top floor (Purgas_station_lower_levels: PurgasStationDepthsContent,
    /// PurgasStationDepthsMissionsV1; the Game's PurgasStation): its rows, A Means to an End's basins,
    /// Reactor Overload's pumps and module, the pedestal room and the sleeping pods, the trip wires.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class PurgasStationDepthsTests
    {
        private const string Migration = "20271129000000_Purgas_station_lower_levels";
        private const string Previous = "20271128000000_Purgas_station_level_one";

        private static readonly string[] Tables =
        {
            "creature", "creature_action", "creature_stat", "spawnpool", "world_usable", "world_destructible",
            "mission_content_definition", "mission_objective_definition", "mission_trigger", "mission_action",
            "mission_scene_binding", "mission_channel_policy", "mission_indicator", "mission_reward_item"
        };

        private long _now = 1_000_000;

        [TestInitialize]
        public void Clock()
        {
            P.Reset();
            P.Now = () => _now;
            P.GrantWaypoint = (client, id) => { };
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
        public void TheLowerLevelsGoInAndDownTakesThemOutExactly()
        {
            using var database = new OrdinaryLootTestDatabase();
            var migrator = database.Context.GetService<IMigrator>();

            migrator.Migrate(Previous);
            var before = Tables.ToDictionary(table => table, table => Dump(database, table));

            migrator.Migrate(Migration);

            Assert.AreEqual(D.Creatures.Length, Count(database, $"SELECT COUNT(*) FROM creature WHERE id IN ({D.KeypassThraxThree}, {D.Juggernaut})"));
            Assert.AreEqual(D.Pools.Length, Count(database, $"SELECT COUNT(*) FROM spawnpool WHERE id IN ({string.Join(", ", D.Pools.Select(p => p.Id))}) AND map_context_id = 1806"));
            Assert.AreEqual(D.Usables.Length, Count(database, $"SELECT COUNT(*) FROM world_usable WHERE id IN ({string.Join(", ", D.Usables.Select(u => u.Id))}) AND map_context_id = 1806"));
            Assert.AreEqual(D.Destructibles.Length, Count(database, $"SELECT COUNT(*) FROM world_destructible WHERE id IN ({string.Join(", ", D.Destructibles.Select(d => d.Id))}) AND map_context_id = 1806"));
            Assert.AreEqual(1 + 5 + 4 + 3 + 3, D.Usables.Length, "the module, five pedestals and ten beams");
            Assert.AreEqual(7 + 4 + 1 + 4, D.Destructibles.Length, "seven basins, four pumps, the field and four pods");
            Assert.AreEqual(1, Count(database, $"SELECT COUNT(*) FROM world_destructible WHERE id = {D.PodField} AND sealed = 1"));
            Assert.AreEqual(1, Count(database, $"SELECT COUNT(*) FROM spawnpool WHERE id = {T.DeathtribePool(2)} AND mode = 2"));
            Assert.AreEqual(1, Count(database, $"SELECT COUNT(*) FROM spawnpool WHERE id = {D.BayPool} AND ABS(pos_x - 320) < 0.01 AND ABS(pos_z - 120) < 0.01"));
            Assert.AreEqual(2, Count(database, $"SELECT COUNT(*) FROM mission_content_definition WHERE mission_id IN (850, 852) AND content_revision = '{L.Revision}' AND enabled = 1"));

            foreach (var sql in D.Up())
                Assert.AreEqual(0, database.Context.Database.ExecuteSqlRaw(sql), sql);

            migrator.Migrate(Previous);

            foreach (var table in Tables)
            {
                var after = Dump(database, table);
                var changed = before[table].Except(after).Concat(after.Except(before[table])).Take(4);
                CollectionAssert.AreEqual(before[table], after, $"Down puts {table} back as it was: {string.Join(" / ", changed)}");
            }
        }

        [TestMethod]
        public void OnMySqlTheLowerLevelsGoInAndComeOut()
        {
            using var context = PersistenceIntegrationTests.CreateContext(typeof(Rasa.Context.World.MySqlWorldContext), "unused");
            var migrator = context.GetService<IMigrator>();
            var up = migrator.GenerateScript(Previous, Migration);
            var down = migrator.GenerateScript(Migration, Previous);

            foreach (var sql in D.Up().Concat(L.Up()))
                StringAssert.Contains(up, sql);
            foreach (var sql in L.Down().Concat(D.Down()))
                StringAssert.Contains(down, sql);
        }

        [TestMethod]
        public void EveryPlaceStandsOnTheNavmeshAndEachMarkerIsSentInTheStation()
        {
            using var harness = Create();
            var nav = harness.Map.NavMesh;
            var entrance = nav.Nearest(new Vector3(87.8f, 65.9f, 221.6f)).Value;
            var spots = D.Pools.Select(pool => (pool.At, pool.Comment))
                .Concat(D.Basins.Select((basin, i) => (basin.At, $"basin {i + 1}")))
                .Concat(D.ReactorPumps.Select((pump, i) => (pump, $"reactor pump {i + 1}")))
                .Concat(D.Pods.Select((pod, i) => (pod.At, $"pod {i + 1}")))
                .Concat(Enumerable.Range(1, 5).Select(n => (D.PedestalAt((uint)n), $"pedestal {n}")))
                .Concat(new[] { (D.ModuleAt, "the stabilization module"), (D.PodDoor, "the pedestal room's east doorway"), (D.BayPoolAt, "Juggernaut Bay's pool") });

            foreach (var (at, what) in spots)
            {
                var spot = new Vector3((float)at.X, (float)at.Y, (float)at.Z);
                var ground = nav.GroundHeight(spot + new Vector3(0, 0.5f, 0));
                Assert.IsNotNull(ground, what);
                Assert.AreEqual(at.Y, ground.Value, 1.0, what);
                nav.FindPath(entrance, nav.Nearest(spot).Value, out var complete);
                Assert.IsTrue(complete, $"{what}: reached from the entrance");
            }

            var catalog = new MissionContentCatalog(harness, new Dictionary<uint, Mission>(), null);
            catalog.Load();
            foreach (var missionId in L.All)
                foreach (var objective in catalog.Missions[missionId].Objectives.Values)
                    foreach (var pin in objective.Indicators)
                        Assert.AreEqual(T.Map, MissionMarkerProjection.ResolveMap(catalog, missionId, objective.ObjectiveId, pin), $"{missionId}/{objective.ObjectiveId}/{pin.IndicatorId}");
        }

        [TestMethod]
        public void ThePodsAreReachedOnlyThroughThePedestalRoomsField()
        {
            using var harness = Create();
            var nav = harness.Map.NavMesh;
            var door = D.PodDoor;
            var path = nav.FindPath(nav.Nearest(new Vector3(-252f, 49.2f, -58f)).Value, nav.Nearest(new Vector3(-216f, 49.33f, -16f)).Value, out var complete);
            Assert.IsTrue(complete);

            var crossings = path.Zip(path.Skip(1), (a, b) => (a, b)).Where(leg => (leg.a.X - (float)door.X) * (leg.b.X - (float)door.X) <= 0)
                .Select(leg => leg.a.Z + (leg.b.Z - leg.a.Z) * ((float)door.X - leg.a.X) / (leg.b.X - leg.a.X == 0 ? 1 : leg.b.X - leg.a.X))
                .Where(z => Math.Abs(z - (float)door.Z) < 16).ToList();
            Assert.IsNotEmpty(crossings, "across the doorway's line, by the room");
            Assert.IsTrue(crossings.All(z => Math.Abs(z - (float)door.Z) < 3), $"through its doorway: {string.Join(", ", crossings)}");
        }

        #endregion

        #region A Means to an End and Reactor Overload

        [TestMethod]
        public void TheBasinsTheKeypassThePumpsAndTheModule()
        {
            using var harness = Create();
            var state = Enter(harness);
            Completed(harness, M.OffGrid);

            // Two basins shot before the mission: the copy remembers them.
            foreach (var n in new uint[] { 1, 2 })
                Assert.IsTrue(PlacedDestructibles.Destroy(PlacedDestructibles.LiveOf(harness.Map, D.BasinId(n)), harness.Client.Player));
            Work(harness, 1100);
            Assert.IsTrue(state.BasinsDown[0] && state.BasinsDown[1]);

            Assert.IsTrue(harness.Manager.TryAcceptRadioMission(harness.Client, L.MeansToAnEnd), "offered by radio after Off Grid");
            harness.Drain();
            Work(harness, 1100);
            Assert.AreEqual(MissionObjectiveState.Incomplete, Objective(harness, L.MeansToAnEnd, 1), "two of seven");

            for (uint n = 3; n <= 7; n++)
                Assert.IsTrue(PlacedDestructibles.Destroy(PlacedDestructibles.LiveOf(harness.Map, D.BasinId(n)), harness.Client.Player));
            Work(harness, 1100);
            Work(harness, 1100);
            Assert.AreEqual(MissionObjectiveState.Completed, Objective(harness, L.MeansToAnEnd, 1));
            Assert.AreEqual(MissionObjectiveState.Incomplete, Objective(harness, L.MeansToAnEnd, 2));
            Assert.AreEqual(long.MaxValue, PlacedDestructibles.LiveOf(harness.Map, D.BasinId(7)).RespawnAt, "a basin stays down");

            Give(harness, L.KeypassTemplate);
            Work(harness, 1100);
            Assert.AreEqual(MissionObjectiveState.Completed, Objective(harness, L.MeansToAnEnd, 2));
            Assert.IsTrue(harness.Manager.TryCompleteRadioMission(harness.Client, L.MeansToAnEnd, 0, null), "radioed in");

            // Reactor Overload: the module waits for the pumps, the keypass is held already.
            Work(harness, P.OfferAgainMs + 1100);
            Assert.IsTrue(harness.Manager.TryAcceptRadioMission(harness.Client, L.ReactorOverload), "offered by radio after A Means to an End");
            harness.Drain();
            var module = PlacedUsables.LiveOf(harness.Map, D.StabilizationModule);
            Assert.Contains(PlayerMessage.PmCannotPerformActionNow, UseAndHearRefusals(harness, module.Object), "not before the pumps");

            for (uint n = 1; n <= 4; n++)
                Assert.IsTrue(PlacedDestructibles.Destroy(PlacedDestructibles.LiveOf(harness.Map, D.ReactorPumpId(n)), harness.Client.Player));
            Work(harness, 1100);
            Work(harness, 1100);
            Assert.AreEqual(MissionObjectiveState.Completed, Objective(harness, L.ReactorOverload, 1));
            Assert.AreEqual(MissionObjectiveState.Completed, Objective(harness, L.ReactorOverload, 3), "the keypass held from A Means to an End");
            Assert.AreEqual(MissionObjectiveState.Incomplete, Objective(harness, L.ReactorOverload, 2));

            Use(harness, module.Object);
            Assert.IsTrue(state.Overdriven);
            Assert.AreEqual(MissionObjectiveState.Completed, Objective(harness, L.ReactorOverload, 2));
            Assert.IsTrue(harness.Manager.TryCompleteRadioMission(harness.Client, L.ReactorOverload, 0, null), "radioed in");
        }

        #endregion

        #region The pedestal room, the pods and the trip wires

        [TestMethod]
        public void ThePedestalsInOrderDropTheFieldAWrongOneCallsSecurity()
        {
            using var harness = Create();
            var state = Enter(harness);
            var field = PlacedDestructibles.LiveOf(harness.Map, D.PodField);
            Assert.IsTrue(field.Field.Sealed);

            Use(harness, Pedestal(harness, 1));
            Use(harness, Pedestal(harness, 2));
            Work(harness);
            Assert.IsEmpty(state.Pressed, "2 is not next after 1");
            Assert.IsTrue(Pool(harness, D.PedestalSecurityPool).Woken, "security called");
            Assert.AreEqual(UseObjectState.TsState0, PlacedUsables.LiveOf(harness.Map, D.PedestalId(1)).State);

            foreach (var n in D.PedestalOrder)
                Use(harness, Pedestal(harness, n));
            Work(harness);

            Assert.IsTrue(state.PedestalsSolved);
            Assert.IsTrue(field.Field.IsDestroyed, "the field is down");
            Assert.IsTrue(Enumerable.Range(1, 5).All(n => PlacedUsables.LiveOf(harness.Map, D.PedestalId((uint)n)).State == UseObjectState.TsState1));
            Assert.Contains(PlayerMessage.PmUseObjectNotUsable, UseAndHearRefusals(harness, Pedestal(harness, 1)));
        }

        [TestMethod]
        public void OnlyTheLivePodLetsTheSecondDeathtribeOut()
        {
            using var harness = Create();
            var state = Enter(harness);
            var deathtribe = Pool(harness, T.DeathtribePool(2));

            Assert.IsTrue(PlacedDestructibles.Destroy(PlacedDestructibles.LiveOf(harness.Map, D.PodId(1)), harness.Client.Player));
            Work(harness);
            Assert.IsFalse(state.PodOpened);
            Assert.IsFalse(deathtribe.Woken);

            Assert.IsTrue(PlacedDestructibles.Destroy(PlacedDestructibles.LiveOf(harness.Map, D.PodId(D.LivePod)), harness.Client.Player));
            Work(harness);
            Assert.IsTrue(state.PodOpened);
            Assert.IsTrue(deathtribe.Woken, "the Deathtribe out of his pod");

            // Not counted for Chain Reaction until he has come out and been killed.
            Assert.IsFalse(state.DeathtribeDown[1]);
            deathtribe.HasSpawned = true;
            Work(harness);
            Assert.IsTrue(state.DeathtribeDown[1]);
        }

        [TestMethod]
        public void AFloorWireIsClearedByAJumpAStackIsNot()
        {
            var hopscotch = D.Wires[0];
            var stack = D.Wires[1];
            var z = (float)hopscotch.Lines[0];

            Assert.IsTrue(P.Crosses(hopscotch, new Vector3(208f, 81.2f, z - 0.4f), new Vector3(208f, 81.2f, z + 0.4f)), "walked across");
            Assert.IsFalse(P.Crosses(hopscotch, new Vector3(208f, 82.2f, z - 0.4f), new Vector3(208f, 82.2f, z + 0.4f)), "jumped over");
            Assert.IsFalse(P.Crosses(hopscotch, new Vector3(220f, 81.2f, z - 0.4f), new Vector3(220f, 81.2f, z + 0.4f)), "beside it");
            Assert.IsFalse(P.Crosses(hopscotch, new Vector3(208f, 81.2f, z + 0.2f), new Vector3(208f, 81.2f, z + 0.6f)), "not across");

            Assert.IsTrue(P.Crosses(stack, new Vector3(180.5f, 65.5f, 88f), new Vector3(178.5f, 65.5f, 88f)), "into the control room");
            Assert.IsTrue(P.Crosses(stack, new Vector3(180.5f, 67.5f, 88f), new Vector3(178.5f, 67.5f, 88f)), "a jump does not clear a stack");
        }

        [TestMethod]
        public void ATeleportAcrossAWireIsNoCrossing()
        {
            using var harness = Create();
            var state = Enter(harness);

            harness.MoveTo(new Vector3(200f, 65.5f, 88f));
            Work(harness);
            harness.MoveTo(new Vector3(150f, 63.73f, 88f));
            Work(harness);

            Assert.AreEqual(P.WirePhase.Armed, state.Wires[1], "50 m in one step is no walk through the beams");
        }

        [TestMethod]
        public void CrossingAWireCallsItsBaneAndItIsSetAgainOnceTheyAreDead()
        {
            using var harness = Create();
            var state = Enter(harness);
            var wire = D.Wires[1];

            harness.MoveTo(new Vector3(182f, 65.5f, 88f));
            Work(harness);
            harness.MoveTo(new Vector3(176f, 65f, 88f));
            Work(harness);

            Assert.AreEqual(P.WirePhase.Tripped, state.Wires[1]);
            Assert.AreEqual(P.WirePhase.Armed, state.Wires[0], "only the one crossed");
            Assert.IsTrue(Pool(harness, wire.AlarmPool).Woken, "its Bane called");
            Assert.AreEqual(UseObjectState.TsState0, PlacedUsables.LiveOf(harness.Map, D.WireBeamId(wire.Number, 0)).State, "its beams out");

            // Its Bane come and die; a minute on, it is set again.
            var pool = Pool(harness, wire.AlarmPool);
            pool.Woken = false;
            state.OneShot.Clear();
            Work(harness, P.WireRearmMs + 100);
            Assert.AreEqual(P.WirePhase.Armed, state.Wires[1]);
            Assert.AreEqual(UseObjectState.TsState1, PlacedUsables.LiveOf(harness.Map, D.WireBeamId(wire.Number, 0)).State, "its beams on");
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

        private static DynamicObject Pedestal(WildernessRuntimeTestHarness harness, uint n) => PlacedUsables.LiveOf(harness.Map, D.PedestalId(n)).Object;

        private static SpawnPool Pool(WildernessRuntimeTestHarness harness, uint id) => harness.Map.SpawnPools.Single(pool => pool.DbId == id);

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

        private static MissionObjectiveState Objective(WildernessRuntimeTestHarness harness, uint missionId, uint objectiveId) =>
            harness.Client.Player.Missions[missionId].Objectives[objectiveId].State;

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
