namespace Rasa.Packets.Trade.Server
{
    using Data;
    using Memory;

    /// <summary>
    /// Recv_TradeConfirmChange(changeSourceEntityId, confirmValue) (client/trade.py:221). Sent to
    /// both sides; the client routes it to its own or its partner's accept indicator by entity id.
    /// </summary>
    public class TradeConfirmChangePacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.TradeConfirmChange;

        public ulong SourceEntityId { get; }
        public bool Confirmed { get; }

        public TradeConfirmChangePacket(ulong sourceEntityId, bool confirmed)
        {
            SourceEntityId = sourceEntityId;
            Confirmed = confirmed;
        }

        public override void Write(PythonWriter pw)
        {
            pw.WriteTuple(2);
            pw.WriteULong(SourceEntityId);
            pw.WriteBool(Confirmed);
        }
    }
}
