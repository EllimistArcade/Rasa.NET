namespace Rasa.Packets.Trade.Server
{
    using Data;
    using Memory;

    /// <summary>
    /// Recv_TradeInvite() (client/trade.py:111): posts TRADE_REQUEST_PENDING, which adds the
    /// 'TradeRequest' status indicator. Clicking it sends RequestAcceptTradeRequest.
    /// </summary>
    public class TradeInvitePacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.TradeInvite;

        public override void Write(PythonWriter pw)
        {
            pw.WriteTuple(0);
        }
    }
}
