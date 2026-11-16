using System.Collections.Generic;

namespace Rasa.Packets.MapChannel.Server
{
    using Data;
    using Memory;

    public class EquipmentInfoPacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.EquipmentInfo;

        public List<ulong> EquipmentInfo { get; set; }

        /// <summary>
        /// The player's fists, in the HANDTOHAND slot (10000001), which the slot list cannot hold:
        /// only in what their own client is sent (Managers.Unarmed); 0 for none.
        /// </summary>
        public ulong HandToHand { get; set; }

        public EquipmentInfoPacket(List<ulong> equipmentInfo)
        {
            EquipmentInfo = equipmentInfo;
        }

        /// <summary>
        /// The equipped slots that hold something, by slot, then the fists. Built afresh on every
        /// write: a packet is written again when it does not fit in what is left of a send block
        /// (LengthedSocket.TryWriteFrame), and the slots kept from the first write were reported as
        /// duplicates of themselves on the second ("duplicate equipment slot", with the same item).
        /// </summary>
        public override void Write(PythonWriter pw)
        {
            var equipment = new List<(uint Slot, ulong Item)>();
            uint slot = 0;

            foreach (var entry in EquipmentInfo)
            {
                if (entry != 0)
                    equipment.Add((slot, entry));

                slot++;
            }

            if (HandToHand != 0)
                equipment.Add(((uint)EquipmentData.HandToHand, HandToHand));

            pw.WriteTuple(1);
            pw.WriteList(equipment.Count);

            foreach (var (equipped, item) in equipment)
            {
                pw.WriteTuple(2);
                pw.WriteUInt(equipped);
                pw.WriteULong(item);
            }
        }
    }
}
