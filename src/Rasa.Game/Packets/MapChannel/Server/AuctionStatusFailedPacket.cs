namespace Rasa.Packets.MapChannel.Server
{
    using Data;
    using Memory;

    /// <summary>
    /// The client's Recv_AuctionStatusFailed is an empty function, so this reaches nobody. It
    /// exists so a failing status request is still answered rather than left hanging.
    /// </summary>
    public class AuctionStatusFailedPacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.AuctionStatusFailed;

        public PlayerMessage PlayerMessageId { get; set; }

        public AuctionStatusFailedPacket(PlayerMessage playerMessageId)
        {
            PlayerMessageId = playerMessageId;
        }

        public override void Write(PythonWriter pw)
        {
            pw.WriteTuple(1);
            pw.WriteUInt((uint)PlayerMessageId);
        }
    }
}
