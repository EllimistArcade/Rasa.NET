namespace Rasa.Packets.Trade.Server
{
    using Data;
    using Memory;

    /// <summary>
    /// client/trade.py Recv_TradeUpdateItem(itemSourceEntityId, itemEntityId, configurationId).
    ///
    /// itemSourceEntityId is the *manifestation* entity of whoever offered the item.
    ///
    /// The client marks this one DEPRECATED and its handler posts no UI event at all - it only
    /// refreshes a local stack-count cache. Nothing here sends it: an offer is a whole stack, so
    /// there is no count to update, and a client that draws nothing from it cannot be tested
    /// against. It exists so the opcode is not an unhandled one if a future client sends it.
    /// </summary>
    public class TradeUpdateItemPacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.TradeUpdateItem;

        public ulong ItemSourceEntityId { get; set; }
        public ulong ItemEntityId { get; set; }
        public int ConfigurationId { get; set; }

        public TradeUpdateItemPacket(ulong itemSourceEntityId, ulong itemEntityId, int configurationId)
        {
            ItemSourceEntityId = itemSourceEntityId;
            ItemEntityId = itemEntityId;
            ConfigurationId = configurationId;
        }

        public override void Write(PythonWriter pw)
        {
            pw.WriteTuple(3);
            pw.WriteULong(ItemSourceEntityId);
            pw.WriteULong(ItemEntityId);
            pw.WriteInt(ConfigurationId);
        }
    }
}
