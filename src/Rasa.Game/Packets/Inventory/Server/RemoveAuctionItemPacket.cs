namespace Rasa.Packets.Inventory.Server
{
    using Data;
    using Memory;

    public class RemoveAuctionItemPacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.RemoveAuctionItem;

        public ulong EntityId { get; set; }

        public RemoveAuctionItemPacket(ulong entityId)
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
