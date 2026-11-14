using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Rasa.Test.World
{
    using Rasa.Data;
    using Rasa.Game;
    using Rasa.Managers;
    using Rasa.Missions;
    using Rasa.Packets.Manifestation.Server;
    using Rasa.Packets.MapChannel.Client;
    using Rasa.Packets.MapChannel.Server;
    using Rasa.Structures;
    using Rasa.Test.Missions;

    // The Experience Boosters (CONSUMABLE_ADD_EFFECT 503) were refused as not implemented and
    // kept. One puts XP_BOOST_EFFECT on the player: GAME_EFFECT_ARG1 percent more experience from
    // kills for GAME_EFFECT_ARG2 seconds, to their own part of a squad's kill only.
    [TestClass]
    [DoNotParallelize]
    public class ExperienceBoosterTests
    {
        private const uint BoosterAction = 503;
        private const uint Booster = 122036;        // Experience Booster, 50% for 30 minutes
        private const uint HyperBooster = 131966;   // Hyper Experience Booster, 2000% for 60 minutes
        private const long Now = 1_800_000_000_000;

        [TestMethod]
        public void ABoosterPutsOnHalfAgainForThirtyMinutesAndIsUsedUp()
        {
            using var harness = BootcampRuntimeTestHarness.Create();
            var player = harness.Client.Player;

            var (item, packets) = Use(harness, 1, Booster);

            var boost = player.ActiveEffects.Values.Single(e => e.TypeId == ExperienceBoost.TypeId);
            Assert.AreEqual(50, boost.ExperienceBoostPercent);
            Assert.AreEqual(1u, boost.EffectLevel);
            Assert.AreEqual(1_800_000, boost.RemainingMs, 2000);
            Assert.IsTrue(boost.IsBuff && boost.AllowDetach, "a buff the player may click off");
            Assert.AreEqual(50, ExperienceBoost.PercentOf(player));

            Assert.IsFalse(player.Inventory.PersonalInventory.Contains(item.EntityId), "used up");
            Assert.IsTrue(player.ActionReuseUntil.ContainsKey(ActionId.ConsumableAddEffect), "the 10 s cooldown");

            // The client's AddEffectAbility announces the effect type of each hit; the attach does not.
            var recovery = packets.OfType<AbilityRecoveryPacket>().Single(p => p.ActionId == ActionId.ConsumableAddEffect);
            Assert.AreEqual(AbilityRecoveryPacket.HitDataKind.EffectAttach, recovery.Kind);
            Assert.AreEqual(player.EntityId, recovery.Hits.Single().EntityId);
            Assert.AreEqual(ExperienceBoost.TypeId, recovery.Hits.Single().EffectTypeId);
            Assert.AreEqual(boost.EffectId, packets.OfType<GameEffectAttachedPacket>().Single(p => p.EffectTypeId == ExperienceBoost.TypeId).EffectId);
        }

        [TestMethod]
        public void TheHyperBoosterIsTwentyTimesMoreForAnHour()
        {
            using var harness = BootcampRuntimeTestHarness.Create();

            Use(harness, 3, HyperBooster);

            var boost = harness.Client.Player.ActiveEffects.Values.Single(e => e.TypeId == ExperienceBoost.TypeId);
            Assert.AreEqual(2000, boost.ExperienceBoostPercent);
            Assert.AreEqual(3_600_000, boost.RemainingMs, 2000);
        }

        [TestMethod]
        public void ASecondBoosterStartsTheTimeAgainAndDoesNotStack()
        {
            using var harness = BootcampRuntimeTestHarness.Create();
            var player = harness.Client.Player;

            // A Hyper boost with a minute left, and a 50% booster used over it: the time is the
            // new booster's, the percent stays the higher.
            ExperienceBoost.Put(harness.BootcampMap, player, 2000, 3, 60_000, announce: false);
            Use(harness, 1, Booster);

            var boost = player.ActiveEffects.Values.Single(e => e.TypeId == ExperienceBoost.TypeId);
            Assert.AreEqual(2000, boost.ExperienceBoostPercent);
            Assert.AreEqual(1_800_000, boost.RemainingMs, 2000);

            // And a stronger one over a weaker: its own percent, and its own time.
            ExperienceBoost.Put(harness.BootcampMap, player, 50, 1, 60_000, announce: false);
            player.ActionReuseUntil.Clear();
            Use(harness, 3, HyperBooster);

            boost = player.ActiveEffects.Values.Single(e => e.TypeId == ExperienceBoost.TypeId);
            Assert.AreEqual(2000, boost.ExperienceBoostPercent);
            Assert.AreEqual(3_600_000, boost.RemainingMs, 2000);
            Assert.AreEqual(2000, ExperienceBoost.PercentOf(player), "one boost, not two");
        }

        [TestMethod]
        public void TheBoostRaisesOnlyItsHoldersOwnPartOfASquadsKill()
        {
            using var context = MissionTestContext.WithProgressMission(
                MissionProgressRule.CompleteOnExactSubject(MissionProgressEventKind.CreatureKilled, 82));
            var boosted = context.Client;
            var mate = context.CreateAdditionalClient(2);
            var manifestations = new ManifestationManager(context);

            Assert.IsNotNull(ExperienceBoost.Put(context.Map, boosted.Player, 50, 1, 60_000, announce: false));

            // 100 shared by two first: 92 each, and the holder's 92 half again.
            MissionTestContext.Drain(boosted);
            MissionTestContext.Drain(mate);
            Assert.AreEqual(92u, KillShares.AwardExperience(manifestations, new[] { boosted, mate }, boosted, 100));
            Assert.AreEqual(138u, boosted.Player.Experience);
            Assert.AreEqual(92u, mate.Player.Experience, "another's boost does nothing for them");

            // Each line says it: " (+50% Booster Bonus)" on the holder's, none on the mate's.
            var holders = MissionTestContext.Drain(boosted).OfType<ExperienceChangedPacket>().Single().XPInfo;
            var mates = MissionTestContext.Drain(mate).OfType<ExperienceChangedPacket>().Single().XPInfo;
            Assert.AreEqual(1.5, holders.BoosterMod, 1e-9);
            Assert.AreEqual(1, mates.BoosterMod);
            Assert.AreEqual(1.84, holders.GroupMod, 1e-9);
            Assert.AreEqual(50u, holders.BaseGained);

            // The mate's kill is no different: the boost is to the holder's part, whoever killed.
            Assert.AreEqual(92u, KillShares.AwardExperience(manifestations, new[] { mate, boosted }, mate, 100));
            Assert.AreEqual(276u, boosted.Player.Experience);
            Assert.AreEqual(184u, mate.Player.Experience);
        }

        [TestMethod]
        [DataRow(0, 100u, 100u)]
        [DataRow(50, 100u, 150u)]
        [DataRow(50, 33u, 50u)]
        [DataRow(2000, 10u, 210u)]
        [DataRow(50, 0u, 0u)]
        public void APartIsRaisedByThePercent(int percent, uint experience, uint boosted)
        {
            using var world = new WorldTestContext();
            var player = Player(world).Player;

            if (percent > 0)
                ExperienceBoost.Put(world.Map, player, percent, 1, 60_000, announce: false);

            Assert.AreEqual(boosted, ExperienceBoost.Boosted(player, experience));
        }

        [TestMethod]
        public void TheBoostGoesAcrossAMapChangeAndThroughADeath()
        {
            using var world = new WorldTestContext();
            var player = Player(world).Player;
            var boost = ExperienceBoost.Put(world.Map, player, 50, 1, 600_000, announce: false);

            Assert.IsTrue(PlayerDeath.KeptInDeath(boost, keepMorph: false));
            Assert.IsTrue(EffectCarry.Carries(player, boost));

            EffectCarry.Stash(player);
            Assert.AreSame(boost, player.CarriedEffects.Single().Effect);
            Assert.AreEqual(50, player.CarriedEffects.Single().Effect.ExperienceBoostPercent);
        }

        [TestMethod]
        public void LoggingOutKeepsTheTimeTheBoostHadLeft()
        {
            using var world = new WorldTestContext();
            var client = Player(world);
            var player = client.Player;

            ExperienceBoost.Put(world.Map, player, 2000, 3, 900_000, announce: false);

            var saved = RelogVitals.Capture(player, Now);
            Assert.AreEqual(2000, saved.XpBoostPercent);
            Assert.AreEqual(3u, saved.XpBoostLevel);
            Assert.AreEqual(900_000, saved.XpBoostRemainingMs, 2000);

            // Back an hour later: the time away does not count, and it is told to the client.
            GameEffectManager.Instance.ClearEffects(world.Map, player);
            WorldTestContext.Drain(client);
            player.LeftWith = saved;
            RelogVitals.RestorePenalties(client, Now + 3_600_000);

            var boost = player.ActiveEffects.Values.Single(e => e.TypeId == ExperienceBoost.TypeId);
            Assert.AreEqual(2000, boost.ExperienceBoostPercent);
            Assert.AreEqual(3u, boost.EffectLevel);
            Assert.AreEqual(900_000, boost.RemainingMs, 2000);
            Assert.IsTrue(WorldTestContext.Drain(client).Select(p => p.Message).OfType<Rasa.Packets.Protocol.CallMethodMessage>()
                .Any(m => m.Packet is GameEffectAttachedPacket attached && attached.EffectTypeId == ExperienceBoost.TypeId));

            // None: nothing saved, nothing put on.
            GameEffectManager.Instance.ClearEffects(world.Map, player);
            Assert.AreEqual(0, RelogVitals.Capture(player, Now).XpBoostPercent);
            player.LeftWith = new SavedVitals { XpBoostPercent = 50, XpBoostLevel = 1, XpBoostRemainingMs = 0 };
            RelogVitals.RestorePenalties(client, Now);
            Assert.IsFalse(player.ActiveEffects.Values.Any(e => e.TypeId == ExperienceBoost.TypeId));
        }

        [TestMethod]
        public void TheBoostIsSavedWithTheCharacter()
        {
            using var context = MissionTestContext.WithCompletableMission(429);
            var id = context.Client.Player.Id;

            using (var unit = context.CreateChar())
                unit.Characters.UpdateCharacterXpBoost(id, 50, 2, 1_234_000);

            using (var unit = context.CreateChar())
            {
                var saved = SavedVitals.From(unit.Characters.Find(id));
                Assert.AreEqual(50, saved.XpBoostPercent);
                Assert.AreEqual(2u, saved.XpBoostLevel);
                Assert.AreEqual(1_234_000L, saved.XpBoostRemainingMs);
            }

            // None left: the row says none.
            using (var unit = context.CreateChar())
                unit.Characters.UpdateCharacterXpBoost(id, 0, 2, 1_234_000);

            using (var unit = context.CreateChar())
            {
                var entry = unit.Characters.Find(id);
                Assert.AreEqual(0u, entry.XpBoostPercent);
                Assert.AreEqual(0u, entry.XpBoostLevel);
                Assert.AreEqual(0L, entry.XpBoostRemainingMs);
            }
        }

        [TestMethod]
        public void WalkingOffAfterItLandedShowsTheBoostRatherThanLosingIt()
        {
            using var harness = BootcampRuntimeTestHarness.Create();
            var client = harness.Client;
            var player = client.Player;

            // The client plays the recovery, and the player starts walking before it has
            // announced the boost: it cancels the action and asks for it to be interrupted.
            var (item, _) = Use(harness, 1, Booster);
            var boost = player.ActiveEffects.Values.Single(e => e.TypeId == ExperienceBoost.TypeId);

            ActorManager.Instance.RequestActionInterrupt(client, Interrupt(1));

            // Too late to call off: used, and on - and shown, taken off the client's quiet copy
            // first, since it will not announce an effect id it holds.
            Assert.IsFalse(player.Inventory.PersonalInventory.Contains(item.EntityId), "used");
            Assert.IsTrue(player.ActiveEffects.ContainsKey(boost.EffectId), "on");
            var sent = harness.Drain();
            var detach = sent.OfType<GameEffectDetachedPacket>().Single();
            var attach = sent.OfType<GameEffectAttachedPacket>().Single();
            Assert.AreEqual(boost.EffectId, detach.EffectId);
            Assert.AreEqual(boost.EffectId, attach.EffectId);
            Assert.AreEqual(ExperienceBoost.TypeId, attach.EffectTypeId);
            Assert.IsTrue(attach.Announced, "announced");
            Assert.IsLessThan(sent.ToList().IndexOf(attach), sent.ToList().IndexOf(detach), "the detach first");
            Assert.IsFalse(sent.OfType<ActionInterruptPacket>().Any(), "not interrupted: it landed");

            // Once.
            ActorManager.Instance.RequestActionInterrupt(client, Interrupt(1));
            Assert.IsEmpty(harness.Drain().ToList());
        }

        [TestMethod]
        public void WalkingOffWhileItWindsUpCallsItOffAndKeepsTheBooster()
        {
            using var harness = BootcampRuntimeTestHarness.Create();
            var client = harness.Client;
            var item = ToyTests.Grant(harness, Booster);
            var manager = ToyTests.CreateManager(harness, BoosterAction, 1, Booster);

            manager.RequestPerformAbility(client, ToyTests.Request((int)BoosterAction, 1, item.EntityId));
            var pending = harness.BootcampMap.PerformRecovery.Single(candidate => candidate.ActionId == ActionId.ConsumableAddEffect);
            ActorManager.Instance.RequestActionInterrupt(client, Interrupt(1));
            ToyTests.Land(harness, manager, pending);

            Assert.IsTrue(client.Player.Inventory.PersonalInventory.Contains(item.EntityId), "kept");
            Assert.IsFalse(client.Player.ActiveEffects.Values.Any(e => e.TypeId == ExperienceBoost.TypeId));
            Assert.IsNull(client.Player.QuietlyLanded);
            var sent = harness.Drain();
            Assert.IsTrue(sent.OfType<ActionInterruptPacket>().Any(), "Interrupted");
            Assert.IsFalse(sent.OfType<GameEffectAttachedPacket>().Any());
        }

        [TestMethod]
        public void AnInterruptForAnotherActionOrLongAfterShowsNothing()
        {
            using var harness = BootcampRuntimeTestHarness.Create();
            var client = harness.Client;

            Use(harness, 1, Booster);

            ActorManager.Instance.RequestActionInterrupt(client, Interrupt(3));
            ActorManager.Instance.RequestActionInterrupt(client, new RequestActionInterruptPacket { ActionId = ActionId.WeaponAttack, ActionArgId = 1 });
            Assert.IsFalse(harness.Drain().OfType<GameEffectAttachedPacket>().Any(), "not that action");

            client.Player.QuietlyLanded.LandedTick -= QuietLandings.WindowMs + 1;
            ActorManager.Instance.RequestActionInterrupt(client, Interrupt(1));
            Assert.IsFalse(harness.Drain().OfType<GameEffectAttachedPacket>().Any(), "its recovery long played");
        }

        private static RequestActionInterruptPacket Interrupt(uint level) =>
            new RequestActionInterruptPacket { ActionId = ActionId.ConsumableAddEffect, ActionArgId = level };

        /// <summary>Uses the item and lands the action; what was sent, which must not be a refusal.</summary>
        private static (Item Item, System.Collections.Generic.List<object> Packets) Use(BootcampRuntimeTestHarness.Harness harness, uint level, uint template)
        {
            var item = ToyTests.Grant(harness, template);
            var manager = ToyTests.CreateManager(harness, BoosterAction, level, template);
            harness.Drain();

            manager.RequestPerformAbility(harness.Client, ToyTests.Request((int)BoosterAction, level, item.EntityId));
            var pending = harness.BootcampMap.PerformRecovery.Single(candidate => candidate.ActionId == ActionId.ConsumableAddEffect);
            ToyTests.Land(harness, manager, pending);

            var packets = harness.Drain().Cast<object>().ToList();
            Assert.IsFalse(packets.OfType<UserActionFailedPacket>().Any());

            return (item, packets);
        }

        private static Client Player(WorldTestContext world)
        {
            var client = world.CreateClient(0, 0);
            client.Player.Level = 10;
            CellManager.Instance.AddToWorld(client);
            WorldTestContext.Drain(client);
            return client;
        }
    }
}
