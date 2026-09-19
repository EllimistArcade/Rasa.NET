namespace Rasa.Packets.Trade.Server
{
    using Data;
    using Memory;

    /// <summary>
    /// Recv_TradeDestroy() (client/trade.py:139): resets trade state, hides the window and kills
    /// the 'TradeRequest' indicator. Also the only way to clear a stale invite indicator.
    /// </summary>
    public class TradeDestroyPacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.TradeDestroy;

        public override void Write(PythonWriter pw)
        {
            pw.WriteTuple(0);
        }
    }
}
