using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Rasa.Test.World
{
    using Rasa.Data;
    using Rasa.Game;
    using Rasa.Managers;
    using Rasa.Packets.MapChannel.Client;
    using Rasa.Packets.MapChannel.Server;
    using Rasa.Packets.Protocol;
    using Rasa.Structures;

    /// <summary>
    /// What a player's recovery did that their client shows as it plays it (QuietLandings): the
    /// effects it put on quietly and what it hit, kept for their client, and announced to it when
    /// it interrupts the action after it has landed.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class QuietLandingsTests
    {
        [TestMethod]
        public void OnlyWhatTheRecoveryIsToAnnounceIsKept()
        {
            using var world = new WorldTestContext();
            var client = Player(world);
            var player = client.Player;

            GameEffect quiet, risen;

            using (QuietLandings.Open(player, ActionId.ConsumableAddEffect, 1))
            {
                quiet = Attach(world, player, 901, e => e.AnnounceOnAttach = false);
                Attach(world, player, 902, e => { });
                Attach(world, player, 903, e => { e.AnnounceOnAttach = false; e.AnnounceToNewcomers = false; });
                Attach(world, player, 904, e => { e.AnnounceOnAttach = false; e.ServerOnly = true; });
                risen = Attach(world, player, 907, e => { e.AnnounceOnAttach = false; e.AnnounceToNewcomers = false; e.AnnounceToCaster = true; });
            }

            var landed = player.QuietlyLanded;
            Assert.IsNotNull(landed);
            CollectionAssert.AreEqual(new[] { quiet, risen }, landed.Effects.Select(entry => entry.Effect).ToList(),
                "the ones the recovery announces; not one kept quiet for good, unless its caster is to see it");

            // Outside a recovery nothing is kept, and a recovery that put on nothing quietly
            // leaves none.
            Attach(world, player, 905, e => e.AnnounceOnAttach = false);
            Assert.AreSame(landed, player.QuietlyLanded);

            using (QuietLandings.Open(player, ActionId.WeaponAttack, 1))
                Attach(world, player, 906, e => { });
            Assert.IsNull(player.QuietlyLanded);
        }

        [TestMethod]
        public void OnlyWhatIsStillOnIsAnnouncedOneHitForEachHolder()
        {
            using var world = new WorldTestContext();
            var client = Player(world);
            var player = client.Player;

            GameEffect kept, alsoKept, gone;

            using (QuietLandings.Open(player, ActionId.ConsumableAddEffect, 1))
            {
                kept = Attach(world, player, 901, e => e.AnnounceOnAttach = false);
                alsoKept = Attach(world, player, 902, e => e.AnnounceOnAttach = false);
                gone = Attach(world, player, 903, e => e.AnnounceOnAttach = false);
            }

            GameEffectManager.Instance.DettachEffect(world.Map, player, gone);
            WorldTestContext.Drain(client);

            ActorManager.Instance.RequestActionInterrupt(client,
                new RequestActionInterruptPacket { ActionId = ActionId.ConsumableAddEffect, ActionArgId = 1 });

            var sent = WorldTestContext.Drain(client).Select(p => p.Message).OfType<CallMethodMessage>().ToList();
            var hit = sent.Single();
            Assert.AreEqual(player.EntityId, hit.EntityId, "on the holder");
            var announce = (AnnounceMapDamagePacket)hit.Packet;
            CollectionAssert.AreEqual(new uint[] { 901, 902 }, announce.TargetEffectIds.ToList());
        }

        [TestMethod]
        public void WhatTheAbilityHitIsAnnouncedToItsCasterForItsBars()
        {
            using var world = new WorldTestContext();
            var client = Player(world);
            var player = client.Player;
            var struck = Spawn(world);
            var gone = Spawn(world);
            var other = Spawn(world);

            using (QuietLandings.Open(player, ActionId.AaSoldierShrapnel, 1))
            {
                ActorManager.Instance.Damage(world.Map, struck, 50, player, out _);
                ActorManager.Instance.Damage(world.Map, struck, 50, player, out _);
                ActorManager.Instance.Damage(world.Map, gone, 50, player, out _);
                ActorManager.Instance.Damage(world.Map, other, 50, struck, out _);
            }

            var landed = player.QuietlyLanded;
            CollectionAssert.AreEqual(new[] { struck, gone }, landed.Hit.ToList(), "the player's hits, each once; not another's");
            Assert.IsEmpty(landed.Effects.ToList());

            // One left the world since: nothing to announce on.
            Unregister(gone);
            WorldTestContext.Drain(client);
            ActorManager.Instance.RequestActionInterrupt(client,
                new RequestActionInterruptPacket { ActionId = ActionId.AaSoldierShrapnel, ActionArgId = 1 });

            var hit = WorldTestContext.Drain(client).Select(p => p.Message).OfType<CallMethodMessage>().Single();
            Assert.AreEqual(struck.EntityId, hit.EntityId);
            var announce = (AnnounceMapDamagePacket)hit.Packet;
            Assert.AreEqual(0, announce.Amount, "nothing more dealt: its bars announced");
            Assert.IsEmpty(announce.TargetEffectIds.ToList());

            Unregister(struck);
            Unregister(other);
        }

        [TestMethod]
        public void TheHitDoesNothingButNameTheEffects()
        {
            // Actor.Recv_AnnounceMapDamage(rawInfo): the 12-tuple, nothing dealt, nothing
            // absorbed or resisted - so no number, no log line, no reaction - and the types last
            // but one, which AnnounceDamage announces on the actor.
            var raw = (List<object>)HitResultTests.Decode(new AnnounceMapDamagePacket(0, targetEffectIds: new uint[] { 10000053 }))[0];

            Assert.HasCount(12, raw);
            Assert.AreEqual(0L, raw[3], "absorbed");
            Assert.AreEqual(0L, raw[4], "resisted");
            Assert.AreEqual(0L, raw[5], "finalAmt");
            Assert.AreEqual(0L, raw[7], "deathBlow");
            Assert.AreEqual(0L, raw[9], "wasImmune");
            CollectionAssert.AreEqual(new object[] { 10000053L }, (List<object>)raw[10]);
            Assert.IsEmpty((List<object>)raw[11]);
        }

        private static GameEffect Attach(WorldTestContext world, Manifestation player, int typeId, Action<GameEffect> shape)
        {
            var effect = new GameEffect
            {
                TypeId = typeId,
                EffectId = GameEffectManager.Instance.NextEffectId(world.Map),
                SourceId = player.EntityId,
                Source = player,
                IsBuff = true,
                ExpiresTick = Environment.TickCount64 + 60_000
            };
            shape(effect);
            GameEffectManager.Instance.Attach(world.Map, player, effect);
            return effect;
        }

        /// <summary>A hostile creature with 1000 health and no armour, in the map's cells.</summary>
        private static Creature Spawn(WorldTestContext world)
        {
            var map = world.Map;
            var creature = new Creature
            {
                Name = "Fixture",
                TargetCategory = TargetCategory.Hostile,
                MapContextId = map.MapInfo.MapContextId,
                RuntimeMapChannel = map,
                Position = new System.Numerics.Vector3(5, 0, 5),
                EntityClass = 0,
                State = CharacterState.Idle,
                Level = 1,
                AppearanceData = new Dictionary<EquipmentData, AppearanceData>()
            };
            creature.Attributes[Attributes.Health] = new ActorAttributes(Attributes.Health, 1000, 1000, 1000, 0, 0);
            creature.Attributes[Attributes.Armor] = new ActorAttributes(Attributes.Armor, 0, 0, 0, 0, 0);
            EntityManager.Instance.RegisterEntity(creature.EntityId, EntityType.Creature);
            EntityManager.Instance.RegisterCreature(creature);

            var seed = CellManager.Instance.GetCellSeed(creature.Position);
            creature.Cells = CellManager.Instance.CreateCellMatrix(map, seed & 0xFFFF, seed >> 16);
            CellManager.Instance.GetCell(map, seed & 0xFFFF, seed >> 16).CreatureList.Add(creature);

            return creature;
        }

        private static void Unregister(Creature creature)
        {
            EntityManager.Instance.UnregisterCreature(creature.EntityId);
            EntityManager.Instance.UnregisterEntity(creature.EntityId);
        }

        private static Client Player(WorldTestContext world)
        {
            var client = world.CreateClient(0, 0);
            CellManager.Instance.AddToWorld(client);
            WorldTestContext.Drain(client);
            return client;
        }
    }
}
