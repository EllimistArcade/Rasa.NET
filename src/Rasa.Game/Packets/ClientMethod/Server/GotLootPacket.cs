using System.Collections.Generic;

namespace Rasa.Packets.ClientMethod.Server
{
    using Data;
    using Memory;
    using Structures;

    /// <summary>
    /// clientmethod.Recv_GotLoot(creatureEntityId, entityClassIdList, moneyAmount): what one take
    /// from a corpse gave this player. Each (classId, quantity, itemId) is a "You received N X."
    /// in the loot filter and a pick-up on the status updater, the credits are "You received N
    /// credits.", and the pick-up sound is played for the best of them. It says only what this
    /// player was given by this take: the items they took, and their share of the credits.
    /// </summary>
    public class GotLootPacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.GotLoot;

        public ulong CreatureEntityId { get; }
        public List<LootItem> Items { get; }
        public int Credits { get; }

        public GotLootPacket(ulong creatureEntityId, List<LootItem> items, int credits)
        {
            CreatureEntityId = creatureEntityId;
            Items = items ?? new List<LootItem>();
            Credits = credits;
        }

        public override void Write(PythonWriter pw)
        {
            pw.WriteTuple(3);
            pw.WriteULong(CreatureEntityId);
            pw.WriteList(Items.Count);
            foreach (var item in Items)
            {
                pw.WriteTuple(3);
                pw.WriteUInt(item.ItemClassId);
                pw.WriteUInt(item.ItemQuantity);
                pw.WriteULong(item.EntityId);
            }
            pw.WriteInt(Credits);
        }
    }
}
