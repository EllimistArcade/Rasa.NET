namespace Rasa.Packets.Trade.Client
{
    using Data;
    using Memory;

    /// <summary>
    /// SendCallActorMethod('RequestConfirmTrade', (g_currentTrade.configurationId,)). The id is
    /// the last one the client saw, so a confirmation of terms that have since changed can be refused.
    /// </summary>
    public class RequestConfirmTradePacket : ClientPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.RequestConfirmTrade;

        public long ConfigurationId { get; set; }

        public override void Read(PythonReader pr)
        {
            pr.ReadTuple();
            ConfigurationId = TradeArgs.ReadInteger(pr);
        }
    }
}
