namespace Rasa.Packets.Trade.Client
{
    using Data;
    using Memory;

    /// <summary>
    /// SendCallActorMethod('RequestAddItemToTrade', (itemEntityId, g_currentTrade.configurationId)).
    /// Item trading is not implemented yet; the request is refused with a message.
    /// </summary>
    public class RequestAddItemToTradePacket : ClientPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.RequestAddItemToTrade;

        public long ItemEntityId { get; set; }
        public long ConfigurationId { get; set; }

        public override void Read(PythonReader pr)
        {
            pr.ReadTuple();
            ItemEntityId = TradeArgs.ReadInteger(pr);
            ConfigurationId = TradeArgs.ReadInteger(pr);
        }
    }
}
