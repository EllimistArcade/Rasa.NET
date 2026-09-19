namespace Rasa.Packets.Inventory.Server
{
    using Data;
    using Memory;

    public class RemoveInboxItemPacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.RemoveInboxItem;

        public ulong EntityId { get; set; }

        public RemoveInboxItemPacket(ulong entityId)
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
