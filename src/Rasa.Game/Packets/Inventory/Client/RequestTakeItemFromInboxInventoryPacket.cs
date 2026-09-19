namespace Rasa.Packets.Inventory.Client
{
    using Data;
    using Memory;

    /// <summary>
    /// The Pick Up Items tab's Receive button. Sent as
    /// (auctioneerId, entityId, destSlot) - the client will not send it at all unless an auction
    /// house is open, so the auctioneer is always present.
    /// </summary>
    public class RequestTakeItemFromInboxInventoryPacket : ClientPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.RequestTakeItemFromInboxInventory;

        public ulong EntityId { get; set; }         // the auctioneer
        public ulong ItemEntityId { get; set; }
        public uint DestSlot { get; set; }

        public override void Read(PythonReader pr)
        {
            pr.ReadTuple();
            EntityId = pr.ReadULong();
            ItemEntityId = pr.ReadULong();
            DestSlot = pr.ReadUInt();
        }
    }
}
