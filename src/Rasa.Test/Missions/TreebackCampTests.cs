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
    using T = Rasa.Services.Preloader.TreebackCampContent;
    using static Wilderness.WildernessTwinPillarsTests;

    /// <summary>
    /// The Treeback Camp as the wiki and Ten Ton Hammer tell it (TreebackCampContent, TreebackCamp):
    /// its rows and thirteen missions, the forward base and Bagby, the gate and its controller, the
    /// dying commando, the howlers, the leaders, the conduits, Markinson and the harvester - and the
    /// wiki's known bugs put right.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class TreebackCampTests
    {
        private const string Migration = "20271126000000_Treeback_camp";
        private const string Previous = "20271125000000_Donn_egg_cluster_narrows";
        private const string MapName = "adv_foreas_concordia_palisades_treebackcamp";

        private long _now = 1_000_000;

        [TestInitialize]
        public void Clean()
        {
            TreebackCamp.Reset();
            TreebackCamp.Now = () => _now;
        }

        [TestCleanup]
        public void Restore()
        {
            TreebackCamp.Reset();
            TreebackCamp.Now = () => Environment.TickCount64;
        }

        [TestMethod]
        public void TheCampHasItsAfsItsBaneAndItsThirteenMissionsEachObjectiveMarked()
        {
            using var harness = Create();
            var world = harness.World;

            foreach (var missionId in T.Missions)
            {
                Assert.IsTrue(harness.Manager.TryGetOperationalMission(missionId, out var mission), $"mission {missionId}");
                Assert.AreEqual(T.Category, mission.CategoryId);
            }

            var packages = world.Set<NpcPackageEntry>().AsNoTracking().ToDictionary(row => row.Id, row => row.PackageId);
            Assert.AreEqual(T.BagbyPackage, packages[T.Bagby]);
            Assert.AreEqual(T.GallowayPackage, packages[T.Galloway]);
            Assert.AreEqual(T.LoudenPackage, packages[T.Louden]);

            var creatures = world.Set<CreatureEntry>().AsNoTracking().ToDictionary(c => c.Id);
            foreach (var c in T.Creatures.Where(c => c.Faction == T.TargetCategoryRow.Hostile))
                Assert.IsTrue(creatures[c.Id].Level is >= 22 and <= 26, $"{c.Comment} level {creatures[c.Id].Level}");

            // The wiki's first bug: no markers. Every objective has one now - but the count of Sabotage's conduits, which
            // has each of its five.
            var markers = world.Set<MissionIndicatorEntry>().AsNoTracking().Where(row => row.ContentRevision == T.Revision).ToList();
            var objectives = world.Set<MissionObjectiveDefinitionEntry>().AsNoTracking().Where(row => row.ContentRevision == T.Revision).ToList();
            Assert.IsNotEmpty(objectives);
            foreach (var objective in objectives.Where(o => !(o.MissionId == T.Sabotage && o.ObjectiveId == T.AllConduits)))
                Assert.IsTrue(markers.Any(m => m.MissionId == objective.MissionId && m.ObjectiveId == objective.ObjectiveId),
                    $"{objective.MissionId}.{objective.ObjectiveId} has a marker");

            // And the gate's switch among them, by the client's own name.
            Assert.IsTrue(markers.Any(m => m.MissionId == T.OpenTheGate && m.IndicatorId == T.ControllerMarker));
        }

        [TestMethod]
        public void EveryPoolAndObjectStandsOnTheGroundAndNoNpcInAnother()
        {
            using var database = new Rasa.Test.Database.OrdinaryLootTestDatabase();
            database.Context.GetService<IMigrator>().Migrate(Migration);
            var pools = database.Context.SpawnPoolEntries.AsNoTracking().ToList();
            var ours = T.Pools.Select(p => p.Id).ToHashSet();
            var meshes = new Dictionary<uint, NavMeshQuery>
            {
                [T.Camp] = TestNavMeshes.Query(NavMeshFile.PathFor(NavmeshDirectory(), MapName)),
                [T.Palisades] = TestNavMeshes.Query(NavMeshFile.PathFor(NavmeshDirectory(), "adv_foreas_concordia_palisades"))
            };

            foreach (var pool in pools.Where(pool => ours.Contains(pool.Id)))
            {
                var at = new Vector3((float)pool.PosX, (float)pool.PosY, (float)pool.PosZ);
                var ground = meshes[pool.MapContextId].Nearest(at);
                Assert.IsTrue(ground.HasValue, $"{pool.Id} off the ground");
                Assert.IsLessThan(0.6f, Vector2.Distance(new Vector2(at.X, at.Z), new Vector2(ground.Value.X, ground.Value.Z)), $"{pool.Id} over walkable ground");
                Assert.IsLessThan(1f, Math.Abs(ground.Value.Y - at.Y), $"{pool.Id}: the ground {ground.Value.Y - at.Y} m from it");
            }

            var camp = meshes[T.Camp];
            foreach (var spot in T.Conduits.Select(c => c.At).Concat(new[]
                     {
                         T.BagbyAt, T.BagbyAtForwardBase, T.ComputerAt, T.GateSwitchAt, T.KendrickAt, T.StarksAt, T.BrooksAt, T.CromakAt,
                         T.AmuaggorAt, T.HarvesterAt, T.ThraggorthAt, T.MarkinsonAt, T.TagakAt, T.GateWest, T.GateEast
                     }))
            {
                var ground = camp.Nearest(spot);
                Assert.IsTrue(ground.HasValue && Vector2.Distance(new Vector2(spot.X, spot.Z), new Vector2(ground.Value.X, ground.Value.Z)) < 1f &&
                    Math.Abs(ground.Value.Y - spot.Y) < 1f, $"{spot} off the ground");
            }

            var npcs = pools.Where(pool => pool.Creature1Id is >= 500000 and < 520000 || T.Afs.Contains(pool.Creature1Id) ||
                pool.Creature1Id is T.Almos or T.Longshanks or T.Olaera).ToList();
            foreach (var pool in npcs.Where(pool => ours.Contains(pool.Id)))
                foreach (var other in npcs.Where(other => other.Id != pool.Id && other.MapContextId == pool.MapContextId))
                    Assert.IsFalse(Math.Sqrt(Math.Pow(pool.PosX - other.PosX, 2) + Math.Pow(pool.PosZ - other.PosZ, 2)) < 1.5 &&
                        Math.Abs(pool.PosY - other.PosY) < 3, $"{pool.Id} in {other.Id}");
        }

        /// <summary>The gate's field closes the gap in the west wall, the way the walk east takes.</summary>
        [TestMethod]
        public void TheGateClosesTheWayEast()
        {
            var navMesh = TestNavMeshes.Query(NavMeshFile.PathFor(NavmeshDirectory(), MapName));
            var path = navMesh.FindPath(navMesh.Nearest(T.GallowayAt).Value, navMesh.Nearest(T.Kennels).Value, out var complete);
            Assert.IsTrue(complete);

            var gate = ForceFields.ClassOf("baneminor");
            var crossed = path.Zip(path.Skip(1)).Any(leg => ForceFields.Crosses(gate, T.GateAt, T.GateYaw,
                leg.First + new Vector3(0, 0.5f, 0), leg.Second + new Vector3(0, 0.5f, 0)));
            Assert.IsTrue(crossed, "the way to the kennels goes through the gate");
        }

        [TestMethod]
        public void TheMigrationGoesInOnceAndDownPutsEverythingBack()
        {
            using var database = new Rasa.Test.Database.OrdinaryLootTestDatabase();
            var migrator = database.Context.GetService<IMigrator>();
            migrator.Migrate(Previous);
            var poolsBefore = Count(database, "SELECT COUNT(*) AS Value FROM spawnpool");

            migrator.Migrate(Migration);
            Assert.AreEqual(T.Missions.Length, Count(database, $"SELECT COUNT(*) AS Value FROM mission_content_definition WHERE content_revision = '{T.Revision}' AND enabled = 1"));
            var rows = Count(database, "SELECT COUNT(*) AS Value FROM creature WHERE id BETWEEN 785000 AND 785999");
            Assert.AreEqual(T.Creatures.Length, rows);
            Assert.AreEqual(T.Pools.Length, Count(database, "SELECT COUNT(*) AS Value FROM spawnpool WHERE id BETWEEN 785000 AND 785999"));

            foreach (var sql in T.Up().Where(sql => !sql.StartsWith("INSERT INTO mission") && !sql.StartsWith("UPDATE mission")))
                database.Context.Database.ExecuteSqlRaw(Escape(sql));
            Assert.AreEqual(rows, Count(database, "SELECT COUNT(*) AS Value FROM creature WHERE id BETWEEN 785000 AND 785999"), "guarded");

            migrator.Migrate(Previous);
            foreach (var table in new[] { "creature", "creature_action", "creature_stat", "creature_appearance", "spawnpool", "npc_package" })
                Assert.AreEqual(0, Count(database, $"SELECT COUNT(*) AS Value FROM {table} WHERE id BETWEEN 785000 AND 785999"), table);
            Assert.AreEqual(0, Count(database, $"SELECT COUNT(*) AS Value FROM mission_content_definition WHERE content_revision = '{T.Revision}'"));
            Assert.AreEqual(poolsBefore, Count(database, "SELECT COUNT(*) AS Value FROM spawnpool"));

            migrator.Migrate(Migration);
            Assert.AreEqual(T.Missions.Length, Count(database, $"SELECT COUNT(*) AS Value FROM mission_content_definition WHERE content_revision = '{T.Revision}' AND enabled = 1"));
        }

        /// <summary>Almos's, then Bagby's: the computer, answering once the Thrax at it are dead; Bagby comes up; then Louden.</summary>
        [TestMethod]
        public void TheBeachheadTheForwardBaseAndBagbyComingUp()
        {
            using var harness = Create();
            var almos = Put(harness, T.Almos, harness.Client.Player.Position + new Vector3(2, 0, 0));
            Accept(harness, almos, T.TreebackBeachhead);

            Work(harness);
            var bagby = TreebackCamp.BagbyOf(harness.Map);
            Assert.IsNotNull(bagby);
            Assert.IsTrue(bagby.ImmuneToAllDamage);
            Assert.IsLessThan(1f, Vector2.Distance(new Vector2(bagby.Position.X, bagby.Position.Z), new Vector2(T.BagbyAt.X, T.BagbyAt.Z)), "at the beachhead");
            Talk(harness, bagby, T.TreebackBeachhead, T.ReportToBagby);
            Reward(harness, bagby, T.TreebackBeachhead, 0, T.EmpBomb, T.ReportExperience, 1700);

            Accept(harness, bagby, T.RetakeTheForwardBase);
            var computer = TreebackCamp.Computer(harness.Map);
            Assert.AreEqual(UseObjectState.TsState0, computer.StateId);

            // A Thrax by it: it does not answer.
            var thrax = Put(harness, T.Guard, T.ComputerAt + new Vector3(6, 0, 4));
            Assert.AreEqual(PlayerMessage.PmUseObjectNotUsable, TreebackCamp.Refusal(harness.Client, computer));
            WildernessDaghdasUrnTests.Kill(harness, thrax);
            Assert.IsNull(TreebackCamp.Refusal(harness.Client, computer));

            Use(harness, computer);
            Assert.AreEqual(UseObjectState.TsState1, computer.StateId);
            Assert.AreEqual(MissionObjectiveState.Completed, Objective(harness, T.RetakeTheForwardBase, T.TurnOnTheComputer));
            Assert.AreEqual(MissionObjectiveState.Incomplete, Objective(harness, T.RetakeTheForwardBase, T.WaitForBagby));

            // Wait for him.
            harness.MoveTo(T.ForwardBase);
            Work(harness);
            Assert.IsFalse(TreebackCamp.BagbyAtForwardBase(harness.Map), "not yet");
            _now += TreebackCamp.BagbyDelayMs;
            Work(harness);
            Work(harness);
            Assert.IsTrue(TreebackCamp.BagbyAtForwardBase(harness.Map));
            Assert.IsLessThan(2f, Vector3.Distance(bagby.Position, T.BagbyAtForwardBase), $"Bagby at {bagby.Position}");
            Assert.AreEqual(MissionObjectiveState.Completed, Objective(harness, T.RetakeTheForwardBase, T.WaitForBagby));
            Reward(harness, bagby, T.RetakeTheForwardBase, 1, T.ConcussionGrenade, T.Experience, 1700);

            Accept(harness, bagby, T.TreebackResearch);
            var louden = Put(harness, T.Louden, T.LoudenAt);
            Talk(harness, louden, T.TreebackResearch, T.SpeakWithLouden);
            Reward(harness, louden, T.TreebackResearch, 3, T.TreebackBoots[3], T.ReportExperience, 1700);
        }

        /// <summary>Shut, the gate stops players; the controller, thrown, opens it for good, and Kendrick lands.</summary>
        [TestMethod]
        public void TheControllerOpensTheGateAndKendrickLands()
        {
            using var harness = Create();
            Completed(harness, T.TreebackBeachhead, T.RetakeTheForwardBase, T.TreebackResearch);
            Work(harness);

            var gate = TreebackCamp.Gate(harness.Map);
            Assert.IsNotNull(gate);
            Assert.IsTrue(ForceFields.StopsPlayer(harness.Map, T.GateWest + new Vector3(0, 0.5f, 0), T.GateEast + new Vector3(0, 0.5f, 0)), "shut");
            Assert.IsTrue(gate.Sealed, "shots do nothing");
            Assert.IsNull(TreebackCamp.Kendrick(harness.Map));

            var louden = Put(harness, T.Louden, T.LoudenAt);
            Accept(harness, louden, T.OpenTheGate);
            var controller = TreebackCamp.GateSwitch(harness.Map);
            Assert.AreEqual(T.GateSwitchName, controller.NameOverrideId);
            Use(harness, controller);

            Assert.IsTrue(gate.IsDestroyed, "open");
            Assert.IsFalse(ForceFields.StopsPlayer(harness.Map, T.GateWest + new Vector3(0, 0.5f, 0), T.GateEast + new Vector3(0, 0.5f, 0)));
            Assert.AreEqual(MissionObjectiveState.Completed, Objective(harness, T.OpenTheGate, T.ShutOffTheField));
            Assert.AreEqual(PlayerMessage.PmUseObjectNotUsable, TreebackCamp.Refusal(harness.Client, controller), "thrown once");

            Work(harness);
            Assert.IsNotNull(TreebackCamp.Kendrick(harness.Map), "Kendrick lands inside it");
            Reward(harness, louden, T.OpenTheGate, 0, T.TreebackGloves[0], T.Experience, 1800);
        }

        /// <summary>In another copy, a member who has opened the gate opens it again by coming to it.</summary>
        [TestMethod]
        public void OneWhoHasOpenedTheGateOpensItInANewCopy()
        {
            using var harness = Create();
            Work(harness);
            harness.MoveTo(T.GateWest - new Vector3(5, 0, 0));
            Work(harness);
            Assert.IsFalse(TreebackCamp.GateOpen(harness.Map), "not for a stranger");

            Completed(harness, T.OpenTheGate);
            Work(harness);
            Assert.IsTrue(TreebackCamp.GateOpen(harness.Map));
            Assert.IsTrue(TreebackCamp.Gate(harness.Map).IsDestroyed);
        }

        /// <summary>Louden's ridge: the signal, the dying commando; his howlers, of every kind.</summary>
        [TestMethod]
        public void TheDyingCommandoAndHisHowlersOfEveryKind()
        {
            using var harness = Create();
            Completed(harness, T.TreebackBeachhead, T.RetakeTheForwardBase, T.TreebackResearch, T.OpenTheGate);
            var louden = Put(harness, T.Louden, T.LoudenAt);
            Accept(harness, louden, T.CheckTheRidge);

            harness.MoveTo(T.KeenAt + new Vector3(20, 0, 0));
            Work(harness);
            Work(harness);
            Assert.AreEqual(MissionObjectiveState.Completed, Objective(harness, T.CheckTheRidge, T.FindTheSignal));
            Assert.AreEqual(MissionObjectiveState.Incomplete, Objective(harness, T.CheckTheRidge, T.TalkToTheCommando));

            var keen = Put(harness, T.Keen, T.KeenAt);
            Accept(harness, keen, T.HowlingRevenge);
            Work(harness);
            Assert.AreEqual(MissionObjectiveState.Completed, Objective(harness, T.CheckTheRidge, T.TalkToTheCommando));

            // The wiki's bug: one kind counted. The kennels' alphas, the Palisades' howlers and a howler boss all count.
            var kinds = new[] { T.AlphaHowler, 540008u, 520040u };
            for (var i = 0; i < T.HowlersWanted; i++)
            {
                Assert.AreEqual(MissionObjectiveState.Incomplete, Objective(harness, T.HowlingRevenge, T.KillAlphaHowlers), $"after {i}");
                WildernessDaghdasUrnTests.Kill(harness, Put(harness, kinds[i % kinds.Length], T.Kennels + new Vector3(i, 0, 0)));
                harness.Drain();
            }
            Assert.AreEqual(MissionObjectiveState.Completed, Objective(harness, T.HowlingRevenge, T.KillAlphaHowlers));

            Reward(harness, Put(harness, T.Keen, harness.Client.Player.Position + new Vector3(2, 0, 0)), T.HowlingRevenge, null, null, T.Experience, 1900);
            Reward(harness, Put(harness, T.Louden, harness.Client.Player.Position + new Vector3(-2, 0, 0)), T.CheckTheRidge, 2, T.TreebackHelmets[2], T.Experience, 1800);
        }

        /// <summary>
        /// Galloway's three, in any order, one of them killed before it was taken up; Thraggorth lured out by the panel;
        /// Tagak comes once they are dead; Amuaggor's badge for Olaera. The wiki's bugs all.
        /// </summary>
        [TestMethod]
        public void TheLeadersCountWhenKilledBeforeAndInAnyOrder()
        {
            using var harness = Create();
            Completed(harness, T.TreebackBeachhead, T.RetakeTheForwardBase, T.TreebackResearch, T.OpenTheGate);
            Work(harness);

            var cromak = TreebackCamp.Leader(harness.Map, T.Cromak);
            var amuaggor = TreebackCamp.Leader(harness.Map, T.Amuaggor);
            Assert.IsNotNull(cromak);
            Assert.IsNotNull(amuaggor);
            Assert.IsNull(TreebackCamp.Leader(harness.Map, T.Thraggorth), "he keeps out of sight");
            Assert.IsNull(TreebackCamp.Leader(harness.Map, T.Tagak));

            // Cromak first, before Cut Off The Head is taken.
            WildernessDaghdasUrnTests.Kill(harness, cromak);
            Work(harness);
            Assert.IsTrue(TreebackCamp.IsDead(harness.Map, T.Cromak));

            var galloway = Put(harness, T.Galloway, T.GallowayAt);
            Accept(harness, galloway, T.CutOffTheHead);
            foreach (var objective in new[] { T.KillResearcher, T.KillHunterLord, T.KillSecurityChief })
                Assert.AreNotEqual(MissionObjectiveState.Inactive, Objective(harness, T.CutOffTheHead, objective), "all three open at once");
            Work(harness);
            Assert.AreEqual(MissionObjectiveState.Completed, Objective(harness, T.CutOffTheHead, T.KillHunterLord), "Cromak counts");

            // Then Thraggorth, lured out.
            var panel = TreebackCamp.Panel(harness.Map);
            Use(harness, panel);
            var thraggorth = TreebackCamp.Leader(harness.Map, T.Thraggorth);
            Assert.IsNotNull(thraggorth, "the panel brings him out");
            Assert.AreEqual(PlayerMessage.PmUseObjectNotUsable, TreebackCamp.Refusal(harness.Client, panel), "while he is out");
            WildernessDaghdasUrnTests.Kill(harness, thraggorth);
            Work(harness);
            Assert.AreEqual(MissionObjectiveState.Completed, Objective(harness, T.CutOffTheHead, T.KillSecurityChief));
            Assert.AreEqual(MissionObjectiveState.Incomplete, Objective(harness, T.CutOffTheHead, T.KillResearcher));
            Assert.IsNull(TreebackCamp.Leader(harness.Map, T.Tagak), "two of three");

            // Olaera's badge, taken up before Amuaggor dies.
            var olaera = Put(harness, T.Olaera, harness.Client.Player.Position + new Vector3(2, 0, 0));
            Accept(harness, olaera, T.MedicalAlertBadge);

            WildernessDaghdasUrnTests.Kill(harness, amuaggor);
            Work(harness);
            Work(harness);
            Assert.AreEqual(MissionObjectiveState.Completed, Objective(harness, T.CutOffTheHead, T.KillResearcher));
            Assert.AreEqual(1L, Held(harness, T.BadgeTemplate), "the badge off him");
            Assert.AreEqual(MissionObjectiveState.Completed, Objective(harness, T.MedicalAlertBadge, T.GetTheBadge));
            Work(harness);
            Assert.AreEqual(1L, Held(harness, T.BadgeTemplate), "once");
            Reward(harness, Put(harness, T.Olaera, harness.Client.Player.Position + new Vector3(2, 0, 0)), T.MedicalAlertBadge, null, null, T.Experience, 1900);
            Assert.AreEqual(0L, Held(harness, T.BadgeTemplate), "Olaera has it");

            Assert.IsTrue(harness.Manager.TryCompleteRadioMission(harness.Client, T.CutOffTheHead, 1, null), "radioed in");
            harness.Drain();
            Assert.AreEqual(1L, Held(harness, T.TreebackLeggings[1]));

            // Finishing Move: Tagak at his post.
            Assert.IsTrue(harness.Manager.TryAcceptRadioMission(harness.Client, T.FinishingMove), "offered by radio");
            harness.Drain();
            Work(harness);
            var tagak = TreebackCamp.Leader(harness.Map, T.Tagak);
            Assert.IsNotNull(tagak);
            Assert.IsLessThan(2f, Vector3.Distance(tagak.Position, T.TagakAt));
            WildernessDaghdasUrnTests.Kill(harness, tagak);
            Work(harness);
            Assert.AreEqual(MissionObjectiveState.Completed, Objective(harness, T.FinishingMove, T.KillTheCommander));
            Assert.IsTrue(harness.Manager.TryCompleteRadioMission(harness.Client, T.FinishingMove, null, null), "radioed in");
        }

        /// <summary>
        /// Kendrick's conduits, one after another, each marked as it comes, the count of all five; then Markinson lands in
        /// the secured generator station, and the NX-22 goes into the harvester; Bagby takes it.
        /// </summary>
        [TestMethod]
        public void TheConduitsThenMarkinsonAndTheHarvester()
        {
            using var harness = Create();
            Completed(harness, T.TreebackBeachhead, T.RetakeTheForwardBase, T.TreebackResearch, T.OpenTheGate);
            Work(harness);
            var kendrick = Put(harness, T.Kendrick, T.KendrickAt);
            Accept(harness, kendrick, T.Sabotage);
            Work(harness);

            var conduits = TreebackCamp.Conduits(harness.Map);
            Assert.HasCount(5, conduits);
            Assert.AreEqual(MissionObjectiveState.Incomplete, Objective(harness, T.Sabotage, T.Conduits[0].Objective));
            Assert.AreNotEqual(MissionObjectiveState.Incomplete, Objective(harness, T.Sabotage, T.Conduits[1].Objective), "one at a time");

            // The third first: it counts, and is its objective when that one's turn comes.
            Assert.IsTrue(PlacedDestructibles.Destroy(conduits[2], harness.Client.Player));
            Work(harness);
            Work(harness);
            Assert.AreEqual(MissionObjectiveState.Incomplete, Objective(harness, T.Sabotage, T.AllConduits));
            foreach (var i in new[] { 0, 1, 3, 4 })
            {
                Assert.IsTrue(PlacedDestructibles.Destroy(conduits[i], harness.Client.Player));
                Work(harness);
                Work(harness);
                Work(harness);
                Assert.AreEqual(long.MaxValue, conduits[i].RespawnAt, "it stays down");
            }
            foreach (var conduit in T.Conduits)
                Assert.AreEqual(MissionObjectiveState.Completed, Objective(harness, T.Sabotage, conduit.Objective), $"{conduit.Objective}");
            Assert.AreEqual(MissionObjectiveState.Completed, Objective(harness, T.Sabotage, T.AllConduits));

            Assert.IsTrue(harness.Manager.TryCompleteRadioMission(harness.Client, T.Sabotage, 3, null), "radioed in");
            harness.Drain();
            Assert.AreEqual(1L, Held(harness, T.TreebackJackets[3]));

            // Markinson: not while a Bane holds where he lands.
            Assert.IsNull(TreebackCamp.Markinson(harness.Map));
            var holder = Put(harness, T.Machina, T.MarkinsonAt + new Vector3(5, 0, 0));
            harness.MoveTo(T.GeneratorStation);
            Work(harness);
            Assert.IsNull(TreebackCamp.Markinson(harness.Map), "not secure");
            WildernessDaghdasUrnTests.Kill(harness, holder);
            harness.MoveTo(T.GeneratorStation);
            Work(harness);
            var markinson = TreebackCamp.Markinson(harness.Map);
            Assert.IsNotNull(markinson, "he lands");

            Accept(harness, markinson, T.TreebackGenocide);
            Assert.AreEqual(1L, Held(harness, T.NerveGasTemplate), "the NX-22");
            var harvester = TreebackCamp.Harvester(harness.Map);
            Use(harness, harvester);
            Assert.IsTrue(TreebackCamp.AgentDeployed(harness.Map));
            Assert.AreEqual(UseObjectState.IaStateAccepted, harvester.StateId);
            Assert.AreEqual(MissionObjectiveState.Completed, Objective(harness, T.TreebackGenocide, T.DeployTheAgent));

            Work(harness);
            var bagby = TreebackCamp.BagbyOf(harness.Map);
            Reward(harness, bagby, T.TreebackGenocide, null, null, T.FinalExperience, 2400);
            Assert.AreEqual(0L, Held(harness, T.NerveGasTemplate));
        }

        /// <summary>The two from outside with something to carry in: the gin to Bagby, the report to Galloway.</summary>
        [TestMethod]
        public void TheGinToBagbyAndTheReportToGalloway()
        {
            using var harness = Create();
            Work(harness);

            Accept(harness, Put(harness, T.Tayros, harness.Client.Player.Position + new Vector3(2, 0, 0)), T.BottleOfTheGoodStuff);
            Assert.AreEqual(1L, Held(harness, T.LiquorTemplate));
            var bagby = TreebackCamp.BagbyOf(harness.Map);
            Talk(harness, bagby, T.BottleOfTheGoodStuff, T.FindBagby);
            Reward(harness, bagby, T.BottleOfTheGoodStuff, 1, T.TreebackLeggings[0], T.ReportExperience, 1800);
            Assert.AreEqual(0L, Held(harness, T.LiquorTemplate), "Bagby has it");

            Accept(harness, Put(harness, T.Longshanks, harness.Client.Player.Position + new Vector3(2, 0, 0)), T.TreebackFieldReport);
            Assert.AreEqual(1L, Held(harness, T.ReportTemplate));
            var galloway = Put(harness, T.Galloway, T.GallowayAt);
            Talk(harness, galloway, T.TreebackFieldReport, T.DeliverTheReport);
            Reward(harness, galloway, T.TreebackFieldReport, 2, T.TreebackJackets[2], T.ReportExperience, 1800);
            Assert.AreEqual(0L, Held(harness, T.ReportTemplate), "Galloway has it");
        }

        #region Helpers

        private static long Count(Rasa.Test.Database.OrdinaryLootTestDatabase database, string sql) =>
            database.Context.Database.SqlQueryRaw<long>(sql).ToArray().Single();

        /// <summary>ExecuteSqlRaw takes braces for parameters: the scene bindings' JSON has them.</summary>
        private static string Escape(string sql) => sql.Replace("{", "{{").Replace("}", "}}");

        private static WildernessRuntimeTestHarness Create()
        {
            var harness = WildernessRuntimeTestHarness.Create(mapContextId: T.Camp);
            try
            {
                using var unit = harness.CreateChar();
                unit.ExecuteTransaction(() => unit.CharacterStartingExperience.Add(
                    new CharacterStartingExperienceEntry(harness.Client.Player.Id, T.Revision, CharacterStartingExperienceState.Skipped)));
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
            Assert.IsNotNull(creature, $"creature {dbId}");
            CreatureManager.Instance.SetLocation(creature, NavMeshManager.SnapToGround(harness.Map, position), 0, harness.Map.MapInfo.MapContextId);
            CellManager.Instance.AddToWorld(harness.Map, creature);
            return creature;
        }

        private static void Work(WildernessRuntimeTestHarness harness)
        {
            TreebackCamp.Worker(harness.Map);
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

        private static void Completed(WildernessRuntimeTestHarness harness, params uint[] missionIds)
        {
            foreach (var missionId in missionIds)
            {
                using var unit = harness.CreateChar();
                unit.ExecuteTransaction(() => unit.CharacterMissions.Runtime.Archive(
                    new CharacterMissionEntry(harness.Client.Player.Id, missionId, (uint)MissionState.Completed)
                    {
                        AssignmentId = Guid.NewGuid().ToString("N"),
                        Generation = 1,
                        ContentRevision = T.Revision
                    }, harness.UtcNow));
                harness.Client.Player.MissionHistory[missionId] = MissionState.Completed;
                harness.Client.Player.MissionSuccessHistory.Add(missionId);
            }
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
