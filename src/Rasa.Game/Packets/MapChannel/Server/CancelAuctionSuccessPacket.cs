namespace Rasa.Packets.MapChannel.Server
{
    using Data;
    using Memory;

    public class CancelAuctionSuccessPacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.CancelAuctionSuccess;

        public ulong ItemId { get; set; }

        public CancelAuctionSuccessPacket(ulong itemId)
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
