using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Rasa.Test.Gameplay
{
    using Rasa.Data;
    using Rasa.Game;
    using Rasa.Managers;
    using Rasa.Packets;
    using Rasa.Packets.MapChannel.Server.PerformRecovery;
    using Rasa.Packets.Protocol;
    using Rasa.Structures;
    using Rasa.Test.World;

    /// <summary>
    /// The report: any pistol, clicked once, played its firing animation twice - two rounds out
    /// of the barrel - for one round off the clip and one hit. The shooter's client keeps its own
    /// shot in hand for the attack's windup and recovery, 250 ms for a pistol, or until the round
    /// has arrived, and a recovery that comes after that is a new action it plays again. A pistol
    /// shot held for its flight (ShotFlight) answered after its strike, and one fired by a
    /// client's input waited a whole loop for the next pass before it was answered at all.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class PistolShotTests
    {
        private const uint PistolArg = 133;     // the context's weapon, class 6048: WEAPON_ATTACK 1/133, windup 0, recovery 250

        private static readonly FieldInfo Actions = typeof(AbilityManager).GetField("_actions", BindingFlags.Instance | BindingFlags.NonPublic);

        [TestCleanup]
        public void Cleanup() => ToHit.Roll = Stuns.Roll;

        [TestMethod]
        public void AShotIsHeldOnlyIfItsAnswerComesWhileTheShootersClientHasItInHand()
        {
            Assert.AreEqual(0, ShotFlight.HeldMs(10, 0, 500, 250), "a pistol's 143 ms and the allowance are past its 250");
            Assert.AreEqual(0, ShotFlight.HeldMs(3, 0, 500, 250), "however close");
            Assert.AreEqual(143, ShotFlight.HeldMs(10, 0, 800, 500), "a rifle at 10 m");
            Assert.AreEqual(250, ShotFlight.HeldMs(17.5f, 0, 800, 500), "to 17.5 m");
            Assert.AreEqual(0, ShotFlight.HeldMs(20, 0, 800, 500), "and no further");
            Assert.AreEqual(200, ShotFlight.HeldMs(20, 100, 1500, 1500), "a shotgun");
            Assert.AreEqual(0, ShotFlight.HeldMs(40, 0, 800, 1000), "the refire still a limit");
            Assert.AreEqual(143, ShotFlight.HeldMs(10, 0, 500), "the attack not known: the refire alone, as before");
        }

        [TestMethod]
        public void TheTimeInHandIsTheAttacksWindupAndRecovery()
        {
            using (Level(PistolArg, windupMs: 100, recoveryMs: 250))
                Assert.AreEqual(350L, ShotFlight.InHandMs(ActionId.WeaponAttack, PistolArg));

            Assert.AreEqual(0L, ShotFlight.InHandMs(ActionId.WeaponAttack, 999999), "none known");
        }

        [TestMethod]
        public void InTheClientsDataAPistolHasItsShotInHand250MsAndARifle500()
        {
            using var harness = Rasa.Test.Missions.Wilderness.WildernessRuntimeTestHarness.Create();
            harness.LoadAbilities();

            Assert.AreEqual(250L, ShotFlight.InHandMs(ActionId.WeaponAttack, PistolArg), "a pistol");
            Assert.AreEqual(500L, ShotFlight.InHandMs(ActionId.WeaponAttack, 68), "a rifle");
        }

        [TestMethod]
        public void APistolShotIsNotHeldAndLandsBeforeTheRepliesGoOut()
        {
            using var context = new WeaponAmmoContext();
            using var level = Level(PistolArg, windupMs: 0, recoveryMs: 250);
            var manager = new ManifestationManager(context);
            var map = context.World.Map;
            var creature = ShotFlightTests.Spawn(context.World, new Vector3(0, 0, -10));

            Aim(context, creature);
            Assert.IsTrue(manager.PlayerTryFireWeapon(context.Client));

            var missile = map.QueuedMissiles.Single();
            Assert.AreEqual(0, missile.TriggerTime, "10 m is 143 ms; the 800 ms refire held it");
            Assert.IsFalse(missile.HeldForFlight);
            Sent(context.Client);

            // Between the clients' input and the replies: landed, and its answer in this tick's.
            MissileManager.Instance.ResolveShotsDueNow(map);

            Assert.IsEmpty(map.QueuedMissiles);
            Assert.IsTrue(creature.Attributes[Attributes.Health].Current < 10000);
            var recovery = Sent(context.Client).OfType<WeaponAttackRecovery>().Single();
            Assert.AreEqual(creature.EntityId, recovery.Missile.Args.HitData.Single().EntityId);
        }

        [TestMethod]
        public void ARifleShotCloseInIsStillHeldForItsFlight()
        {
            using var context = new WeaponAmmoContext();
            using var level = Level(PistolArg, windupMs: 0, recoveryMs: 500);
            var manager = new ManifestationManager(context);
            var map = context.World.Map;
            var creature = ShotFlightTests.Spawn(context.World, new Vector3(0, 0, -10));

            Aim(context, creature);
            Assert.IsTrue(manager.PlayerTryFireWeapon(context.Client));

            var missile = map.QueuedMissiles.Single();
            Assert.AreEqual(143, missile.TriggerTime);
            Assert.IsTrue(missile.HeldForFlight);

            // Not landed before its flight is over.
            MissileManager.Instance.ResolveShotsDueNow(map);

            Assert.AreSame(missile, map.QueuedMissiles.Single());
            Assert.AreEqual(10000, creature.Attributes[Attributes.Health].Current);
        }

        [TestMethod]
        public void OnlyAPlayersShotThatLandsAtOnceIsLandedEarly()
        {
            using var context = new WeaponAmmoContext();
            var map = context.World.Map;
            var player = context.Client.Player;
            var creature = ShotFlightTests.Spawn(context.World, new Vector3(0, 0, -10));

            ShotFlightTests.Fit(context);

            // A swing, and a creature's shot: both on the next pass, as before.
            MissileManager.Instance.MissileLaunch(map, new ActionData(player, ActionId.WeaponMelee, 1, creature.EntityId, 0), 10, melee: true);
            MissileManager.Instance.MissileLaunch(map, new ActionData(creature, ActionId.WeaponAttack, 1, player.EntityId, 0), 10);

            var before = map.QueuedMissiles.ToList();
            Assert.HasCount(2, before);

            MissileManager.Instance.ResolveShotsDueNow(map);

            CollectionAssert.AreEquivalent(before, map.QueuedMissiles);
            map.QueuedMissiles.Clear();
        }

        private static void Aim(WeaponAmmoContext context, Creature creature)
        {
            ShotFlightTests.Fit(context);
            context.Client.Player.Rotation = 0;
            context.Client.Player.Target = creature.EntityId;
            ToHit.Roll = _ => false;
        }

        /// <summary>The weapon attack argument's level, as the client data has it, for as long as the test runs.</summary>
        private static IDisposable Level(uint argId, int windupMs, int recoveryMs)
        {
            var actions = (Dictionary<ActionId, ActionInfo>)Actions.GetValue(AbilityManager.Instance);
            var added = !actions.TryGetValue(ActionId.WeaponAttack, out var action);

            if (added)
                actions[ActionId.WeaponAttack] = action = new ActionInfo { ActionId = ActionId.WeaponAttack, Module = "weapons.attack" };

            action.Levels.TryGetValue(argId, out var previous);
            action.Levels[argId] = new ActionLevelInfo { ActionId = ActionId.WeaponAttack, Level = argId, WindupMs = windupMs, RecoveryMs = recoveryMs };

            return new Restore(() =>
            {
                if (added)
                    actions.Remove(ActionId.WeaponAttack);
                else if (previous != null)
                    action.Levels[argId] = previous;
                else
                    action.Levels.Remove(argId);
            });
        }

        private sealed class Restore : IDisposable
        {
            private Action _undo;

            public Restore(Action undo) => _undo = undo;

            public void Dispose()
            {
                _undo?.Invoke();
                _undo = null;
            }
        }

        private static List<PythonPacket> Sent(Client client) =>
            WorldTestContext.Drain(client)
                .Select(packet => packet.Message)
                .OfType<CallMethodMessage>()
                .Select(message => message.Packet)
                .ToList();
    }
}
