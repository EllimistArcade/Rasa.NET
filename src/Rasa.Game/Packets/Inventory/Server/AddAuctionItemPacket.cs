namespace Rasa.Packets.Inventory.Server
{
    using Data;
    using Memory;

    /// <summary>
    /// Puts one item into the client's auction inventory with no auction data attached, so the
    /// item counts towards MAX_AUCTION_ITEMS immediately after it is listed. The figures arrive
    /// with the next AuctionStatusSuccess.
    /// </summary>
    public class AddAuctionItemPacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.AddAuctionItem;

        public ulong EntityId { get; set; }

        public AddAuctionItemPacket(ulong entityId)
        {
            EntityId = entityId;
        }

        public override void Write(PythonWriter pw)
        {
            pw.WriteTuple(1);
            pw.WriteULong(EntityId);
        }
    }
}
