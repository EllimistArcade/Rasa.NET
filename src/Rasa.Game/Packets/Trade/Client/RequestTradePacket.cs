namespace Rasa.Packets.Trade.Client
{
    using Data;
    using Memory;

    /// <summary>
    /// client/trade.py OnRequestTrade - SendCallActorMethod('RequestTrade', (playerId,)).
    /// playerId is the target's entity id, taken from the radial menu's context target.
    /// </summary>
    public class RequestTradePacket : ClientPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.RequestTrade;

        public long TargetEntityId { get; set; }

        public override void Read(PythonReader pr)
        {
            pr.ReadTuple();
            TargetEntityId = TradeArgs.ReadInteger(pr);
        }
    }
}
