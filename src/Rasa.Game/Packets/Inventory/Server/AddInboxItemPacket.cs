namespace Rasa.Packets.Inventory.Server
{
    using Data;
    using Memory;

    /// <summary>
    /// Puts one item into the client's inbox, which is what the auction house's Pick Up Items
    /// tab lists. Sent per item at login as well: the client's CreateInventory would take a
    /// whole inbox at once, but it iterates that list expecting bare entity ids while
    /// InventoryCreatePacket writes (index, entityId) pairs, so the per-item call is the one
    /// that works.
    /// </summary>
    public class AddInboxItemPacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.AddInboxItem;

        public ulong EntityId { get; set; }

        public AddInboxItemPacket(ulong entityId)
        {
            EntityId = entityId;
        }

        public override void Write(PythonWriter pw)
        {
            pw.WriteTuple(1);
            pw.WriteULong(EntityId);
        }
    }
}
