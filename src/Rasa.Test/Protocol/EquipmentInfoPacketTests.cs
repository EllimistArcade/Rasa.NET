using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Rasa.Test.Protocol
{
    using Rasa.Data;
    using Rasa.Memory;
    using Rasa.Packets.MapChannel.Server;

    // EquipmentInfo, written as often as the send path writes it. A packet that does not fit in
    // what is left of a send block is written again at the start of the next one
    // (LengthedSocket.TryWriteFrame), so Write must give the same bytes every time. It kept the
    // slots of the first write in the packet, and the second logged each as a duplicate of itself.
    [TestClass]
    public class EquipmentInfoPacketTests
    {
        private const ulong Fists = 90001;

        [TestMethod]
        public void WrittenAgainItIsTheSameBytes()
        {
            var packet = Packet();
            var before = State(packet);

            var first = Bytes(packet);
            CollectionAssert.AreEqual(before, State(packet), "writing it leaves the packet as it was");
            var second = Bytes(packet);

            CollectionAssert.AreEqual(first, second);
            CollectionAssert.AreEqual(first, Bytes(Packet()), "as a packet written once");
        }

        /// <summary>Every field of the packet, a collection by its count.</summary>
        private static List<string> State(EquipmentInfoPacket packet)
        {
            var state = new List<string>();
            for (var type = packet.GetType(); type != null; type = type.BaseType)
                foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    var value = field.GetValue(packet);
                    state.Add($"{type.Name}.{field.Name}={(value is ICollection collection ? $"count {collection.Count}" : value)}");
                }
            return state;
        }

        [TestMethod]
        public void TheFilledSlotsByNumberThenTheFists()
        {
            var expected = Bytes(pw =>
            {
                pw.WriteTuple(1);
                pw.WriteList(4);
                foreach (var (slot, item) in new (uint, ulong)[] { (1, 74889), (2, 80425), (13, 79465), ((uint)EquipmentData.HandToHand, Fists) })
                {
                    pw.WriteTuple(2);
                    pw.WriteUInt(slot);
                    pw.WriteULong(item);
                }
            });

            CollectionAssert.AreEqual(expected, Bytes(Packet()));
        }

        private static EquipmentInfoPacket Packet()
        {
            var slots = new List<ulong>(new ulong[20]);
            slots[1] = 74889;
            slots[2] = 80425;
            slots[13] = 79465;
            return new EquipmentInfoPacket(slots) { HandToHand = Fists };
        }

        private static byte[] Bytes(EquipmentInfoPacket packet) => Bytes(packet.Write);

        private static byte[] Bytes(System.Action<PythonWriter> write)
        {
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
            using (var pw = new PythonWriter(writer))
                write(pw);
            return stream.ToArray();
        }
    }
}
