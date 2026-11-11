namespace Rasa.Packets.Inventory.Client
{
    using Data;
    using Memory;

    public class ClanLockbox_DestroyItemPacket : ClientPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.ClanLockbox_DestroyItem;

        public ulong EntityId { get; set; }
        public uint Quantity { get; set; }

        public override void Read(PythonReader pr)
        {
            pr.ReadTuple();
            // The client sends both as Python ints: the id is the dragged widget's
            // (inventory.py DestroyItemInInventory, dndWidget.GetData('id')), the quantity a stack
            // count. Reading only the long form threw, and the throw closed the connection.
            EntityId = pr.ReadId();
            Quantity = pr.ReadCount();
        }
    }
}
