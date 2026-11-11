using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Rasa.Test.World
{
    using Rasa.Data;
    using Rasa.Managers;
    using Rasa.Packets.MapChannel.Server;
    using Rasa.Packets.Protocol;
    using Rasa.Services.Preloader;
    using Rasa.Structures;
    using Rasa.Test.Missions.Wilderness;

    /// <summary>
    /// The report: on the Valverde Plateau the Stalkers and the Juggernaut only ever used their
    /// guns. A Stalker should charge its egg and drop it on whoever is around it, some should
    /// come down from orbit, and a Juggernaut has its mortars and a vortex that pulls players in
    /// (Stalker_and_juggernaut_kits, CreatureBombs.EggReach, SpawnPoolManager.AnimFromOrbit,
    /// CreatureAreaAttacks.JuggernautMortars, JuggernautBlackhole).
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class StalkerAndJuggernautTests
    {
        private const uint PlateauStalker = 750021;
        private const uint StalkerClass = 3781;     // Vehicle_Bane_Stalker

        [TestMethod]
        public void AStalkerLaysItsEggOnAPlayerShootingItFromForty()
        {
            using var harness = WildernessRuntimeTestHarness.Create();
            harness.LoadAbilities();
            var player = Sturdy(harness);

            var stalker = harness.Creatures.CreateCreature(PlateauStalker, null);
            var ovulate = stalker.Actions.Single(CreatureBombs.IsOvulate);
            Assert.AreEqual(60f, ovulate.RangeMax, "the charge is used as far as the egg reaches");

            var windups = Fight(harness, stalker, player, 40f, steps: 120);

            CollectionAssert.Contains(windups, ActionId.CrStalkerOvulate, string.Join(", ", windups));
            Assert.IsTrue(CreatureBombs.IsCharging(stalker));

            // The charge done, the egg drops: its windup, the Stalker standing.
            var charge = stalker.ActiveEffects.Values.Single(e => e.TypeId == CreatureBombs.StalkerEggChargeTypeId);
            charge.ExpiresTick = Environment.TickCount64 - 1;
            GameEffectManager.Instance.DoWork(harness.Map, 100);

            CollectionAssert.Contains(Windups(harness, stalker), ActionId.CrStalkerEggDrop);
            Assert.IsTrue(CreatureBombs.IsSelfDestructing(stalker), "dropping its egg, it does nothing else");
        }

        [TestMethod]
        public void TheEggReachesSixtyAndTheChargeAloneTwenty()
        {
            using var harness = WildernessRuntimeTestHarness.Create();
            harness.LoadAbilities();

            Assert.IsTrue(AbilityManager.Instance.TryGetLevel(ActionId.CrStalkerOvulate, 1, out var charge));
            Assert.IsTrue(AbilityManager.Instance.TryGetLevel(ActionId.CrStalkerEggDrop, 1, out var drop));

            Assert.AreEqual(60f, CreatureBombs.EggReach(charge, drop));
            Assert.AreEqual(20f, CreatureBombs.EggReach(charge, null));
        }

        [TestMethod]
        public void TwoOfThePlateausStalkerPoolsComeDownFromOrbit()
        {
            using var harness = WildernessRuntimeTestHarness.Create(mapContextId: StalkerAndJuggernautKits.Plateau);
            harness.LoadAbilities();
            CellManager.Instance.AddToWorld(harness.Client);

            // Where the first of them comes down.
            var orbital = harness.Map.SpawnPools.Single(p => p.DbId == 750022);
            harness.MoveTo(orbital.Position);
            WorldTestContext.Drain(harness.Client);

            foreach (var pool in new uint[] { 750021, 750022, 750023, 750024 })
                Assert.AreEqual(StalkerAndJuggernautKits.OrbitalPools.Contains(pool) ? SpawnPoolManager.AnimFromOrbit : SpawnPoolManager.AnimNone,
                    harness.Map.SpawnPools.Single(p => p.DbId == pool).AnimType, $"pool {pool}");

            harness.SpawnWorld(750021, 750022);

            var packets = WorldTestContext.Drain(harness.Client).Select(p => p.Message).OfType<CallMethodMessage>().ToList();
            var landed = Living(harness, 750022);
            var standing = Living(harness, 750021);

            Assert.HasCount(2, landed);
            Assert.HasCount(2, standing);

            foreach (var stalker in landed)
            {
                Assert.IsTrue(packets.Any(m => m.EntityId == stalker.EntityId && m.Packet is PerformWindupPacket w
                    && w.ActionId == CreatureSummons.BirthAction && w.ActionArgId == StalkerClass), "its birth, the Stalker's own");
                Assert.IsTrue(packets.Any(m => m.EntityId == stalker.EntityId && m.Packet is PerformRecoveryPacket r
                    && r.ActionId == CreatureSummons.BirthAction));
                Assert.IsTrue(CreatureSummons.IsBusy(stalker), "down, it does nothing else until it is up");
            }

            foreach (var stalker in standing)
            {
                Assert.IsFalse(packets.Any(m => m.EntityId == stalker.EntityId && m.Packet is PerformWindupPacket w && w.ActionId == CreatureSummons.BirthAction));
                Assert.IsFalse(CreatureSummons.IsBusy(stalker));
            }
        }

        [TestMethod]
        public void TheJuggernautsHaveTheirMortarsAndTheirBlackhole()
        {
            using var harness = WildernessRuntimeTestHarness.Create();

            var prototype = harness.Creatures.CreateCreature(StalkerAndJuggernautKits.PrototypeJuggernaut, null);
            CollectionAssert.AreEquivalent(new[] { (ActionId.WeaponAttack, 180u), (ActionId.CrJuggernautMortars, 2u), (ActionId.CrJuggernautBlackhole, 1u) },
                prototype.Actions.Select(a => (a.ActionId, a.ActionArgId)).ToList());

            var den = harness.Creatures.CreateCreature(StalkerAndJuggernautKits.DevilsDenJuggernaut, null);
            CollectionAssert.AreEquivalent(new[] { (ActionId.WeaponAttack, 180u), (ActionId.CrJuggernautGuns, 1u), (ActionId.CrJuggernautMortars, 1u), (ActionId.CrJuggernautBlackhole, 1u) },
                den.Actions.Select(a => (a.ActionId, a.ActionArgId)).ToList());
        }

        [TestMethod]
        public void InAFightTheJuggernautFiresItsMortarsOnEveryoneAroundItsTargetAndOpensItsBlackhole()
        {
            using var harness = WildernessRuntimeTestHarness.Create();
            harness.LoadAbilities();
            var player = Sturdy(harness);

            var area = CreatureAreaAttacks.AreaOf(ActionId.CrJuggernautMortars, 2);
            Assert.AreEqual(CreatureAreaAttacks.MortarRadius, area.AroundTarget, "the shells come down round its target");

            var juggernaut = harness.Creatures.CreateCreature(StalkerAndJuggernautKits.PrototypeJuggernaut, null);
            var windups = Fight(harness, juggernaut, player, 20f, steps: 200);

            CollectionAssert.Contains(windups, ActionId.CrJuggernautMortars, string.Join(", ", windups));
            CollectionAssert.Contains(windups, ActionId.CrJuggernautBlackhole, string.Join(", ", windups));
        }

        [TestMethod]
        public void TheBlackholePullsAPlayerInBurnsThemAndClosesWhenItsTimeIsUp()
        {
            using var harness = WildernessRuntimeTestHarness.Create();
            harness.LoadAbilities();
            var player = Sturdy(harness);
            var juggernaut = Placed(harness, StalkerAndJuggernautKits.PrototypeJuggernaut, player.Position + new Vector3(30, 0, 0));
            var action = juggernaut.Actions.Single(JuggernautBlackhole.Is);

            var hole = JuggernautBlackhole.Open(harness.Map, juggernaut, action, player);
            Assert.IsNotNull(hole);

            try
            {
                Assert.AreEqual(JuggernautBlackhole.BlackholeClass, hole.EntityClass);
                Assert.AreSame(hole, JuggernautBlackhole.OpenBy(juggernaut));
                Assert.IsNull(JuggernautBlackhole.Open(harness.Map, juggernaut, action, player), "one at a time");

                // Ten metres off it, inside its fifteen: dragged in and burning.
                harness.MoveTo(NavMeshManager.SnapToGround(harness.Map, hole.Position + new Vector3(0, 0, 10)));
                var before = Vector3.Distance(player.Position, hole.Position);
                var now = Environment.TickCount64;

                JuggernautBlackhole.Worker(harness.Map, now);

                Assert.IsTrue(Vector3.Distance(player.Position, hole.Position) < before - 5f, $"pulled in from {before}");
                var burn = player.ActiveEffects.Values.Single(e => e.TypeId == JuggernautBlackhole.WellTargetTypeId);
                Assert.AreEqual(hole.EntityId, burn.SourceId, "its FX drawn from the blackhole");
                Assert.AreSame(juggernaut, burn.Source, "the burn the Juggernaut's");
                Assert.AreEqual(((int)action.MinDamage, (int)action.MaxDamage), (burn.TickDamageMin, burn.TickDamageMax));
                Assert.AreEqual(DamageType.Laser, burn.TickDamageType);

                // Its six seconds up: the burn off, the blackhole gone.
                JuggernautBlackhole.Worker(harness.Map, now + 6001);

                Assert.IsFalse(player.ActiveEffects.ContainsKey(burn.EffectId));
                Assert.IsNull(JuggernautBlackhole.OpenBy(juggernaut));
                Assert.AreEqual(CharacterState.Dead, hole.State);
            }
            finally
            {
                JuggernautBlackhole.Killed(harness.Map, hole);
            }
        }

        [TestMethod]
        public void ABlackholeShotDownClosesAndIsNobodysKill()
        {
            using var harness = WildernessRuntimeTestHarness.Create();
            harness.LoadAbilities();
            var player = Sturdy(harness);
            var juggernaut = Placed(harness, StalkerAndJuggernautKits.PrototypeJuggernaut, player.Position + new Vector3(30, 0, 0));

            var hole = JuggernautBlackhole.Open(harness.Map, juggernaut, juggernaut.Actions.Single(JuggernautBlackhole.Is), player);
            JuggernautBlackhole.Worker(harness.Map, Environment.TickCount64);
            Assert.IsTrue(player.ActiveEffects.Values.Any(e => e.TypeId == JuggernautBlackhole.WellTargetTypeId));

            hole.Attributes[Attributes.Health].Current = 0;
            harness.Creatures.HandleCreatureKill(harness.Map, hole, player);

            Assert.IsNull(JuggernautBlackhole.OpenBy(juggernaut));
            Assert.IsFalse(JuggernautBlackhole.Is(hole));
            Assert.IsFalse(player.ActiveEffects.Values.Any(e => e.TypeId == JuggernautBlackhole.WellTargetTypeId), "everyone let go");
            Assert.IsFalse(harness.Map.MapCellInfo.Cells.Values.Any(c => c.CreatureList.Contains(hole)), "and taken out of the world");
        }

        /// <summary>The harness's player, in the world and with health to take a fight.</summary>
        private static Manifestation Sturdy(WildernessRuntimeTestHarness harness)
        {
            var player = harness.Client.Player;
            player.Attributes[Attributes.Health] = new ActorAttributes(Attributes.Health, 10000000, 10000000, 10000000, 0, 0);
            CellManager.Instance.AddToWorld(harness.Client);
            return player;
        }

        private static Creature Placed(WildernessRuntimeTestHarness harness, uint creatureId, Vector3 at)
        {
            var creature = harness.Creatures.CreateCreature(creatureId, null);
            harness.Creatures.SetLocation(creature, at, 0, harness.Client.Player.MapContextId);
            CellManager.Instance.AddToWorld(harness.Map, creature);
            return creature;
        }

        /// <summary>The creature set fighting the player from this far, and what it wound up over these thinks.</summary>
        private static List<ActionId> Fight(WildernessRuntimeTestHarness harness, Creature creature, Manifestation player, float distance, int steps)
        {
            harness.Creatures.SetLocation(creature, player.Position + new Vector3(distance, 0, 0), 0, player.MapContextId);
            CellManager.Instance.AddToWorld(harness.Map, creature);
            Assert.IsTrue(BehaviorManager.Instance.TrySetActionFighting(creature, player.EntityId));
            creature.Controller.ActionFighting.Opened = true;

            var windups = new List<ActionId>();

            try
            {
                for (var step = 0; step < steps; step++)
                {
                    BehaviorManager.Instance.MapChannelThink(harness.Map, 100);
                    MissileManager.Instance.DoWork(harness.Map, 100);
                    windups.AddRange(Windups(harness, creature));
                }
            }
            finally
            {
                harness.Map.QueuedMissiles.Clear();
            }

            return windups;
        }

        private static List<ActionId> Windups(WildernessRuntimeTestHarness harness, Creature creature) =>
            WorldTestContext.Drain(harness.Client).Select(p => p.Message).OfType<CallMethodMessage>()
                .Where(m => m.EntityId == creature.EntityId).Select(m => m.Packet).OfType<PerformWindupPacket>()
                .Select(w => w.ActionId).ToList();

        private static Creature[] Living(WildernessRuntimeTestHarness harness, uint pool) =>
            harness.Map.MapCellInfo.Cells.Values.SelectMany(cell => cell.CreatureList).Distinct()
                .Where(creature => creature.SpawnPool?.DbId == pool && CreatureManager.IsLivingOnMap(harness.Map, creature)).ToArray();
    }
}
