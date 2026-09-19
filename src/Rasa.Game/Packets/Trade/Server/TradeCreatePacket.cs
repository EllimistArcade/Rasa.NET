namespace Rasa.Packets.Trade.Server
{
    using Data;
    using Memory;

    /// <summary>
    /// Recv_TradeCreate(tradePartnerId, configurationId, bInitiatedTrade) (client/trade.py:119).
    /// Opens the trade window. The partner id must be an entity the client can see - the client
    /// cancels immediately if GetEntity returns nothing.
    /// </summary>
    public class TradeCreatePacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.TradeCreate;

        public ulong TradePartnerEntityId { get; }
        public int ConfigurationId { get; }
        public bool InitiatedTrade { get; }

        public TradeCreatePacket(ulong tradePartnerEntityId, int configurationId, bool initiatedTrade)
        {
            TradePartnerEntityId = tradePartnerEntityId;
            ConfigurationId = configurationId;
            InitiatedTrade = initiatedTrade;
        }

        public override void Write(PythonWriter pw)
        {
            pw.WriteTuple(3);
            pw.WriteULong(TradePartnerEntityId);
            pw.WriteInt(ConfigurationId);
            pw.WriteBool(InitiatedTrade);
        }
    }
}
