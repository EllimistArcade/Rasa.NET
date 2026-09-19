namespace Rasa.Packets.MapChannel.Server
{
    using Data;
    using Memory;

    public class AuctionExpiredPacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.AuctionExpired;

        public ulong ItemId { get; set; }

        public AuctionExpiredPacket(ulong itemId)
        {
            ItemId = itemId;
        }

        public override void Write(PythonWriter pw)
        {
            pw.WriteTuple(1);
            pw.WriteULong(ItemId);
        }
    }
}
