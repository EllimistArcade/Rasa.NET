using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Rasa.Test.World
{
    using Rasa.Data;
    using Rasa.Managers;
    using Rasa.Packets.MapChannel.Server;
    using Rasa.Packets.Protocol;
    using Rasa.Structures;
    using Rasa.Test.Missions;

    /// <summary>
    /// The usables the client's map files place are sent the state the client draws them in as a
    /// player arrives on their map (MapUsables): the Forean fire pits, lit.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class MapUsableTests
    {
        private static MapChannel Channel(uint mapContextId, string name) => new MapChannel
        {
            MapInfo = new MapInfo(mapContextId, name, 1556, 0),
            ClientList = new List<Rasa.Game.Client>(),
            PlayerLimit = 128
        };

        private static List<(ulong EntityId, ForceStatePacket Packet)> States(Rasa.Game.Client client) =>
            WorldTestContext.Drain(client).Select(packet => packet.Message).OfType<CallMethodMessage>()
                .Where(message => message.Packet is ForceStatePacket)
                .Select(message => (message.EntityId, (ForceStatePacket)message.Packet)).ToList();

        private static Rasa.Game.Client On(WorldTestContext world, MapChannel map)
        {
            var client = world.CreateClient();
            world.Map.ClientList.Remove(client);
            client.Player.MapChannel = map;
            client.Player.MapContextId = map.MapInfo.MapContextId;
            map.ClientList.Add(client);
            WorldTestContext.Drain(client);
            return client;
        }

        [TestMethod]
        public void EachFirePitIsLitForWhoeverArrivesOnItsMap()
        {
            using var world = new WorldTestContext();

            foreach (var (map, name, entityId) in new[]
            {
                (MapUsables.ConcordiaDivide, "adv_foreas_concordia_divide", 132770324522631UL),
                (MapUsables.ValverdePlateau, "adv_foreas_valverde_plateau", 134269267723598UL),
                (MapUsables.ConcordiaPalisades, "adv_foreas_concordia_palisades", 133182640922310UL),
            })
            {
                var client = On(world, Channel(map, name));

                Assert.AreEqual(1, MapUsables.PlayerEnteredMap(client), name);

                var sent = States(client).Single();
                Assert.AreEqual(entityId, sent.EntityId, $"{name}: the .map's own id for its fire pit");
                Assert.AreEqual(UseObjectState.TsState0, sent.Packet.State, $"{name}: USE_TS_STATE_0, whose effect is the fire");
                Assert.AreEqual(0, sent.Packet.WindupTimeMs);
            }
        }

        [TestMethod]
        public void AMapWithoutOneIsSentNothingAndACopyOfOneIsSentTheSame()
        {
            using var world = new WorldTestContext();
            var wilderness = On(world, world.Map);

            Assert.AreEqual(0, MapUsables.PlayerEnteredMap(wilderness));
            Assert.IsEmpty(States(wilderness));

            var copy = Channel(MapUsables.ConcordiaDivide, "adv_foreas_concordia_divide");
            copy.IsPrivateInstance = true;
            var visitor = On(world, copy);

            MapUsables.PlayerEnteredMap(visitor);
            Assert.AreEqual(132770324522631UL, States(visitor).Single().EntityId, "a copy loads the same .map");

            Assert.AreEqual(0, MapUsables.PlayerEnteredMap(null));
        }

        [TestMethod]
        public void TheFirePitsAreTheTwoStateClassesWhoseFirstStateIsLit()
        {
            Assert.HasCount(3, MapUsables.All);
            Assert.AreEqual(3, MapUsables.All.Select(usable => usable.EntityId).Distinct().Count());

            foreach (var usable in MapUsables.All)
            {
                CollectionAssert.Contains(new[] { MapUsables.ForeanFirePitV01, MapUsables.ForeanFirePitV03 }, usable.ClassId, usable.ToString());
                Assert.AreEqual(UseObjectState.TsState0, usable.State, usable.ToString());

                // The client makes a .map entity only above 32 bits (gamemap.py _LoadEntities3).
                Assert.IsGreaterThan(uint.MaxValue, usable.EntityId, usable.ToString());
            }
        }

        [TestMethod]
        public void ArrivingOnTheMapSendsThem()
        {
            using var harness = BootcampRuntimeTestHarness.Create();
            var client = harness.Client;
            var map = client.Player.MapChannel;
            var bootcamp = map.MapInfo;

            try
            {
                WorldTestContext.Drain(client);
                ManifestationManager.Instance.AssignPlayer(client);
                Assert.IsEmpty(States(client), "Bootcamp has none");

                map.MapInfo = new MapInfo(MapUsables.ConcordiaPalisades, "adv_foreas_concordia_palisades", bootcamp.MapVersion, bootcamp.BaseRegionId);
                ManifestationManager.Instance.AssignPlayer(client);

                var sent = States(client).Single();
                Assert.AreEqual((133182640922310UL, UseObjectState.TsState0), (sent.EntityId, sent.Packet.State));
            }
            finally
            {
                map.MapInfo = bootcamp;
            }
        }
    }
}
