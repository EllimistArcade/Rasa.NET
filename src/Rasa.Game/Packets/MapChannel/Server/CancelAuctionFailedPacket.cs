namespace Rasa.Packets.MapChannel.Server
{
    using Data;
    using Memory;

    public class CancelAuctionFailedPacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.CancelAuctionFailed;

        public ulong ItemId { get; set; }
        public PlayerMessage PlayerMessageId { get; set; }

        public CancelAuctionFailedPacket(ulong itemId, PlayerMessage playerMessageId)
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
