namespace Rasa.Packets.Trade.Server
{
    using Data;
    using Memory;

    /// <summary>
    /// Recv_TradeCompleted() (client/trade.py:147): plays the completion effect and clears state.
    /// </summary>
    public class TradeCompletedPacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.TradeCompleted;

        public override void Write(PythonWriter pw)
        {
            pw.WriteTuple(0);
        }
    }
}
