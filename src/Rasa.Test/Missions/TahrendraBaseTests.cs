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
    using T = Rasa.Services.Preloader.TahrendraBaseContent;
    using static Wilderness.WildernessTwinPillarsTests;

    /// <summary>
    /// Tahrendra Base as the walkthrough tells it (TahrendraBaseContent, TahrendraBase): its rows
    /// and three missions, the Guard Lounge's crates and the barracks' doors, Eitak and the
    /// terminal, the teleporter, Donnaway and her charge, Xixl and the transporter, the Warden
    /// Bots, Upper Block Security, and the wormhole room.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class TahrendraBaseTests
    {
        private const string Migration = "20271127000000_Tahrendra_base";
        private const string Previous = "20271126000000_Treeback_camp";
        private const string MapName = "adv_arieki_torden_mires_tahrendrabase";

        private long _now = 1_000_000;

        [TestInitialize]
        public void Clock()
        {
            TahrendraBase.Reset();
            TahrendraBase.Now = () => _now;
            TahrendraBase.Roll = (min, max) => max;
            PlacedDestructibles.Now = () => _now;
            PlacedUsables.Now = () => _now;
        }

        [TestCleanup]
        public void Restore()
        {
            TahrendraBase.Reset();
            TahrendraBase.Now = () => Environment.TickCount64;
            TahrendraBase.Roll = (min, max) => Random.Shared.NextInt64(min, max + 1);
            PlacedDestructibles.Now = () => Environment.TickCount64;
            PlacedUsables.Now = () => Environment.TickCount64;
            PlacedUsables.Reset();
        }

        [TestMethod]
        public void TheBaseHasItsPeopleItsBaneAndItsThreeMissions()
        {
            using var harness = Create();
            var world = harness.World;

            foreach (var missionId in T.Missions)
            {
                Assert.IsTrue(harness.Manager.TryGetOperationalMission(missionId, out var mission), $"mission {missionId}");
                Assert.AreEqual(T.Category, mission.CategoryId);
            }

            var definitions = world.MissionContentDefinitionEntries.AsNoTracking().Where(row => row.ContentRevision == T.Revision).ToList();
            Assert.AreEqual(T.Missions.Length, definitions.Count(row => row.Enabled));
            Assert.IsTrue(definitions.Single(row => row.MissionId == T.BringingTheHouseDown).RadioCompleteable, "HQ radios its end");

            var packages = world.Set<NpcPackageEntry>().AsNoTracking().ToDictionary(row => row.Id, row => row.PackageId);
            Assert.AreEqual(T.TilleryPackage, packages[T.Tillery]);
            Assert.AreEqual(T.DonnawayPackage, packages[T.Donnaway]);
            Assert.AreEqual(T.HologramPackage, packages[T.Hologram]);

            var creatures = world.Set<CreatureEntry>().AsNoTracking().ToDictionary(c => c.Id);
            Assert.AreEqual(9490U, creatures[T.Tillery].NameId);
            Assert.AreEqual(9497U, creatures[T.Donnaway].NameId);
            Assert.AreEqual(9529U, creatures[T.WardenBot].NameId);
            foreach (var c in T.Creatures.Where(c => c.Faction == T.TargetCategoryRow.Hostile))
                Assert.IsTrue(creatures[c.Id].Level is >= 28 and <= 31, $"{c.Comment} level {creatures[c.Id].Level}");
            Assert.IsTrue(world.Set<CreatureAppearanceEntry>().Any(a => a.Id == T.Tillery && a.SlotId == 15));
            Assert.IsTrue(world.Set<CreatureAppearanceEntry>().Any(a => a.Id == T.Donnaway && a.SlotId == 15));

            // The base's pools, and the seed's with the base's Bane in them instead of the level 36 Mires'.
            var pools = world.Set<SpawnPoolEntry>().AsNoTracking().Where(pool => pool.MapContextId == T.Tahrendra).ToList();
            Assert.AreEqual(T.Pools.Length, pools.Count(pool => pool.Id is >= 786000 and <= 786999));
            Assert.AreEqual(T.Eitak, pools.Single(pool => pool.Id == T.EitakPool).Creature1Id);
            Assert.AreEqual(T.Xixl, pools.Single(pool => pool.Id == T.XixlPool).Creature1Id);
            var seeds = T.Recast.Select(r => r.Seed).ToHashSet();
            foreach (var pool in pools.Where(pool => T.SeedPools.Contains(pool.Id)))
                foreach (var creature in new[] { pool.Creature1Id, pool.Creature2Id, pool.Creature3Id, pool.Creature4Id, pool.Creature5Id, pool.Creature6Id })
                    Assert.IsFalse(seeds.Contains(creature), $"pool {pool.Id} still has {creature}");

            var rewards = world.Set<MissionRewardItemEntry>().AsNoTracking().Where(item => item.ContentRevision == T.Revision).ToList();
            CollectionAssert.AreEquivalent(new[] { T.SalvageTool, T.CipherTool, T.TissueExtractor, T.HealingDisc },
                rewards.Where(item => item.MissionId == T.BringingTheHouseDown).Select(item => item.ItemTemplateId).ToArray());
            CollectionAssert.AreEquivalent(new[] { T.GravitonVest, T.HazmatLegs, T.ReflectiveBoots, T.MotorAssistHelmet },
                rewards.Where(item => item.MissionId == T.WhatGoesAround).Select(item => item.ItemTemplateId).ToArray());
            Assert.IsFalse(rewards.Any(item => item.MissionId == T.BreakOnThrough), "the recipes are left out");

            var markers = world.Set<MissionIndicatorEntry>().AsNoTracking().Where(row => row.ContentRevision == T.Revision).ToList();
            Assert.IsTrue(markers.All(row => row.IndicatorId > MissionIndicator.UnnamedFrom));
        }

        [TestMethod]
        public void EveryPoolAndSpotStandsOnTheGround()
        {
            using var database = new Rasa.Test.Database.OrdinaryLootTestDatabase();
            database.Context.GetService<IMigrator>().Migrate(Migration);
            var pools = database.Context.SpawnPoolEntries.AsNoTracking().ToList();
            var ours = T.Pools.Select(p => p.Id).ToHashSet();
            var mesh = TestNavMeshes.Query(NavMeshFile.PathFor(NavmeshDirectory(), MapName));

            foreach (var pool in pools.Where(pool => ours.Contains(pool.Id)))
                OnTheGround(mesh, new Vector3((float)pool.PosX, (float)pool.PosY, (float)pool.PosZ), $"pool {pool.Id}");

            var spots = T.Crates.Select(c => c.At).Concat(T.GuardBarracks.Select(b => b.BombAt)).Concat(T.WardenBots).Concat(T.Balconies)
                .Concat(new[]
                {
                    T.Arrival, T.TilleryAtMedic, T.TilleryAtCheckPoint, T.Terminal, T.AfsTeleporter, T.DonnawayBeamsIn, T.BioSite,
                    T.Transporter, T.RendezvousAt, T.WormholeMark, T.DonnawayAtWork, T.HologramAt, T.MainDoor
                });
            foreach (var spot in spots)
                OnTheGround(mesh, spot, $"{spot}");
        }

        /// <summary>Each barracks' door stands in its jamb, where the jamb is narrower than the door: nothing gets round it.</summary>
        [TestMethod]
        public void EachBarracksDoorFillsItsJamb()
        {
            var cover = CoverMesh.Read(CoverMesh.PathFor(NavmeshDirectory(), MapName));

            foreach (var barracks in T.GuardBarracks)
            {
                var turn = Quaternion.CreateFromYawPitchRoll(barracks.Yaw, 0f, 0f);
                var chest = barracks.Door + new Vector3(0, 1.5f, 0);
                foreach (var side in new[] { -1f, 1f })
                    Assert.IsTrue(cover.GeometryBlocks(chest, chest + Vector3.Transform(new Vector3(side * 3.4f, 0, 0), turn)),
                        $"{barracks.Name}: open past the door's {(side < 0 ? "left" : "right")} edge");

                Assert.IsFalse(cover.GeometryBlocks(chest + Vector3.Transform(new Vector3(0, 0, 3f), turn), chest), $"{barracks.Name}: shut in front");
                Assert.IsFalse(cover.GeometryBlocks(chest + Vector3.Transform(new Vector3(0, 0, -3f), turn), chest), $"{barracks.Name}: shut behind");
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
            Assert.AreEqual(T.Missions.Length, Count(database, $"SELECT COUNT(*) AS Value FROM mission_content_definition WHERE content_revision = '{T.Revision}' AND enabled = 1"));
            var rows = Count(database, "SELECT COUNT(*) AS Value FROM creature WHERE id BETWEEN 786000 AND 786999");
            Assert.AreEqual(T.Creatures.Length, rows);
            Assert.AreEqual(T.Pools.Length, Count(database, "SELECT COUNT(*) AS Value FROM spawnpool WHERE id BETWEEN 786000 AND 786999"));
            Assert.AreEqual(T.Actions.Length, Count(database, "SELECT COUNT(*) AS Value FROM creature_action WHERE id BETWEEN 786000 AND 786999"));
            var seedPools = string.Join(", ", T.SeedPools);
            Assert.AreEqual(0, Count(database, $"SELECT COUNT(*) AS Value FROM spawnpool WHERE id IN ({seedPools}) AND creature_1_Id IN " +
                $"({string.Join(", ", T.Recast.Select(r => r.Seed))})"), "the seed's Bane recast");

            foreach (var sql in T.Up().Where(sql => !sql.StartsWith("INSERT INTO mission") && !sql.StartsWith("UPDATE mission")))
                database.Context.Database.ExecuteSqlRaw(sql);
            Assert.AreEqual(rows, Count(database, "SELECT COUNT(*) AS Value FROM creature WHERE id BETWEEN 786000 AND 786999"), "guarded");

            migrator.Migrate(Previous);
            foreach (var table in new[] { "creature", "creature_action", "creature_stat", "creature_appearance", "spawnpool", "npc_package" })
                Assert.AreEqual(0, Count(database, $"SELECT COUNT(*) AS Value FROM {table} WHERE id BETWEEN 786000 AND 786999"), table);
            Assert.AreEqual(0, Count(database, $"SELECT COUNT(*) AS Value FROM mission_content_definition WHERE content_revision = '{T.Revision}'"));
            var poolsAfter = Pools(database);
            CollectionAssert.AreEquivalent(poolsBefore.Keys.ToList(), poolsAfter.Keys.ToList());
            foreach (var (id, before) in poolsBefore)
                Assert.AreEqual(before, poolsAfter[id], $"pool {id}");

            migrator.Migrate(Migration);
            Assert.AreEqual(T.Missions.Length, Count(database, $"SELECT COUNT(*) AS Value FROM mission_content_definition WHERE content_revision = '{T.Revision}' AND enabled = 1"));
        }

        /// <summary>Tillery's: a detonator from each of the Guard Lounge's crates, each blown at a barracks' door; he moves up.</summary>
        [TestMethod]
        public void TheCratesDetonatorsSealTheBarracksAndTilleryMovesUp()
        {
            using var harness = Create();
            Work(harness);
            var tillery = TahrendraBase.TilleryOf(harness.Map);
            Assert.IsNotNull(tillery);
            Assert.IsTrue(tillery.ImmuneToAllDamage, "he takes no harm");
            Assert.IsLessThan(2f, Vector3.Distance(tillery.Position, T.TilleryAtMedic), $"beside the medic, at {tillery.Position}");
            Accept(harness, tillery, T.BreakOnThrough);

            var crates = harness.Map.DynamicObjects.Where(obj => (uint)obj.EntityClassId == T.CrateClass).ToList();
            Assert.HasCount(T.Crates.Length, crates);

            Use(harness, crates[0]);
            Assert.AreEqual(1U, Count(harness, T.DetonatorClass));
            Assert.IsTrue(TahrendraBase.CrateEmpty(harness.Map, 0));
            Assert.AreEqual(PlayerMessage.PmUseObjectNotUsable, TahrendraBase.Refusal(harness.Client, crates[0]));
            Assert.AreEqual(MissionObjectiveState.Incomplete, Objective(harness, T.BreakOnThrough, T.FindExplosives));

            _now += TahrendraBase.CrateEmptyMaxMs;
            Work(harness);
            Assert.IsFalse(TahrendraBase.CrateEmpty(harness.Map, 0), "a detonator in it again a while later");

            Use(harness, crates[1]);
            Assert.AreEqual(T.DetonatorsWanted, Count(harness, T.DetonatorClass));
            Assert.AreEqual(MissionObjectiveState.Completed, Objective(harness, T.BreakOnThrough, T.FindExplosives));
            Assert.AreEqual(MissionObjectiveState.Incomplete, Objective(harness, T.BreakOnThrough, T.SealBarracks));

            // Two are enough: no more.
            Use(harness, crates[0]);
            Assert.AreEqual(T.DetonatorsWanted, Count(harness, T.DetonatorClass));

            var bombs = TahrendraBase.BarracksBombs(harness.Map);
            Assert.HasCount(T.GuardBarracks.Length, bombs);
            for (var i = 0; i < bombs.Count; i++)
            {
                var barracks = T.GuardBarracks[i];
                Assert.IsFalse(harness.Map.SpawnPools.Single(pool => pool.DbId == barracks.Pool).Quieted, barracks.Name);

                Use(harness, bombs[i].Object);
                Assert.AreEqual(T.DetonatorsWanted - (uint)i - 1, Count(harness, T.DetonatorClass), $"{barracks.Name}: the bomb takes a detonator");
                Assert.AreEqual(UseObjectState.BombStateArmed, bombs[i].State);
                _now += 10_000;
                PlacedUsables.Worker(harness.Map);
                Work(harness);

                Assert.IsTrue(TahrendraBase.Sealed(harness.Map)[i], barracks.Name);
                var door = TahrendraBase.BarracksDoors(harness.Map)[i];
                Assert.IsNotNull(door);
                Assert.IsTrue(door.Sealed);
                var turn = Quaternion.CreateFromYawPitchRoll(barracks.Yaw, 0f, 0f);
                Assert.IsTrue(ForceFields.StopsPlayer(harness.Map, barracks.Door + Vector3.Transform(new Vector3(0, 0.5f, 2f), turn),
                    barracks.Door + Vector3.Transform(new Vector3(0, 0.5f, -2f), turn)), $"{barracks.Name}: shut");
                Assert.IsTrue(harness.Map.SpawnPools.Single(pool => pool.DbId == barracks.Pool).Quieted, $"{barracks.Name}: no more from it");
                Assert.AreEqual(i == bombs.Count - 1 ? MissionObjectiveState.Completed : MissionObjectiveState.Incomplete,
                    Objective(harness, T.BreakOnThrough, T.SealBarracks), barracks.Name);
            }

            Work(harness);
            Assert.AreSame(tillery, TahrendraBase.TilleryOf(harness.Map));
            Assert.IsLessThan(2f, Vector3.Distance(tillery.Position, T.TilleryAtCheckPoint), $"in the check point room, at {tillery.Position}");
            Reward(harness, tillery, T.BreakOnThrough, null, null, T.BreakExperience, (int)T.BreakCredits);

            // Long after, the doors still stand.
            _now += 3_600_000;
            PlacedUsables.Worker(harness.Map);
            Work(harness);
            Assert.IsTrue(TahrendraBase.BarracksDoors(harness.Map).All(door => !door.IsDestroyed && door.Sealed));
        }

        /// <summary>
        /// Tillery's second: Eitak, the terminal, the samples, the teleporter and Donnaway's charge,
        /// set at the red light, the gas through Security Station 2 and its transporter wrecked.
        /// </summary>
        [TestMethod]
        public void EitakTheTerminalTheTeleporterAndDonnawaysCharge()
        {
            using var harness = Create();
            Completed(harness, T.BreakOnThrough);
            Work(harness);
            var tillery = TahrendraBase.TilleryOf(harness.Map);
            Assert.IsLessThan(2f, Vector3.Distance(tillery.Position, T.TilleryAtCheckPoint), "Break on Through done: up already");
            Accept(harness, tillery, T.BringingTheHouseDown);
            Work(harness);

            // The terminal answers once Eitak is dead.
            var terminal = harness.Map.DynamicObjects.Single(obj => (uint)obj.EntityClassId == T.KeyboardClass);
            Assert.AreEqual(T.ConsolePackage, terminal.MissionConversation.NpcPackageId);
            WildernessDaghdasUrnTests.Kill(harness, Put(harness, T.Eitak, T.Station1));
            Assert.AreEqual(MissionObjectiveState.Completed, Objective(harness, T.BringingTheHouseDown, T.SecureStation1));
            Converse(harness, terminal, T.BringingTheHouseDown, T.SecurityConsole);
            Assert.AreEqual(MissionObjectiveState.Completed, Objective(harness, T.BringingTheHouseDown, T.SecurityConsole));
            Assert.AreEqual(MissionObjectiveState.Incomplete, Objective(harness, T.BringingTheHouseDown, T.Samples));
            Assert.AreEqual(MissionObjectiveState.Incomplete, Objective(harness, T.BringingTheHouseDown, T.SecureStation2));

            // The teleporter is for after the samples.
            var teleporter = harness.Map.DynamicObjects.Single(obj => (uint)obj.EntityClassId == T.TeleporterClass);
            Assert.AreEqual(PlayerMessage.PmUseObjectNotUsable, TahrendraBase.Refusal(harness.Client, teleporter));
            for (var i = 0; i < T.SamplesWanted; i++)
                Give(harness, T.SamplesTemplate);
            Assert.AreEqual(MissionObjectiveState.Completed, Objective(harness, T.BringingTheHouseDown, T.Samples));
            Assert.IsNull(TahrendraBase.Refusal(harness.Client, teleporter));

            Use(harness, teleporter);
            Work(harness);
            Assert.IsTrue(TahrendraBase.TeleporterOn(harness.Map));
            Assert.AreEqual(MissionObjectiveState.Completed, Objective(harness, T.BringingTheHouseDown, T.Teleporter));
            Assert.AreEqual(PlayerMessage.PmUseObjectNotUsable, TahrendraBase.Refusal(harness.Client, teleporter), "once");
            var donnaway = TahrendraBase.DonnawayOf(harness.Map);
            Assert.IsNotNull(donnaway);
            Assert.AreEqual(TahrendraBase.Stage.AtTeleporter, TahrendraBase.DonnawayStage(harness.Map));
            Assert.IsTrue(donnaway.ImmuneToAllDamage);

            // She takes the samples for the charge.
            var site = harness.Map.DynamicObjects.Single(obj => obj.ScenarioKey == "tahrendra:bio-site");
            Assert.AreEqual(PlayerMessage.PmUseObjectNotUsable, TahrendraBase.Refusal(harness.Client, site));
            Talk(harness, donnaway, T.BringingTheHouseDown, T.SpeakToDonnaway);
            Assert.AreEqual(0U, Count(harness, T.SamplesClass), "Donnaway has the samples");
            Assert.AreEqual(1U, Count(harness, T.ChargeClass));
            Assert.IsNull(TahrendraBase.Refusal(harness.Client, site));

            // The transporter beams soldiers in at those near it, two at a time.
            var transporter = TahrendraBase.TransporterOf(harness.Map);
            Assert.IsNotNull(transporter);
            harness.MoveTo(T.Transporter + new Vector3(-12, 0, 0));
            Work(harness);
            Assert.HasCount(1, Living(harness, T.Soldier));
            Work(harness, TahrendraBase.BeamEveryMs);
            Work(harness, TahrendraBase.BeamEveryMs);
            var beamed = Living(harness, T.Soldier);
            Assert.HasCount(TahrendraBase.BeamLimit, beamed);

            // The charge on the red light: the gas, and the transporter wrecked.
            Use(harness, site);
            Work(harness);
            Assert.AreEqual(MissionObjectiveState.Completed, Objective(harness, T.BringingTheHouseDown, T.BioBomb));
            Assert.AreEqual(0U, Count(harness, T.ChargeClass), "set");
            Assert.IsFalse(TahrendraBase.BioBombGoneOff(harness.Map), "not yet");
            var xixl = Put(harness, T.Xixl, T.XixlAt);
            var health = xixl.Attributes[Attributes.Health].Current;
            _now += TahrendraBase.FuseMs;
            Work(harness);
            Assert.IsTrue(TahrendraBase.BioBombGoneOff(harness.Map));
            Assert.IsTrue(transporter.IsDown);
            Assert.IsLessThan(health, xixl.Attributes[Attributes.Health].Current + 1, "Xixl gassed");
            Assert.IsLessThan(health * 0.5f, xixl.Attributes[Attributes.Health].Current + 0f, "a share of him");
            foreach (var soldier in beamed)
                Assert.IsTrue(soldier.State == CharacterState.Dead || soldier.Attributes[Attributes.Health].Current < soldier.Attributes[Attributes.Health].NormalMax,
                    "the beamed soldiers gassed");

            // No more beamed in.
            foreach (var soldier in Living(harness, T.Soldier))
                WildernessDaghdasUrnTests.Kill(harness, soldier);
            harness.MoveTo(T.Transporter + new Vector3(-12, 0, 0));
            Work(harness, TahrendraBase.BeamEveryMs);
            Assert.HasCount(0, Living(harness, T.Soldier));

            WildernessDaghdasUrnTests.Kill(harness, xixl);
            Assert.AreEqual(MissionObjectiveState.Completed, Objective(harness, T.BringingTheHouseDown, T.SecureStation2));
            Assert.AreEqual(MissionObjectiveState.Incomplete, Objective(harness, T.BringingTheHouseDown, T.ReturnToTillery));

            // Long after, the transporter is still down.
            _now += 3_600_000;
            PlacedDestructibles.Worker(harness.Map);
            Work(harness);
            Assert.IsTrue(transporter.IsDown);
        }

        /// <summary>The Bot Virus from Tillery: three Warden Bots turned by a hit each; Upper Block Security all dead at once; radioed in.</summary>
        [TestMethod]
        public void TheBotVirusTurnsTheWardenBotsAndUpperBlockIsSecured()
        {
            using var harness = Create();
            Completed(harness, T.BreakOnThrough);
            Work(harness);
            var tillery = TahrendraBase.TilleryOf(harness.Map);
            Accept(harness, tillery, T.BringingTheHouseDown);
            WildernessDaghdasUrnTests.Kill(harness, Put(harness, T.Eitak, T.Station1));
            Converse(harness, harness.Map.DynamicObjects.Single(obj => (uint)obj.EntityClassId == T.KeyboardClass),
                T.BringingTheHouseDown, T.SecurityConsole);
            WildernessDaghdasUrnTests.Kill(harness, Put(harness, T.Xixl, T.XixlAt));

            var bots = TahrendraBase.BotsOf(harness.Map);
            Assert.HasCount(T.WardenBots.Length, bots);
            Assert.IsTrue(bots.All(bot => bot != null && bot.TargetCategory == TargetCategory.Neutral));

            // Without the virus, a hit is a hit.
            Hit(harness, bots[0]);
            Assert.AreEqual(0, TahrendraBase.TurnedBy(harness.Map, harness.Client.Player));
            Assert.AreEqual(TargetCategory.Neutral, bots[0].TargetCategory);

            Talk(harness, tillery, T.BringingTheHouseDown, T.ReturnToTillery);
            Assert.AreEqual(1U, Count(harness, T.VirusClass));
            Assert.AreEqual(MissionObjectiveState.Incomplete, Objective(harness, T.BringingTheHouseDown, T.Robots));
            Assert.AreEqual(MissionObjectiveState.Incomplete, Objective(harness, T.BringingTheHouseDown, T.SecureStation3));

            for (var i = 1; i <= T.RobotsWanted; i++)
            {
                var bot = bots[i];
                var health = bot.Attributes[Attributes.Health].Current;
                Hit(harness, bot);
                Assert.AreEqual(health, bot.Attributes[Attributes.Health].Current, "the virus, not the shot");
                Assert.AreEqual(TargetCategory.Friendly, bot.TargetCategory);
                Assert.AreEqual(harness.Client.Player.EntityId, bot.MasterEntityId);
                Assert.AreEqual(i, TahrendraBase.TurnedBy(harness.Map, harness.Client.Player));
                Work(harness);
                Assert.AreEqual(i == T.RobotsWanted ? MissionObjectiveState.Completed : MissionObjectiveState.Incomplete,
                    Objective(harness, T.BringingTheHouseDown, T.Robots), $"bot {i}");
            }

            // One killed is back a while later, nobody's.
            WildernessDaghdasUrnTests.Kill(harness, bots[0]);
            Work(harness);
            Assert.AreSame(bots[0], TahrendraBase.BotsOf(harness.Map)[0], "not back yet");
            _now += TahrendraBase.BotBackMs;
            Work(harness);
            var back = TahrendraBase.BotsOf(harness.Map)[0];
            Assert.AreNotSame(bots[0], back);
            Assert.AreEqual(TargetCategory.Neutral, back.TargetCategory);

            // Upper Block Security: three of its pools dead is not it; all four is.
            harness.SpawnWorld(T.UpperBlockPools);
            for (var i = 0; i < T.UpperBlockPools.Length; i++)
            {
                var bane = PoolCreatures(harness, T.UpperBlockPools[i]);
                Assert.IsNotEmpty(bane, $"pool {T.UpperBlockPools[i]}");
                foreach (var creature in bane)
                    WildernessDaghdasUrnTests.Kill(harness, creature);
                Work(harness);
                Assert.AreEqual(i == T.UpperBlockPools.Length - 1, TahrendraBase.UpperBlockSecured(harness.Map), $"after pool {i}");
            }
            Assert.AreEqual(MissionObjectiveState.Completed, Objective(harness, T.BringingTheHouseDown, T.SecureStation3));

            Assert.IsTrue(harness.Client.Player.Missions[T.BringingTheHouseDown].Completeable);
            Assert.IsTrue(harness.Manager.TryCompleteRadioMission(harness.Client, T.BringingTheHouseDown, 0, null), "radioed in");
            harness.Drain();
            Assert.AreEqual(1L, Held(harness, T.SalvageTool));
            Assert.AreEqual(0U, Count(harness, T.VirusClass), "the virus goes with it");
        }

        /// <summary>
        /// By radio: Donnaway at the wormhole room's door, followed to its mark; three waves while
        /// she uploads; the hologram's recording; Tillery takes it.
        /// </summary>
        [TestMethod]
        public void DonnawayToTheWormholeTheWavesAndTheHologram()
        {
            using var harness = Create();
            Completed(harness, T.BreakOnThrough);
            Completed(harness, T.BringingTheHouseDown);
            harness.Manager.OfferAfterTurnIn(harness.Client, T.BringingTheHouseDown);
            Assert.IsTrue(harness.Manager.TryAcceptRadioMission(harness.Client, T.WhatGoesAround), "What Goes Around by radio");
            harness.Drain();
            Work(harness);

            var donnaway = TahrendraBase.DonnawayOf(harness.Map);
            Assert.IsNotNull(donnaway);
            Assert.AreEqual(TahrendraBase.Stage.AtDoor, TahrendraBase.DonnawayStage(harness.Map));
            Assert.IsLessThan(2f, Vector3.Distance(donnaway.Position, T.RendezvousAt), $"at the door, at {donnaway.Position}");
            Talk(harness, donnaway, T.WhatGoesAround, T.Rendezvous);
            Assert.AreEqual(MissionObjectiveState.Incomplete, Objective(harness, T.WhatGoesAround, T.EscortDonnaway));

            // Far from her, she waits; near, she follows.
            harness.MoveTo(T.CheckPoint);
            Work(harness);
            Assert.AreEqual(TahrendraBase.Stage.AtDoor, TahrendraBase.DonnawayStage(harness.Map));
            harness.MoveTo(donnaway.Position + new Vector3(2, 0, 0));
            Work(harness);
            Assert.AreEqual(TahrendraBase.Stage.Escorting, TahrendraBase.DonnawayStage(harness.Map));
            harness.MoveTo(T.WormholeMark);
            Work(harness);
            Work(harness);
            Assert.AreEqual(TahrendraBase.Stage.Working, TahrendraBase.DonnawayStage(harness.Map), $"Donnaway at {donnaway.Position}");
            Work(harness);
            Assert.AreEqual(MissionObjectiveState.Completed, Objective(harness, T.WhatGoesAround, T.EscortDonnaway));
            Assert.AreEqual(MissionObjectiveState.Incomplete, Objective(harness, T.WhatGoesAround, T.Protect));

            // Ryxnl and the first wave at once; the second after a minute, the third after two.
            var waves = TahrendraBase.WavesOf(harness.Map);
            Assert.HasCount(5, waves[0]);
            Assert.IsTrue(waves[0].Any(creature => creature.DbId == T.Ryxnl));
            Assert.HasCount(0, waves[1]);
            KillAll(harness, waves[0]);

            Work(harness, TahrendraBase.Wave2Ms);
            Assert.HasCount(4, TahrendraBase.WavesOf(harness.Map)[1]);
            Assert.AreEqual(MissionObjectiveState.Incomplete, Objective(harness, T.WhatGoesAround, T.Wave2));
            KillAll(harness, TahrendraBase.WavesOf(harness.Map)[1]);
            Work(harness);
            Assert.AreEqual(MissionObjectiveState.Completed, Objective(harness, T.WhatGoesAround, T.Wave2));

            Work(harness, TahrendraBase.Wave3Ms - TahrendraBase.Wave2Ms);
            Assert.HasCount(5, TahrendraBase.WavesOf(harness.Map)[2]);
            KillAll(harness, TahrendraBase.WavesOf(harness.Map)[2]);
            Work(harness);
            Assert.AreEqual(MissionObjectiveState.Completed, Objective(harness, T.WhatGoesAround, T.Wave3));
            Assert.IsFalse(TahrendraBase.UploadDone(harness.Map), "three minutes, not two");
            Assert.IsNull(TahrendraBase.HologramOf(harness.Map));

            Work(harness, TahrendraBase.UploadMs - TahrendraBase.Wave3Ms);
            Assert.IsTrue(TahrendraBase.UploadDone(harness.Map));
            Assert.AreEqual(MissionObjectiveState.Completed, Objective(harness, T.WhatGoesAround, T.Protect));
            var hologram = TahrendraBase.HologramOf(harness.Map);
            Assert.IsNotNull(hologram);
            Talk(harness, hologram, T.WhatGoesAround, T.Recording);
            Assert.IsTrue(harness.Client.Player.Missions[T.WhatGoesAround].Completeable);

            var tillery = TahrendraBase.TilleryOf(harness.Map);
            Reward(harness, tillery, T.WhatGoesAround, 0, T.GravitonVest, T.AroundExperience, (int)T.AroundCredits);
        }

        /// <summary>A member with Donnaway delivered before comes into a new copy: she is at work, and the upload under way.</summary>
        [TestMethod]
        public void ANewCopyPicksUpWhereTheMemberLeftOff()
        {
            using var harness = Create();
            Completed(harness, T.BreakOnThrough);
            Completed(harness, T.BringingTheHouseDown);
            harness.Manager.OfferAfterTurnIn(harness.Client, T.BringingTheHouseDown);
            Assert.IsTrue(harness.Manager.TryAcceptRadioMission(harness.Client, T.WhatGoesAround));
            harness.Drain();
            Work(harness);
            Talk(harness, TahrendraBase.DonnawayOf(harness.Map), T.WhatGoesAround, T.Rendezvous);
            harness.MoveTo(TahrendraBase.DonnawayOf(harness.Map).Position + new Vector3(2, 0, 0));
            Work(harness);
            harness.MoveTo(T.WormholeMark);
            Work(harness);
            Work(harness);
            Work(harness);
            Assert.AreEqual(MissionObjectiveState.Completed, Objective(harness, T.WhatGoesAround, T.EscortDonnaway));

            // A new copy: everything forgotten but the member's log.
            foreach (var wave in TahrendraBase.WavesOf(harness.Map))
                KillAll(harness, wave);
            TahrendraBase.Reset();
            Work(harness);
            Assert.AreEqual(TahrendraBase.Stage.Working, TahrendraBase.DonnawayStage(harness.Map));
            Assert.IsNotEmpty(TahrendraBase.WavesOf(harness.Map)[0], "the first wave again");
        }

        #region Helpers

        private static void OnTheGround(NavMeshQuery mesh, Vector3 at, string what)
        {
            var ground = mesh.Nearest(at);
            Assert.IsTrue(ground.HasValue, $"{what} off the ground");
            Assert.IsLessThan(0.6f, Vector2.Distance(new Vector2(at.X, at.Z), new Vector2(ground.Value.X, ground.Value.Z)), $"{what} over walkable ground");
            Assert.IsLessThan(1.2f, Math.Abs(ground.Value.Y - at.Y), $"{what}: the ground {ground.Value.Y - at.Y} m from it");
        }

        private static long Count(Rasa.Test.Database.OrdinaryLootTestDatabase database, string sql) =>
            database.Context.Database.SqlQueryRaw<long>(sql).ToArray().Single();

        private static uint Count(WildernessRuntimeTestHarness harness, uint classId) =>
            InventoryManager.Instance.CountItemsByClass(harness.Client, (EntityClasses)classId, includeMissionItems: true);

        private static Dictionary<uint, string> Pools(Rasa.Test.Database.OrdinaryLootTestDatabase database) =>
            database.Context.SpawnPoolEntries.AsNoTracking().Where(p => p.MapContextId == T.Tahrendra).ToList()
                .ToDictionary(p => p.Id, p => $"{p.PosX:F2} {p.PosY:F2} {p.PosZ:F2} {p.Rotation:F2} {p.Creature1Id} {p.Creature2Id} " +
                    $"{p.Creature3Id} {p.Creature4Id} {p.Creature5Id} {p.Creature6Id}");

        private static WildernessRuntimeTestHarness Create()
        {
            var harness = WildernessRuntimeTestHarness.Create(mapContextId: T.Tahrendra);
            try
            {
                using (var unit = harness.CreateChar())
                    unit.ExecuteTransaction(() => unit.CharacterStartingExperience.Add(
                        new CharacterStartingExperienceEntry(harness.Client.Player.Id, T.Revision, CharacterStartingExperienceState.Skipped)));
                harness.Client.Player.StartingExperienceCompleted = true;
                harness.Drain();
                harness.MoveTo(T.Arrival);
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

        private void Work(WildernessRuntimeTestHarness harness, long milliseconds = 100)
        {
            _now += milliseconds;
            TahrendraBase.Worker(harness.Map);
            harness.Drain();
        }

        private static List<Creature> Living(WildernessRuntimeTestHarness harness, uint dbId) =>
            harness.Map.MapCellInfo.Cells.Values.SelectMany(cell => cell.CreatureList).Distinct()
                .Where(c => c.DbId == dbId && c.State != CharacterState.Dead && EntityManager.Instance.Creatures.ContainsKey(c.EntityId)).ToList();

        private static List<Creature> PoolCreatures(WildernessRuntimeTestHarness harness, uint poolId) =>
            harness.Map.MapCellInfo.Cells.Values.SelectMany(cell => cell.CreatureList).Distinct()
                .Where(creature => creature.SpawnPool?.DbId == poolId && CreatureManager.IsLivingOnMap(harness.Map, creature)).ToList();

        private static void KillAll(WildernessRuntimeTestHarness harness, IEnumerable<Creature> creatures)
        {
            foreach (var creature in creatures.ToList())
                WildernessDaghdasUrnTests.Kill(harness, creature);
        }

        /// <summary>A shot from the harness's player, through the real damage path.</summary>
        private static void Hit(WildernessRuntimeTestHarness harness, Creature creature)
        {
            harness.MoveTo(creature.Position + new Vector3(4, 0, 0));
            ActorManager.Instance.Damage(harness.Map, creature, 100, harness.Client.Player);
            harness.Drain();
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

        /// <summary>An object's conversation opened and its objective done, as the client asks it.</summary>
        private static void Converse(WildernessRuntimeTestHarness harness, DynamicObject obj, uint missionId, uint objectiveId)
        {
            var npcs = new NpcManager(harness, harness.Manager);
            harness.MoveTo(obj.Position + new Vector3(2, 0, 0));
            harness.Drain();
            npcs.RequestNpcConverse(harness.Client, new RequestNPCConversePacket { EntityId = obj.EntityId, ActionId = ActionId.Converse, ActionArgId = 1 });
            npcs.CompleteNPCObjective(harness.Client, new CompleteNPCObjectivePacket
            {
                EntityId = obj.EntityId, MissionId = missionId, ObjectiveId = objectiveId, PlayerFlagId = 1
            });
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
                    ContentRevision = T.Revision
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
