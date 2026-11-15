using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Rasa.Test.World
{
    using Rasa.Data;
    using Rasa.Game;
    using Rasa.Managers;
    using Rasa.Packets;
    using Rasa.Packets.MapChannel.Server;
    using Rasa.Packets.Protocol;
    using Rasa.Structures;

    /// <summary>
    /// A mission's corpse, bin or crate searched (MissionObjects.OpenFor): it opens for the searcher by Use, so their
    /// client plays its opening, and stays open to them alone.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class SearchedPropTests
    {
        private const EntityClasses Corpse = (EntityClasses)7928;     // UsableTreasureDispBrannCorpseMaleV01

        [TestMethod]
        public void ASearchedCorpseOpensForTheSearcherByUseAndForNobodyElse()
        {
            using var world = new WorldTestContext();
            world.AddClass(Corpse);
            var searcher = world.CreateClient();
            var other = world.CreateClient();
            var corpse = new DynamicObject { EntityClassId = Corpse, StateId = UseObjectState.TdStateClosed };

            MissionObjects.OpenFor(searcher, corpse);

            var use = Told(searcher, corpse).OfType<UsePacket>().Single();
            Assert.AreEqual(UseObjectState.TdStateOpened, use.CurState);
            Assert.AreEqual(searcher.Player.EntityId, use.PlayerEntityId);
            Assert.IsEmpty(Told(other, corpse), "nobody else is told");

            Assert.AreEqual(UseObjectState.TdStateClosed, corpse.StateId, "closed to everyone else");
            Assert.AreEqual(UseObjectState.TdStateOpened, MissionObjects.StateFor(searcher, corpse));
            Assert.AreEqual(UseObjectState.TdStateClosed, MissionObjects.StateFor(other, corpse));
            Assert.AreEqual(UseObjectState.TdStateOpened, MissionObjects.InfoFor(searcher, corpse, true, 0).CurState, "and so it is shown to them");
            Assert.AreEqual(UseObjectState.TdStateClosed, MissionObjects.InfoFor(other, corpse, true, 0).CurState);

            // Searched again: open already, nothing more.
            MissionObjects.OpenFor(searcher, corpse);
            Assert.IsEmpty(Told(searcher, corpse));
        }

        [TestMethod]
        public void OnlyAClosedDispenserOpens()
        {
            using var world = new WorldTestContext();
            world.AddClass(Corpse);
            var searcher = world.CreateClient();
            var opened = new DynamicObject { EntityClassId = Corpse, StateId = UseObjectState.TdStateOpened };
            var switched = new DynamicObject { EntityClassId = Corpse, StateId = UseObjectState.TsState0 };

            MissionObjects.OpenFor(searcher, opened);
            MissionObjects.OpenFor(searcher, switched);

            Assert.IsEmpty(Told(searcher, opened));
            Assert.IsEmpty(Told(searcher, switched));
            Assert.AreEqual(UseObjectState.TsState0, MissionObjects.StateFor(searcher, switched));
        }

        private static List<PythonPacket> Told(Client client, DynamicObject obj) =>
            WorldTestContext.Drain(client).Select(packet => packet.Message).OfType<CallMethodMessage>()
                .Where(message => message.EntityId == obj.EntityId).Select(message => message.Packet).ToList();
    }
}
