using System;
using System.IO;
using System.Text;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Rasa.Test.Protocol
{
    using Rasa.Data;
    using Rasa.Memory;
    using Rasa.Packets;
    using Rasa.Packets.Crafting.Client;
    using Rasa.Packets.Inventory.Client;
    using Rasa.Packets.LootDispenser.Client;
    using Rasa.Packets.Protocol;

    // Packets that name an item by its entity id, read as the client builds them.
    //
    // An id the client kept as the server sent it is a Python long. One it read back off a drag
    // widget (dndWidget.GetData('id')) is a Python int: destroying an item, and dragging one out
    // of a corpse onto the pack, both send that. A reader that took only the long form threw on
    // the int, and a throw out of Read disconnected the player. Each is read here both ways.
    [TestClass]
    public class ItemIdPacketTests
    {
        private const ulong Station = 133079561962676UL;
        private const uint Item = 2050;

        [TestMethod]
        [DataRow(GameOpcode.PersonalInventory_DestroyItem, false, DisplayName = "personal, ints")]
        [DataRow(GameOpcode.PersonalInventory_DestroyItem, true, DisplayName = "personal, longs")]
        [DataRow(GameOpcode.HomeInventory_DestroyItem, false, DisplayName = "home, ints")]
        [DataRow(GameOpcode.HomeInventory_DestroyItem, true, DisplayName = "home, longs")]
        [DataRow(GameOpcode.ClanLockbox_DestroyItem, false, DisplayName = "clan lockbox, ints")]
        [DataRow(GameOpcode.ClanLockbox_DestroyItem, true, DisplayName = "clan lockbox, longs")]
        public void DestroyIsRead(GameOpcode method, bool asLong)
        {
            var (entityId, quantity) = Destroy(method, pw =>
            {
                pw.WriteTuple(2);
                Id(pw, Item, asLong);
                Count(pw, 1, asLong);
            });

            Assert.AreEqual((ulong)Item, entityId);
            Assert.AreEqual(1u, quantity);
        }

        [TestMethod]
        public void DestroyIsReadFromTheBytesTheClientSent()
        {
            // The report's packet: an int16 id (0x1E) and the inline int 1 (0x11).
            var (entityId, quantity) = Destroy(GameOpcode.PersonalInventory_DestroyItem, pw =>
            {
                pw.WriteTuple(2);
                pw.WriteInt(2050);
                pw.WriteInt(1);
            });

            Assert.AreEqual(2050UL, entityId);
            Assert.AreEqual(1u, quantity);
        }

        [TestMethod]
        [DataRow(GameOpcode.PersonalInventory_DestroyItem, DisplayName = "personal")]
        [DataRow(GameOpcode.HomeInventory_DestroyItem, DisplayName = "home")]
        [DataRow(GameOpcode.ClanLockbox_DestroyItem, DisplayName = "clan lockbox")]
        public void ADestroyQuantityBelowOneIsNothing(GameOpcode method)
        {
            // A bare (uint) cast made -1 into 4294967295, which is all of any stack.
            foreach (var sent in new long[] { -1, -100, 0, int.MinValue, long.MinValue })
            {
                var (_, quantity) = Destroy(method, pw =>
                {
                    pw.WriteTuple(2);
                    pw.WriteInt((int)Item);

                    if (sent < int.MinValue)
                        pw.WriteLong(sent);
                    else
                        pw.WriteInt((int)sent);
                });

                Assert.AreEqual(0u, quantity, $"{sent}");
            }
        }

        [TestMethod]
        public void ADestroyQuantityPastAUintIsCapped()
        {
            var (_, quantity) = Destroy(GameOpcode.PersonalInventory_DestroyItem, pw =>
            {
                pw.WriteTuple(2);
                pw.WriteInt((int)Item);
                pw.WriteLong(long.MaxValue);
            });

            Assert.AreEqual(uint.MaxValue, quantity);
        }

        [TestMethod]
        [DataRow(false, -1, DisplayName = "int id, no slot")]
        [DataRow(true, -1, DisplayName = "long id, no slot")]
        [DataRow(false, 5, DisplayName = "int id, dragged to a slot")]
        [DataRow(true, 5, DisplayName = "long id, slot")]
        public void LootFromCorpseIsRead(bool asLong, int slot)
        {
            var packet = Read<RequestLootItemFromCorpsePacket>(GameOpcode.RequestLootItemFromCorpse, pw =>
            {
                pw.WriteTuple(3);
                pw.WriteULong(Station);
                Id(pw, Item, asLong);

                if (slot < 0)
                    pw.WriteNoneStruct();
                else
                    pw.WriteUInt((uint)slot);
            });

            Assert.AreEqual(Station, packet.EntityId);
            Assert.AreEqual((ulong)Item, packet.ItemId);
            Assert.AreEqual(slot < 0 ? (uint?)null : (uint)slot, packet.DestSlot);
        }

        [TestMethod]
        [DataRow(false, DisplayName = "ints")]
        [DataRow(true, DisplayName = "longs")]
        public void CorpseAndLootAllIdsAreRead(bool asLong)
        {
            var looting = Read<RequestCorpseLootingPacket>(GameOpcode.RequestCorpseLooting, pw =>
            {
                pw.WriteTuple(1);
                Id(pw, Item, asLong);
            });

            Assert.AreEqual((ulong)Item, looting.EntityId);

            var cancel = Read<CancelCorpseLootingPacket>(GameOpcode.CancelCorpseLooting, pw =>
            {
                pw.WriteTuple(1);
                Id(pw, Item, asLong);
            });

            Assert.AreEqual((ulong)Item, cancel.EntityId);
        }

        [TestMethod]
        [DataRow(false, DisplayName = "int item ids")]
        [DataRow(true, DisplayName = "long item ids")]
        public void CraftingItemIdsAreRead(bool asLong)
        {
            var salvage = Read<RequestSalvageItemPacket>(GameOpcode.RequestSalvageItem, pw =>
            {
                pw.WriteTuple(3);
                pw.WriteULong(Station);
                Id(pw, Item, asLong);
                pw.WriteUInt(1);
            });

            Assert.AreEqual(Station, salvage.KraftwerksId);
            Assert.AreEqual((ulong)Item, salvage.ItemId);
            Assert.AreEqual(1u, salvage.CraftingAction);

            var upgrade = Read<RequestUpgradeItemPacket>(GameOpcode.RequestUpgradeItem, pw =>
            {
                pw.WriteTuple(3);
                pw.WriteULong(Station);
                Id(pw, Item, asLong);
                pw.WriteUInt(4);
            });

            Assert.AreEqual((ulong)Item, upgrade.ItemId);
            Assert.AreEqual(4u, upgrade.CraftingAction);

            var integrate = Read<RequestIntegrateItemPacket>(GameOpcode.RequestIntegrateItem, pw =>
            {
                pw.WriteTuple(5);
                pw.WriteULong(Station);
                Id(pw, Item, asLong);
                Id(pw, Item + 1, asLong);
                pw.WriteUInt(2);
                pw.WriteUInt(3);
            });

            Assert.AreEqual((ulong)Item, integrate.TargetItemId);
            Assert.AreEqual((ulong)Item + 1, integrate.ModuleItemId);
            Assert.AreEqual(2u, integrate.Slot);
            Assert.AreEqual(3u, integrate.CraftingAction);

            var extract = Read<RequestExtractModulePacket>(GameOpcode.RequestExtractModule, pw =>
            {
                pw.WriteTuple(4);
                pw.WriteULong(Station);
                Id(pw, Item, asLong);
                pw.WriteUInt(1);
                pw.WriteUInt(2);
            });

            Assert.AreEqual((ulong)Item, extract.ItemId);
            Assert.AreEqual(1u, extract.Slot);

            var retrieve = Read<RequestRetrieveFinishedCraftItemPacket>(GameOpcode.RequestRetrieveFinishedCraftItem, pw =>
            {
                pw.WriteTuple(2);
                Id(pw, checked((uint)(Station & 0x7FFFFFFF)), asLong);
                Id(pw, Item, asLong);
            });

            Assert.AreEqual(Station & 0x7FFFFFFF, retrieve.KraftwerksId);
            Assert.AreEqual((ulong)Item, retrieve.ItemId);
        }

        [TestMethod]
        public void AnIdPastAnIntStillComesAsALong()
        {
            var salvage = Read<RequestSalvageItemPacket>(GameOpcode.RequestSalvageItem, pw =>
            {
                pw.WriteTuple(3);
                pw.WriteULong(Station);
                pw.WriteULong(Station + 1);
                pw.WriteUInt(1);
            });

            Assert.AreEqual(Station + 1, salvage.ItemId);
        }

        private static (ulong EntityId, uint Quantity) Destroy(GameOpcode method, Action<PythonWriter> arguments)
        {
            switch (method)
            {
                case GameOpcode.PersonalInventory_DestroyItem:
                    var personal = Read<PersonalInventory_DestroyItemPacket>(method, arguments);
                    return (personal.EntityId, personal.Quantity);

                case GameOpcode.HomeInventory_DestroyItem:
                    var home = Read<HomeInventory_DestroyItemPacket>(method, arguments);
                    return (home.EntityId, home.Quantity);

                case GameOpcode.ClanLockbox_DestroyItem:
                    var clan = Read<ClanLockbox_DestroyItemPacket>(method, arguments);
                    return (clan.EntityId, clan.Quantity);

                default:
                    throw new ArgumentOutOfRangeException(nameof(method));
            }
        }

        private static void Id(PythonWriter pw, uint id, bool asLong)
        {
            if (asLong)
                pw.WriteULong(id);
            else
                pw.WriteUInt(id);
        }

        private static void Count(PythonWriter pw, int count, bool asLong)
        {
            if (asLong)
                pw.WriteLong(count);
            else
                pw.WriteInt(count);
        }

        private static T Read<T>(GameOpcode method, Action<PythonWriter> arguments) where T : PythonPacket
        {
            var message = Message(method, arguments);

            Assert.IsTrue(message.ReadPacket(), $"{method} as the client sends it");

            return (T)message.Packet;
        }

        private static CallServerMethodMessage Message(GameOpcode method, Action<PythonWriter> arguments)
        {
            byte[] payload;

            using (var buffer = new MemoryStream())
            {
                using (var binary = new BinaryWriter(buffer, Encoding.UTF8, leaveOpen: true))
                {
                    binary.Write((byte)0x4F);

                    using (var writer = new PythonWriter(binary))
                        arguments(writer);

                    binary.Write((byte)0x66);
                }

                payload = buffer.ToArray();
            }

            using var stream = new MemoryStream();

            using (var binary = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
            using (var writer = new ProtocolBufferWriter(binary, ProtocolBufferFlags.DontFragment))
            {
                writer.WriteUInt((uint)method);
                writer.WriteArray(payload);
            }

            stream.Position = 0;

            using var input = new BinaryReader(stream);
            using var reader = new ProtocolBufferReader(input, ProtocolBufferFlags.DontFragment);
            var message = new CallServerMethodMessage { Subtype = CallServerMethodSubtype.UserMethodById };

            message.Read(reader);

            return message;
        }
    }
}
