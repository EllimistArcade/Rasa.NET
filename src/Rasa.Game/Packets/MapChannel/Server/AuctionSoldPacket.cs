namespace Rasa.Packets.MapChannel.Server
{
    using Data;
    using Memory;

    /// <summary>
    /// Tells a seller who is online that one of their auctions sold, and for how much. The
    /// credits have already been added to their character row either way.
    /// </summary>
    public class AuctionSoldPacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.AuctionSold;

        public ulong ItemId { get; set; }
        public uint Price { get; set; }

        public AuctionSoldPacket(ulong itemId, uint price)
        {
            ItemId = itemId;
            Price = price;
        }

        public override void Write(PythonWriter pw)
        {
            pw.WriteTuple(2);
            pw.WriteULong(ItemId);
            pw.WriteUInt(Price);
        }
    }
}
