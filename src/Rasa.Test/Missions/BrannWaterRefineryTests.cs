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
    using B = Rasa.Services.Preloader.BrannWaterRefineryContent;
    using static Wilderness.WildernessTwinPillarsTests;

    /// <summary>
    /// The Brann Water Refinery as the wiki and walkthrough tell it (BrannWaterRefineryContent,
    /// BrannWaterRefinery): its rows and five missions, Lagda's escort, the passkey's doors, the
    /// hacking modules, the bombs, the skybridge, the pods, the Greenhouse and its Overseer.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class BrannWaterRefineryTests
    {
        private const string Migration = "20271121000000_Brann_water_refinery";
        private const string Previous = "20271120000000_Maligo_switch_walls";
        private const string MapName = "adv_arieki_torden_plains_brannwaterrefinery";

        [TestInitialize]
        public void Clean() => BrannWaterRefinery.Reset();

        [TestCleanup]
        public void Restore() => BrannWaterRefinery.Reset();

        [TestMethod]
        public void TheRefineryHasItsAfsItsBaneAndItsFiveMissions()
        {
            using var harness = Create();
            var world = harness.World;

            foreach (var missionId in B.Missions)
            {
                Assert.IsTrue(harness.Manager.TryGetOperationalMission(missionId, out var mission), $"mission {missionId}");
                Assert.AreEqual(B.Category, mission.CategoryId);
            }

            var packages = world.Set<NpcPackageEntry>().AsNoTracking().ToDictionary(row => row.Id, row => row.PackageId);
            Assert.AreEqual(B.LagdaPackage, packages[B.Lagda]);
            Assert.AreEqual(B.MelokanPackage, packages[B.Melokan]);
            Assert.AreEqual(B.NamistaiPackage, packages[B.Namistai]);

            var creatures = world.Set<CreatureEntry>().AsNoTracking().ToDictionary(c => c.Id);
            foreach (var c in B.Creatures.Where(c => c.Faction == B.TargetCategoryRow.Hostile))
                Assert.IsTrue(creatures[c.Id].Level is >= 20 and <= 28, $"{c.Comment} level {creatures[c.Id].Level}");

            var pools = world.Set<SpawnPoolEntry>().AsNoTracking().Where(pool => pool.MapContextId == B.Refinery).ToList();
            foreach (var boss in new[] { B.Caolots, B.Kraxor, B.Fallorak, B.Ifilion })
                Assert.AreEqual(1, pools.Count(pool => pool.Creature1Id == boss), $"{boss}");
            foreach (var quarters in new[] { B.NorthernQuartersPool, B.SouthernQuartersPool })
            {
                var pool = pools.Single(p => p.Id == quarters);
                Assert.AreEqual(B.Headhunter, pool.Creature1Id, "no level 35 Plains Thrax in the living quarters");
                Assert.AreEqual(B.Soldier, pool.Creature2Id);
            }

            var markers = world.Set<MissionIndicatorEntry>().AsNoTracking().Where(row => row.ContentRevision == B.Revision).ToList();
            Assert.IsTrue(markers.All(row => row.IndicatorId > MissionIndicator.UnnamedFrom));
        }

        [TestMethod]
        public void EveryPoolAndObjectStandsOnTheGroundAndNoNpcInAnother()
        {
            using var database = new Rasa.Test.Database.OrdinaryLootTestDatabase();
            database.Context.GetService<IMigrator>().Migrate(Migration);
            var pools = database.Context.SpawnPoolEntries.AsNoTracking().ToList();
            var ours = B.Pools.Select(p => p.Id).ToHashSet();
            var refinery = TestNavMeshes.Query(NavMeshFile.PathFor(NavmeshDirectory(), MapName));

            foreach (var pool in pools.Where(pool => ours.Contains(pool.Id)))
            {
                var at = new Vector3((float)pool.PosX, (float)pool.PosY, (float)pool.PosZ);
                var ground = refinery.Nearest(at);
                Assert.IsTrue(ground.HasValue, $"{pool.Id} off the ground");
                Assert.IsLessThan(0.6f, Vector2.Distance(new Vector2(at.X, at.Z), new Vector2(ground.Value.X, ground.Value.Z)), $"{pool.Id} over walkable ground");
                Assert.IsLessThan(1f, Math.Abs(ground.Value.Y - at.Y), $"{pool.Id}: the ground {ground.Value.Y - at.Y} m from it");
            }

            foreach (var spot in B.Modules.Select(m => m.At).Concat(B.Bombs.Select(b => b.At)).Concat(B.Pods.Select(p => p.At))
                         .Concat(B.Chairs.Select(c => c.At)).Append(B.LagdaAt).Append(B.LagdaAtOperations))
            {
                var ground = refinery.Nearest(spot);
                Assert.IsTrue(ground.HasValue && Vector2.Distance(new Vector2(spot.X, spot.Z), new Vector2(ground.Value.X, ground.Value.Z)) < 1f &&
                    Math.Abs(ground.Value.Y - spot.Y) < 1f, $"{spot} off the ground");
            }

            var npcs = pools.Where(pool => pool.MapContextId == B.Refinery &&
                (pool.Creature1Id is >= 500000 and < 520000 || B.Afs.Contains(pool.Creature1Id))).ToList();
            foreach (var pool in npcs.Where(pool => ours.Contains(pool.Id)))
                foreach (var other in npcs.Where(other => other.Id != pool.Id))
                    Assert.IsFalse(Math.Sqrt(Math.Pow(pool.PosX - other.PosX, 2) + Math.Pow(pool.PosZ - other.PosZ, 2)) < 1.5 &&
                        Math.Abs(pool.PosY - other.PosY) < 3, $"{pool.Id} in {other.Id}");
        }

        /// <summary>Each locked door stands in its jamb's middle, where the jamb is narrower than the door: nothing gets round it.</summary>
        [TestMethod]
        public void EachDoorFillsItsJamb()
        {
            var cover = CoverMesh.Read(CoverMesh.PathFor(NavmeshDirectory(), MapName));

            foreach (var door in B.Doors)
            {
                var turn = Quaternion.CreateFromYawPitchRoll(door.Yaw, 0f, 0f);
                var chest = door.At + new Vector3(0, 1.5f, 0);
                foreach (var side in new[] { -1f, 1f })
                    Assert.IsTrue(cover.GeometryBlocks(chest, chest + Vector3.Transform(new Vector3(side * 3.4f, 0, 0), turn)),
                        $"{door.Name}: open past the door's {(side < 0 ? "left" : "right")} edge");

                Assert.IsFalse(cover.GeometryBlocks(chest + Vector3.Transform(new Vector3(0, 0, 3f), turn), chest), $"{door.Name}: shut in front");
                Assert.IsFalse(cover.GeometryBlocks(chest + Vector3.Transform(new Vector3(0, 0, -3f), turn), chest), $"{door.Name}: shut behind");
            }
        }

        [TestMethod]
        public void TheMigrationGoesInOnceAndDownPutsEverythingBack()
        {
            using var database = new Rasa.Test.Database.OrdinaryLootTestDatabase();
            var migrator = database.Context.GetService<IMigrator>();
            migrator.Migrate(Previous);
            var poolsBefore = Pools(database);

            migrator.Migrate(Migration);
            Assert.AreEqual(B.Missions.Length, Count(database, $"SELECT COUNT(*) AS Value FROM mission_content_definition WHERE content_revision = '{B.Revision}' AND enabled = 1"));
            var rows = Count(database, "SELECT COUNT(*) AS Value FROM creature WHERE id BETWEEN 784000 AND 784999");
            Assert.AreEqual(B.Creatures.Length, rows);
            Assert.AreEqual(B.Pools.Length, Count(database, "SELECT COUNT(*) AS Value FROM spawnpool WHERE id BETWEEN 784000 AND 784999"));

            foreach (var sql in B.Up().Where(sql => !sql.StartsWith("INSERT INTO mission") && !sql.StartsWith("UPDATE mission")))
                database.Context.Database.ExecuteSqlRaw(sql);
            Assert.AreEqual(rows, Count(database, "SELECT COUNT(*) AS Value FROM creature WHERE id BETWEEN 784000 AND 784999"), "guarded");

            migrator.Migrate(Previous);
            foreach (var table in new[] { "creature", "creature_action", "creature_stat", "creature_appearance", "spawnpool", "npc_package" })
                Assert.AreEqual(0, Count(database, $"SELECT COUNT(*) AS Value FROM {table} WHERE id BETWEEN 784000 AND 784999"), table);
            Assert.AreEqual(0, Count(database, $"SELECT COUNT(*) AS Value FROM mission_content_definition WHERE content_revision = '{B.Revision}'"));
            var poolsAfter = Pools(database);
            CollectionAssert.AreEquivalent(poolsBefore.Keys.ToList(), poolsAfter.Keys.ToList());
            foreach (var (id, before) in poolsBefore)
                Assert.AreEqual(before, poolsAfter[id], $"pool {id}");

            migrator.Migrate(Migration);
            Assert.AreEqual(B.Missions.Length, Count(database, $"SELECT COUNT(*) AS Value FROM mission_content_definition WHERE content_revision = '{B.Revision}' AND enabled = 1"));
        }

        /// <summary>Kaliel's: Lagda talked to, then following you to the operations room; Melokan; Killroy takes it.</summary>
        [TestMethod]
        public void LagdaFollowsYouToTheOperationsRoomAndKillroyGivesThePasskey()
        {
            using var harness = Create();
            var kaliel = Put(harness, B.Kaliel, B.KalielAt);
            Accept(harness, kaliel, B.WeNeedBackup);
            Assert.AreNotEqual(MissionObjectiveState.Incomplete, Objective(harness, B.WeNeedBackup, B.EscortLagda), "not yet");

            Work(harness);
            var lagda = BrannWaterRefinery.LagdaOf(harness.Map);
            Assert.IsNotNull(lagda);
            Assert.IsTrue(lagda.ImmuneToAllDamage, "she takes no harm");
            Talk(harness, lagda, B.WeNeedBackup, B.AssistLagda);
            Assert.AreEqual(MissionObjectiveState.Incomplete, Objective(harness, B.WeNeedBackup, B.EscortLagda));

            // Far from her, she waits.
            harness.MoveTo(B.MelokanAt);
            Work(harness);
            Assert.IsLessThan(2f, Vector3.Distance(lagda.Position, B.LagdaAt), "she waits at the way in");

            harness.MoveTo(lagda.Position + new Vector3(2, 0, 0));
            Work(harness);
            harness.MoveTo(B.KillroyAt + new Vector3(2, 0, 0));
            Work(harness);
            Work(harness);
            Work(harness);
            Assert.IsTrue(BrannWaterRefinery.LagdaDelivered(harness.Map), $"Lagda at {lagda.Position}");
            Assert.AreEqual(MissionObjectiveState.Completed, Objective(harness, B.WeNeedBackup, B.EscortLagda));

            var melokan = Put(harness, B.Melokan, B.MelokanAt);
            Talk(harness, melokan, B.WeNeedBackup, B.Melokans);

            Assert.IsFalse(BrannWaterRefinery.HasPasskey(harness.Client.Player));
            var killroy = Put(harness, B.Killroy, B.KillroyAt);
            Reward(harness, killroy, B.WeNeedBackup, null, null, B.BackupExperience, (int)B.Credits);
            Assert.IsTrue(BrannWaterRefinery.HasPasskey(harness.Client.Player), "Killroy's passkey");
        }

        /// <summary>Shut, the locked doors stop players and the Bane; a member with the passkey at one opens it for good.</summary>
        [TestMethod]
        public void ThePasskeyOpensTheLockedDoors()
        {
            using var harness = Create();
            Work(harness);
            var doors = BrannWaterRefinery.Doors(harness.Map);
            Assert.HasCount(B.Doors.Length, doors);

            for (var i = 0; i < doors.Count; i++)
            {
                var turn = Quaternion.CreateFromYawPitchRoll(B.Doors[i].Yaw, 0f, 0f);
                var front = B.Doors[i].At + Vector3.Transform(new Vector3(0, 0.5f, 2f), turn);
                var back = B.Doors[i].At + Vector3.Transform(new Vector3(0, 0.5f, -2f), turn);
                Assert.IsTrue(ForceFields.StopsPlayer(harness.Map, front, back), B.Doors[i].Name);
                Assert.IsTrue(ForceFields.Stops(harness.Map, Put(harness, B.Soldier, front), front, back), $"{B.Doors[i].Name}: a Thrax");

                // Without the passkey, it stays shut.
                harness.MoveTo(front);
                Work(harness);
                Assert.IsFalse(doors[i].IsDestroyed, $"{B.Doors[i].Name} without the passkey");
            }

            Completed(harness, B.WeNeedBackup);
            for (var i = 0; i < doors.Count; i++)
            {
                harness.MoveTo(B.Doors[i].At + Vector3.Transform(new Vector3(0, 0.5f, 3f), Quaternion.CreateFromYawPitchRoll(B.Doors[i].Yaw, 0f, 0f)));
                Work(harness);
                Assert.IsTrue(doors[i].IsDestroyed, $"{B.Doors[i].Name} with the passkey");
            }

            harness.MoveTo(B.Arrival);
            Work(harness);
            Assert.IsTrue(doors.All(door => door.IsDestroyed), "and they stay open");
        }

        /// <summary>Jenks': each module destroyed is its objective; Caolots dead the last; Arboc takes it.</summary>
        [TestMethod]
        public void TheHackingModulesAndCaolotsCrippleTheDefenses()
        {
            using var harness = Create();
            Completed(harness, B.WeNeedBackup);
            var jenks = Put(harness, B.Jenks, B.JenksAt);
            Accept(harness, jenks, B.CrippleTheDefenses);
            Work(harness);

            var modules = BrannWaterRefinery.Modules(harness.Map);
            Assert.HasCount(3, modules);
            Assert.IsTrue(modules.All(live => live?.Object?.NameOverrideId == B.ModuleName), "Bane Hacking Module");
            for (var i = 0; i < modules.Count; i++)
            {
                Assert.AreEqual(MissionObjectiveState.Incomplete, Objective(harness, B.CrippleTheDefenses, B.Modules[i].Objective), B.Modules[i].Room);
                Assert.IsTrue(PlacedDestructibles.Destroy(modules[i], harness.Client.Player));
                Work(harness);
                Work(harness);
                Assert.AreEqual(MissionObjectiveState.Completed, Objective(harness, B.CrippleTheDefenses, B.Modules[i].Objective), B.Modules[i].Room);
                Assert.AreEqual(long.MaxValue, modules[i].RespawnAt, "it stays down");
            }

            WildernessDaghdasUrnTests.Kill(harness, Put(harness, B.Caolots, B.CaolotsAt));
            Assert.AreEqual(MissionObjectiveState.Completed, Objective(harness, B.CrippleTheDefenses, B.KillCaolots));

            var arboc = Put(harness, B.Arboc, harness.Client.Player.Position + new Vector3(2, 0, 0));
            Reward(harness, arboc, B.CrippleTheDefenses, 0, B.BioArmorLegs, B.Experience, (int)B.Credits);
        }

        /// <summary>Arboc's: each bomb defused once, at any pace, its detonator; all ten to Coen.</summary>
        [TestMethod]
        public void EachBombDefusedGivesItsDetonatorAndCoenTakesThemAll()
        {
            using var harness = Create();
            Completed(harness, B.WeNeedBackup);
            Completed(harness, B.CrippleTheDefenses);
            var arboc = Put(harness, B.Arboc, B.ArbocAt);
            Accept(harness, arboc, B.DefusingTheSituation);
            Work(harness);

            var bombs = BrannWaterRefinery.Bombs(harness.Map);
            Assert.HasCount(10, bombs);
            for (var i = 0; i < bombs.Count; i++)
            {
                Assert.AreEqual(UseObjectState.BombStateArmed, bombs[i].StateId, B.Bombs[i].Where);
                Use(harness, bombs[i]);
                Assert.AreEqual(UseObjectState.BombStateDisarmed, bombs[i].StateId, B.Bombs[i].Where);
                Assert.AreEqual(i + 1, Held(harness, B.DetonatorTemplate), B.Bombs[i].Where);
                Assert.AreEqual(MissionObjectiveState.Completed, Objective(harness, B.DefusingTheSituation, B.Bombs[i].Objective), B.Bombs[i].Where);
                Assert.AreEqual(PlayerMessage.PmUseObjectNotUsable, BrannWaterRefinery.Refusal(harness.Client, bombs[i]), "once");

                // No timer: a long while between them changes nothing.
                harness.Tick(600_000);
            }

            Assert.AreEqual(MissionObjectiveState.Completed, Objective(harness, B.DefusingTheSituation, B.DefuseAll));
            var coen = Put(harness, B.Coen, harness.Client.Player.Position + new Vector3(2, 0, 0));
            Reward(harness, coen, B.DefusingTheSituation, 2, B.PulsePistol, B.Experience, (int)B.Credits);
            Assert.AreEqual(0, Held(harness, B.DetonatorTemplate), "Coen has the detonators");

            // Nobody on it: the bombs are nobody's to defuse.
            Assert.AreEqual(PlayerMessage.PmUseObjectNotUsable, BrannWaterRefinery.Refusal(harness.Client, bombs[0]));
        }

        /// <summary>
        /// Coen's: Namistai, the skybridge's Bane all dead at once, each living quarters' four pods;
        /// radioed in. Then Hostile Overtaking by radio: Ifilion and the Greenhouse's chairs, to Michan.
        /// </summary>
        [TestMethod]
        public void TheSkybridgeThePodsAndTheGreenhouse()
        {
            using var harness = Create();
            Completed(harness, B.WeNeedBackup);
            Completed(harness, B.CrippleTheDefenses);
            Completed(harness, B.DefusingTheSituation);
            var coen = Put(harness, B.Coen, B.CoenAt);
            Accept(harness, coen, B.FlanksForNothing);
            var namistai = Put(harness, B.Namistai, B.NamistaiAt);
            Talk(harness, namistai, B.FlanksForNothing, B.ReportToNamistai);
            Assert.AreEqual(MissionObjectiveState.Incomplete, Objective(harness, B.FlanksForNothing, B.Skybridge));
            Work(harness);

            // The skybridge: two of its pools dead is not it; all three is.
            harness.SpawnWorld(B.SkybridgePools);
            for (var i = 0; i < B.SkybridgePools.Length; i++)
            {
                foreach (var bane in PoolCreatures(harness, B.SkybridgePools[i]))
                    WildernessDaghdasUrnTests.Kill(harness, bane);
                Work(harness);
                Work(harness);
                Assert.AreEqual(i == B.SkybridgePools.Length - 1, BrannWaterRefinery.SkybridgeSecured(harness.Map), $"after pool {i}");
            }
            Assert.AreEqual(MissionObjectiveState.Completed, Objective(harness, B.FlanksForNothing, B.Skybridge));

            var pods = BrannWaterRefinery.Pods(harness.Map);
            Assert.HasCount(8, pods);
            foreach (var north in new[] { true, false })
            {
                var objective = north ? B.NorthernPods : B.SouthernPods;
                for (var i = 0; i < pods.Count; i++)
                    if (B.Pods[i].North == north)
                    {
                        Assert.AreEqual(MissionObjectiveState.Incomplete, Objective(harness, B.FlanksForNothing, objective));
                        Assert.IsTrue(PlacedDestructibles.Destroy(pods[i], harness.Client.Player));
                        Work(harness);
                        Work(harness);
                    }
                Assert.AreEqual(MissionObjectiveState.Completed, Objective(harness, B.FlanksForNothing, objective), north ? "north" : "south");
            }

            Assert.IsTrue(harness.Manager.TryCompleteRadioMission(harness.Client, B.FlanksForNothing, 0, null), "radioed in");
            harness.Drain();
            Assert.AreEqual(1, Held(harness, B.StealthArmorVest));

            Assert.IsTrue(harness.Manager.TryAcceptRadioMission(harness.Client, B.HostileOvertaking), "offered by radio");
            harness.Drain();
            Work(harness);

            WildernessDaghdasUrnTests.Kill(harness, Put(harness, B.Ifilion, B.IfilionAt));
            Assert.AreEqual(MissionObjectiveState.Completed, Objective(harness, B.HostileOvertaking, B.KillIfilion));

            var chairs = BrannWaterRefinery.Chairs(harness.Map);
            Assert.HasCount(4, chairs);
            foreach (var chair in chairs)
            {
                Assert.AreEqual(MissionObjectiveState.Incomplete, Objective(harness, B.HostileOvertaking, B.Machinery));
                Assert.IsTrue(PlacedDestructibles.Destroy(chair, harness.Client.Player));
                Work(harness);
                Work(harness);
            }
            Assert.AreEqual(MissionObjectiveState.Completed, Objective(harness, B.HostileOvertaking, B.Machinery));

            var michan = Put(harness, B.Michan, harness.Client.Player.Position + new Vector3(2, 0, 0));
            Reward(harness, michan, B.HostileOvertaking, null, null, B.OvertakingExperience, (int)B.Credits);
        }

        #region Helpers

        private static long Count(Rasa.Test.Database.OrdinaryLootTestDatabase database, string sql) =>
            database.Context.Database.SqlQueryRaw<long>(sql).ToArray().Single();

        private static Dictionary<uint, string> Pools(Rasa.Test.Database.OrdinaryLootTestDatabase database) =>
            database.Context.SpawnPoolEntries.AsNoTracking().Where(p => p.MapContextId == B.Refinery).ToList()
                .ToDictionary(p => p.Id, p => $"{p.PosX:F2} {p.PosY:F2} {p.PosZ:F2} {p.Rotation:F2} {p.Creature1Id} {p.Creature2Id}");

        private static WildernessRuntimeTestHarness Create()
        {
            var harness = WildernessRuntimeTestHarness.Create(mapContextId: B.Refinery);
            try
            {
                using var unit = harness.CreateChar();
                unit.ExecuteTransaction(() => unit.CharacterStartingExperience.Add(
                    new CharacterStartingExperienceEntry(harness.Client.Player.Id, B.Revision, CharacterStartingExperienceState.Skipped)));
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

        private static List<Creature> PoolCreatures(WildernessRuntimeTestHarness harness, uint poolId) =>
            harness.Map.MapCellInfo.Cells.Values.SelectMany(cell => cell.CreatureList).Distinct()
                .Where(creature => creature.SpawnPool?.DbId == poolId && CreatureManager.IsLivingOnMap(harness.Map, creature)).ToList();

        private static void Work(WildernessRuntimeTestHarness harness)
        {
            BrannWaterRefinery.Worker(harness.Map);
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

        private static void Completed(WildernessRuntimeTestHarness harness, uint missionId)
        {
            using var unit = harness.CreateChar();
            unit.ExecuteTransaction(() => unit.CharacterMissions.Runtime.Archive(
                new CharacterMissionEntry(harness.Client.Player.Id, missionId, (uint)MissionState.Completed)
                {
                    AssignmentId = Guid.NewGuid().ToString("N"),
                    Generation = 1,
                    ContentRevision = B.Revision
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
