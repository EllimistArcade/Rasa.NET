using System;
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
    /// What a player's recovery put on quietly, for its announce (QuietLandings): kept for their
    /// client, and shown to it announced when it interrupts the action after it has landed.
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

            GameEffect quiet, loud, hidden, serverOnly;

            using (QuietLandings.Open(player, ActionId.ConsumableAddEffect, 1))
            {
                quiet = Attach(world, player, 901, e => e.AnnounceOnAttach = false);
                loud = Attach(world, player, 902, e => { });
                hidden = Attach(world, player, 903, e => { e.AnnounceOnAttach = false; e.AnnounceToNewcomers = false; });
                serverOnly = Attach(world, player, 904, e => { e.AnnounceOnAttach = false; e.ServerOnly = true; });
            }

            var landed = player.QuietlyLanded;
            Assert.IsNotNull(landed);
            Assert.AreSame(quiet, landed.Effects.Single().Effect, "the one the recovery announces; not one kept quiet for good");

            // Outside a recovery nothing is kept, and a recovery that put on nothing quietly
            // leaves none.
            Attach(world, player, 905, e => e.AnnounceOnAttach = false);
            Assert.AreSame(landed, player.QuietlyLanded);

            using (QuietLandings.Open(player, ActionId.WeaponAttack, 1))
                Attach(world, player, 906, e => { });
            Assert.IsNull(player.QuietlyLanded);
        }

        [TestMethod]
        public void OnlyWhatIsStillOnIsShown()
        {
            using var world = new WorldTestContext();
            var client = Player(world);
            var player = client.Player;

            GameEffect kept, gone;

            using (QuietLandings.Open(player, ActionId.ConsumableAddEffect, 1))
            {
                kept = Attach(world, player, 901, e => e.AnnounceOnAttach = false);
                gone = Attach(world, player, 902, e => e.AnnounceOnAttach = false);
            }

            GameEffectManager.Instance.DettachEffect(world.Map, player, gone);
            WorldTestContext.Drain(client);

            ActorManager.Instance.RequestActionInterrupt(client,
                new RequestActionInterruptPacket { ActionId = ActionId.ConsumableAddEffect, ActionArgId = 1 });

            var sent = WorldTestContext.Drain(client).Select(p => p.Message).OfType<CallMethodMessage>().Select(m => m.Packet).ToList();
            Assert.AreEqual(kept.EffectId, sent.OfType<GameEffectDetachedPacket>().Single().EffectId);
            Assert.AreEqual(kept.EffectId, sent.OfType<GameEffectAttachedPacket>().Single(p => p.Announced).EffectId);
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

        private static Client Player(WorldTestContext world)
        {
            var client = world.CreateClient(0, 0);
            CellManager.Instance.AddToWorld(client);
            WorldTestContext.Drain(client);
            return client;
        }
    }
}
