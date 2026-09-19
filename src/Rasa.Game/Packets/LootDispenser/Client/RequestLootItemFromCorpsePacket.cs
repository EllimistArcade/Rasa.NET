namespace Rasa.Packets.LootDispenser.Client
{
    using Data;
    using Memory;

    /// <summary>
    /// Take one item off a corpse. lootdispenser.RequestLootItemFromCorpse sends
    /// (self.entityId, itemId, destSlot), where self.entityId is the dispenser and itemId is the
    /// item's own entity id.
    ///
    /// **destSlot is None more often than not.** Its default is None, and the corpse window's own
    /// right-click path calls RequestLootItemFromCorpse(itemId) with no slot at all - so the
    /// ordinary way of taking an item always sends None. Only dragging a row onto a particular
    /// inventory slot fills it in, and inventory.py leaves it None there too unless both the
    /// category and the slot are known. Reading it as an int would throw, and a throw out of a
    /// packet read closes the connection, so right-clicking loot would have disconnected the
    /// player every time.
    /// </summary>
    public class RequestLootItemFromCorpsePacket : ClientPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.RequestLootItemFromCorpse;

        public ulong EntityId { get; set; }
        public ulong ItemId { get; set; }

        /// <summary>The inventory slot asked for, or null for "wherever it fits".</summary>
        public uint? DestSlot { get; set; }

        public override void Read(PythonReader pr)
        {
            pr.ReadTuple();
            EntityId = pr.ReadULong();
            ItemId = pr.ReadULong();

            if (pr.PeekType() == PythonType.Structs)
            {
                pr.ReadNoneStruct();
                DestSlot = null;
            }
            else
                DestSlot = pr.ReadUInt();
        }
    }
}
