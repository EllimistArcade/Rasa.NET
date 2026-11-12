using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Rasa.Test.World
{
    using Rasa.Data;
    using Rasa.Game;
    using Rasa.Managers;
    using Rasa.Missions;
    using Rasa.Packets;
    using Rasa.Packets.Manifestation.Server;
    using Rasa.Structures;
    using Rasa.Test.Missions;

    /// <summary>
    /// A kill's experience (KillExperience): BASE_KILL_XP scaled by the creature's level as the
    /// client scales by level, doubling every eight; and each player's danger penalty, 20% less a
    /// level from five below them and nothing from nine.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class KillExperienceTests
    {
        [TestInitialize]
        public void Begin() => KillStreaks.Reset();

        [TestCleanup]
        public void End() => KillStreaks.Reset();

        [TestMethod]
        public void AKillIsBaseKillXpDoublingEveryEightLevels()
        {
            Assert.AreEqual(62.5, KillExperience.BaseKillXp, "BASE_KILL_XP");

            var expected = new Dictionary<uint, uint> { [1] = 62, [9] = 125, [17] = 250, [25] = 500, [33] = 1000, [41] = 2000, [49] = 4000, [50] = 4362 };

            foreach (var (level, experience) in expected)
                Assert.AreEqual(experience, KillExperience.BaseFor(level), $"level {level}");

            Assert.AreEqual(62u, KillExperience.BaseFor(0), "no level: as level 1");
            Assert.AreEqual(74u, KillExperience.BaseFor(3), "int(62.5 x 2^(2/8))");
        }

        [TestMethod]
        public void TheDangerPenaltyIsTwentyPercentALevelFromFiveBelowAndAllOfItFromNine()
        {
            Assert.AreEqual(0.2, KillExperience.DangerPenaltyValue, "DANGER_PENALTY_VALUE");
            Assert.AreEqual(5, KillExperience.DangerLevelDiffMin, "DANGER_PENALTY_LEVELDIFF_MIN");
            Assert.AreEqual(9, KillExperience.DangerLevelDiffMax, "DANGER_PENALTY_LEVELDIFF_MAX");

            var expected = new Dictionary<uint, double> { [30] = 1, [20] = 1, [16] = 1, [15] = 0.8, [14] = 0.6, [13] = 0.4, [12] = 0.2, [11] = 0, [1] = 0 };

            foreach (var (creature, factor) in expected)
                Assert.AreEqual(factor, KillExperience.DangerFactor(20, creature), 1e-9, $"a level 20 player, a level {creature} creature");

            Assert.AreEqual(1.0, KillExperience.DangerFactor(20, 0), "no level: no penalty");
            Assert.AreEqual(49u, KillExperience.AfterDanger(62, 20, 15), "80% of 62, rounded down");
            Assert.AreEqual(62u, KillExperience.AfterDanger(62, 20, 16));
            Assert.AreEqual(0u, KillExperience.AfterDanger(62, 20, 11));
        }

        [TestMethod]
        public void AKillPaysItsLevelsWorthLessThePlayersPenalty()
        {
            using var context = MissionTestContext.WithProgressMission(
                MissionProgressRule.CompleteOnExactSubject(MissionProgressEventKind.CreatureKilled, 82));
            var killer = context.Client;

            SetLevel(context, killer, 17);
            Assert.AreEqual(250u, Gained(Kill(context, Corpse(context, 17))), "a level 17 creature: 250");

            SetLevel(context, killer, 22);
            Assert.AreEqual(200u, Gained(Kill(context, Corpse(context, 17))), "five below: 80% of 250");

            SetLevel(context, killer, 25);
            Assert.AreEqual(50u, Gained(Kill(context, Corpse(context, 17))), "eight below: 20%");
        }

        [TestMethod]
        public void ACreatureNineLevelsBelowIsWorthNothingAndNoPartOfTheStreak()
        {
            using var context = MissionTestContext.WithProgressMission(
                MissionProgressRule.CompleteOnExactSubject(MissionProgressEventKind.CreatureKilled, 82));
            var killer = context.Client;
            SetLevel(context, killer, 10);
            var experience = killer.Player.Experience;

            var sent = Kill(context, Corpse(context, 1));

            Assert.IsEmpty(sent.OfType<ExperienceChangedPacket>().ToList());
            Assert.AreEqual(experience, killer.Player.Experience);
            Assert.AreEqual(0, KillStreaks.CreditOf(killer), "no part of a streak");

            // One level nearer, it is worth a fifth.
            SetLevel(context, killer, 9);
            Assert.AreEqual(12u, Gained(Kill(context, Corpse(context, 1))), "20% of 62");
            Assert.IsGreaterThan(0, KillStreaks.CreditOf(killer));
        }

        [TestMethod]
        public void InASquadEachSharerPaysTheirOwnPenaltyAndOneItIsNothingToTakesNoShare()
        {
            using var context = MissionTestContext.WithProgressMission(
                MissionProgressRule.CompleteOnExactSubject(MissionProgressEventKind.CreatureKilled, 82));
            var killer = context.Client;
            var near = context.CreateAdditionalClient(2);
            var far = context.CreateAdditionalClient(3);
            SetLevel(context, killer, 17);
            near.Player.Level = 22;
            far.Player.Level = 30;
            var sharers = new List<Client> { killer, near, far };

            Assert.IsTrue(KillShares.GainsExperience(killer, 17));
            Assert.IsTrue(KillShares.GainsExperience(near, 17));
            Assert.IsFalse(KillShares.GainsExperience(far, 17), "thirteen above it: nothing");
            Assert.AreEqual(2, sharers.Count(sharer => KillShares.GainsExperience(sharer, 17)), "the two it is worth something to share it");
            Assert.IsTrue(KillShares.GainsExperience(far, 0), "no level: no penalty");
        }

        /// <summary>The player at the level, in the database too, which an award of experience is checked against.</summary>
        private static void SetLevel(MissionTestContext context, Client client, byte level)
        {
            using var unit = context.CreateChar();
            unit.Characters.UpdateCharacterProgression(client.Player.Id, client.Player.Experience, level);
            client.Player.Level = level;
        }

        private static uint Gained(List<PythonPacket> sent) => sent.OfType<ExperienceChangedPacket>().Single().XPInfo.BaseGained;

        /// <summary>A creature of the level where the killer stands, in a spawn pool, as CreatureXpAndImmunityTests makes one.</summary>
        private static Creature Corpse(MissionTestContext context, uint level)
        {
            return new Creature
            {
                DbId = 9001,
                EntityClass = EntityClasses.HumanBaseMale,
                MapContextId = context.Map.MapInfo.MapContextId,
                Position = context.Client.Player.Position,
                Level = level,
                State = CharacterState.Idle,
                AppearanceData = new Dictionary<EquipmentData, AppearanceData>(),
                Attributes = Enum.GetValues<Attributes>().ToDictionary(
                    id => id, id => new ActorAttributes(id, 100, 100, 0, 0, 0)),
                SpawnPool = new SpawnPool
                {
                    MapContextId = context.Map.MapInfo.MapContextId,
                    AliveCreatures = 1,
                    RespawnTime = 1000,
                    UpdateTimer = 1000
                }
            };
        }

        /// <summary>The creature killed by the context's player, and what their client was sent for it.</summary>
        private static List<PythonPacket> Kill(MissionTestContext context, Creature creature)
        {
            CellManager.Instance.AddToWorld(context.Map, creature);
            MissionTestContext.Drain(context.Client);

            try
            {
                new CreatureManager(context, new ManifestationManager(context), context.Manager)
                    .HandleCreatureKill(context.Map, creature, context.Client.Player);

                return MissionTestContext.Drain(context.Client);
            }
            finally
            {
                CellManager.Instance.RemoveCreatureFromWorld(context.Map, creature);
            }
        }
    }
}
