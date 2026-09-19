namespace Rasa.Packets.Trade.Server
{
    using Data;
    using Memory;

    /// <summary>
    /// Recv_TradeEnergyUnitsChange(changeSourceEntityId, newAmount, configurationId)
    /// (client/trade.py:206). Sent to both sides; each adopts the new configuration id, and the
    /// partner's window shows the amount.
    /// </summary>
    public class TradeEnergyUnitsChangePacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.TradeEnergyUnitsChange;

        public ulong SourceEntityId { get; }
        public int Amount { get; }
        public int ConfigurationId { get; }

        public TradeEnergyUnitsChangePacket(ulong sourceEntityId, int amount, int configurationId)
        {
            SourceEntityId = sourceEntityId;
            Amount = amount;
            ConfigurationId = configurationId;
        }

        public override void Write(PythonWriter pw)
        {
            pw.WriteTuple(3);
            pw.WriteULong(SourceEntityId);
            pw.WriteInt(Amount);
            pw.WriteInt(ConfigurationId);
        }
    }
}
