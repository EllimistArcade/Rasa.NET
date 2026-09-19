namespace Rasa.Packets.Trade.Server
{
    using Data;
    using Memory;

    /// <summary>
    /// client/trade.py Recv_TradeRemoveItem(itemSourceEntityId, itemEntityId, configurationId).
    ///
    /// itemSourceEntityId is the *manifestation* entity of whoever offered the item: the client
    /// compares it with its own to decide whether the item belongs in its half of the window or
    /// the partner's, so it must be the player's entity id and not the item's owner id.
    /// </summary>
    public class TradeRemoveItemPacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.TradeRemoveItem;

        public ulong ItemSourceEntityId { get; set; }
        public ulong ItemEntityId { get; set; }
        public int ConfigurationId { get; set; }

        public TradeRemoveItemPacket(ulong itemSourceEntityId, ulong itemEntityId, int configurationId)
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
