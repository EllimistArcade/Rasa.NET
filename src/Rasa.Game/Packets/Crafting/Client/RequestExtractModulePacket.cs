namespace Rasa.Packets.Crafting.Client
{
    using Data;
    using Memory;

    /// <summary>RequestExtractModule(kraftwerksId, itemId, slot, CRAFTACTION_EXTRACTION). See client/augmentations/kraftwerks.py.</summary>
    public class RequestExtractModulePacket : ClientPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.RequestExtractModule;

        public ulong KraftwerksId { get; set; }
        public ulong ItemId { get; set; }
        public uint Slot { get; set; }
        public uint CraftingAction { get; set; }

        public override void Read(PythonReader pr)
        {
            pr.ReadTuple();
            KraftwerksId = pr.ReadULong();
            ItemId = pr.ReadULong();
            Slot = pr.ReadUInt();
            CraftingAction = pr.ReadUInt();
        }
    }
}
