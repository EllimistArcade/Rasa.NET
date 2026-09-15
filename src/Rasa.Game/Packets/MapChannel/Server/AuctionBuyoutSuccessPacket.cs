namespace Rasa.Packets.MapChannel.Server
{
    using Data;
    using Memory;

    public class AuctionBuyoutSuccessPacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.AuctionBuyoutSuccess;

        public ulong ItemId { get; set; }

        public AuctionBuyoutSuccessPacket(ulong itemId)
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
