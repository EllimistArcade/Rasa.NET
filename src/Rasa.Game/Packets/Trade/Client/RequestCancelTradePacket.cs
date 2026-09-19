namespace Rasa.Packets.Trade.Client
{
    using Data;
    using Memory;

    /// <summary>
    /// SendCallActorMethod('RequestCancelTrade', ()). Revoking an invite, closing the trade
    /// window, or the client's own OkToContinueTrading check failing (death, range).
    /// </summary>
    public class RequestCancelTradePacket : ClientPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.RequestCancelTrade;

        public override void Read(PythonReader pr)
        {
            pr.ReadTuple();
        }
    }
}
