using System.Numerics;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Rasa.Test.World
{
    using Rasa.Managers;
    using Rasa.Structures;
    using Rasa.Test.Missions.Wilderness;

    /// <summary>
    /// The threat design's pulse, call for help and fade (Threat.Tick, Threat.Pulse,
    /// Threat.CallForHelp, HateTable.Fade).
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class ThreatPulseHelpFadeTests
    {
        private const uint ThraxSoldier = 3;        // Bane_Thrax_Soldier

        [TestMethod]
        public void AnEntryFadesOnlyAfterFiveQuietSecondsAndComesOffBelowOne()
        {
            var table = new HateTable();
            table.Add(1, 100);
            table.Add(2, 100);

            Fade(table, 5000);
            Assert.AreEqual(100, table.Of(1), 1e-9, "five quiet seconds: nothing yet");

            Fade(table, 1000);
            Assert.AreEqual(90, table.Of(1), 1e-9, "the sixth second takes a tenth");

            table.Add(2, 10);
            Assert.AreEqual(0, table.QuietMs(2), "a gain starts its quiet over");
            Fade(table, 1000);
            Assert.AreEqual(100, table.Of(2), 1e-9, "90 and the 10 it gained, not faded");
            Assert.AreEqual(81, table.Of(1), 1e-9);

            Fade(table, 60_000);
            Assert.IsFalse(table.Contains(1), "below one, off the table");
        }

        [TestMethod]
        public void TheTargetKeepsTheTokenWhileItFades()
        {
            var table = new HateTable();
            table.Ensure(7, Threat.NoticedThreat);
            table.Add(8, 50);

            table.Fade(120_000, Threat.FadeAfterMs, Threat.FadePerSecond, Threat.NoticedThreat, keep: 7);

            Assert.AreEqual(Threat.NoticedThreat, table.Of(7), "the one it fights stays on the table");
            Assert.IsFalse(table.Contains(8));
        }

        [TestMethod]
        public void AFightingCreaturePulsesOnTheEnemiesNearIt()
        {
            using var harness = WildernessRuntimeTestHarness.Create();
            var player = harness.Client.Player;
            var thrax = Put(harness, player.Position + new Vector3(10, 0, 0));
            Assert.IsTrue(BehaviorManager.Instance.TrySetActionFighting(thrax, player.EntityId));
            var before = thrax.Hate.Of(player.EntityId);

            Threat.Tick(harness.Map, thrax, Threat.PulseIntervalMs - 1);
            Assert.AreEqual(before, thrax.Hate.Of(player.EntityId), 1e-9, "not yet");
            Threat.Tick(harness.Map, thrax, 1);
            Assert.AreEqual(before + Threat.PulseHate, thrax.Hate.Of(player.EntityId), 1e-9, "within 20 m: 100");

            // Out of the pulse's reach.
            thrax.Position = player.Position + new Vector3(Threat.PulseRadius + 5, 0, 0);
            var held = thrax.Hate.Of(player.EntityId);
            Threat.Tick(harness.Map, thrax, Threat.PulseIntervalMs);
            Assert.AreEqual(held, thrax.Hate.Of(player.EntityId), 1e-9, "25 m off: nothing");
        }

        [TestMethod]
        public void ACreatureNotFightingDoesNotPulse()
        {
            using var harness = WildernessRuntimeTestHarness.Create();
            var player = harness.Client.Player;
            var thrax = Put(harness, player.Position + new Vector3(5, 0, 0));

            Threat.Tick(harness.Map, thrax, Threat.PulseIntervalMs * 3);

            Assert.IsFalse(thrax.Hate.Contains(player.EntityId), "the pulse starts no fight");
        }

        [TestMethod]
        public void TheFirstHurtCallsItsSideWithinSixtyMetresOnceAFight()
        {
            using var harness = WildernessRuntimeTestHarness.Create();
            var player = harness.Client.Player;
            var hurt = Put(harness, player.Position + new Vector3(10, 0, 0));
            var near = Put(harness, hurt.Position + new Vector3(0, 0, 40));
            var far = Put(harness, hurt.Position + new Vector3(0, 0, 70));

            Threat.FromDamage(hurt, player, 10);

            Assert.AreEqual(BehaviorManager.BehaviorActionFighting, near.Controller.CurrentAction, "40 m: called");
            Assert.AreEqual(player.EntityId, near.Controller.ActionFighting.TargetEntityId);
            Assert.AreEqual(Threat.NoticedThreat, near.Hate.Of(player.EntityId), 1e-9, "at the token hate");
            Assert.AreNotEqual(BehaviorManager.BehaviorActionFighting, far.Controller.CurrentAction, "70 m: not called");

            // Once a fight: a second hurt calls nobody new.
            var late = Put(harness, hurt.Position + new Vector3(0, 0, -30));
            Threat.FromDamage(hurt, player, 10);
            Assert.AreNotEqual(BehaviorManager.BehaviorActionFighting, late.Controller.CurrentAction, "called once");

            // Its table empties and the fight is over: the next fight calls again.
            BehaviorManager.Instance.GiveUp(hurt);
            Threat.Tick(harness.Map, hurt, 1);
            Threat.FromDamage(hurt, player, 10);
            Assert.AreEqual(BehaviorManager.BehaviorActionFighting, late.Controller.CurrentAction, "a new fight, a new call");
        }

        [TestMethod]
        public void AGmSetAlwaysFriendlyIsNotPulsedOn()
        {
            using var harness = WildernessRuntimeTestHarness.Create();
            var player = harness.Client.Player;
            var thrax = Put(harness, player.Position + new Vector3(10, 0, 0));
            // In a fight with somebody else.
            thrax.Controller.CurrentAction = BehaviorManager.BehaviorActionFighting;
            player.GmFlagAlwaysFriendly = true;

            Threat.Tick(harness.Map, thrax, Threat.PulseIntervalMs);

            Assert.IsFalse(thrax.Hate.Contains(player.EntityId), "as its scan would not notice them");
        }

        [TestMethod]
        public void AMissionScenesActorNeitherPulsesNorFadesNorCalls()
        {
            using var harness = WildernessRuntimeTestHarness.Create();
            var player = harness.Client.Player;
            var actor = Put(harness, player.Position + new Vector3(10, 0, 0));
            actor.SpawnPool = new SpawnPool { SceneActorRole = "wave-1", MapContextId = harness.Map.MapInfo.MapContextId };
            var near = Put(harness, actor.Position + new Vector3(0, 0, 20));

            Threat.FromDamage(actor, player, 10);
            Assert.AreNotEqual(BehaviorManager.BehaviorActionFighting, near.Controller.CurrentAction, "no call");

            var held = actor.Hate.Of(player.EntityId);
            Threat.Tick(harness.Map, actor, 60_000);
            Assert.AreEqual(held, actor.Hate.Of(player.EntityId), 1e-9, "its scene holds its table: no pulse, no fade");
        }

        private static void Fade(HateTable table, long ms) =>
            table.Fade(ms, Threat.FadeAfterMs, Threat.FadePerSecond, Threat.NoticedThreat, keep: 0);

        private static Creature Put(WildernessRuntimeTestHarness harness, Vector3 at)
        {
            var creature = CreatureManager.Instance.CreateCreature(ThraxSoldier, null);
            Assert.IsNotNull(creature, "a Thrax Soldier row");
            CreatureManager.Instance.SetLocation(creature, at, 0, harness.Map.MapInfo.MapContextId);
            CellManager.Instance.AddToWorld(harness.Map, creature);
            return creature;
        }
    }
}
