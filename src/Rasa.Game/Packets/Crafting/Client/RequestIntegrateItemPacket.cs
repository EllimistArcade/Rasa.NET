namespace Rasa.Packets.Crafting.Client
{
    using Data;
    using Memory;

    /// <summary>RequestIntegrateItem(kraftwerksId, targetItemId, moduleItemId, slot, CRAFTACTION_INSERTION). See client/augmentations/kraftwerks.py.</summary>
    public class RequestIntegrateItemPacket : ClientPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.RequestIntegrateItem;

        public ulong KraftwerksId { get; set; }
        public ulong TargetItemId { get; set; }
        public ulong ModuleItemId { get; set; }
        public uint Slot { get; set; }
        public uint CraftingAction { get; set; }

        public override void Read(PythonReader pr)
        {
            pr.ReadTuple();
            KraftwerksId = pr.ReadULong();
            TargetItemId = pr.ReadULong();
            ModuleItemId = pr.ReadULong();
            Slot = pr.ReadUInt();
            CraftingAction = pr.ReadUInt();
        }
    }
}
