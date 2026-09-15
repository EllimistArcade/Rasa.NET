namespace Rasa.Packets.MapChannel.Server
{
    using Data;
    using Memory;

    public class AuctionBuyoutFailedPacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.AuctionBuyoutFailed;

        public ulong ItemId { get; set; }
        public PlayerMessage PlayerMessageId { get; set; }

        public AuctionBuyoutFailedPacket(ulong itemId, PlayerMessage playerMessageId)
        {
            ItemId = itemId;
            PlayerMessageId = playerMessageId;
        }

        public override void Write(PythonWriter pw)
        {
            pw.WriteTuple(2);
            pw.WriteULong(ItemId);
            pw.WriteUInt((uint)PlayerMessageId);
        }
    }
}
