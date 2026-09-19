namespace Rasa.Packets.Trade.Client
{
    using Data;
    using Memory;

    /// <summary>
    /// SendCallActorMethod('RequestRemoveItemFromTrade', (itemEntityId,)). Accepted and ignored
    /// until item trading exists, since no item can be in a trade yet.
    /// </summary>
    public class RequestRemoveItemFromTradePacket : ClientPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.RequestRemoveItemFromTrade;

        public long ItemEntityId { get; set; }

        public override void Read(PythonReader pr)
        {
            pr.ReadTuple();
            ItemEntityId = TradeArgs.ReadInteger(pr);
        }
    }
}
