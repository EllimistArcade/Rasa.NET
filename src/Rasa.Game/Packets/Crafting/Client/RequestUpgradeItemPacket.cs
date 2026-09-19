namespace Rasa.Packets.Crafting.Client
{
    using Data;
    using Memory;

    /// <summary>RequestUpgradeItem(kraftwerksId, itemId, CRAFTACTION_UPGRADE). See client/augmentations/kraftwerks.py.</summary>
    public class RequestUpgradeItemPacket : ClientPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.RequestUpgradeItem;

        public ulong KraftwerksId { get; set; }
        public ulong ItemId { get; set; }
        public uint CraftingAction { get; set; }

        public override void Read(PythonReader pr)
        {
            pr.ReadTuple();
            KraftwerksId = pr.ReadULong();
            ItemId = pr.ReadULong();
            CraftingAction = pr.ReadUInt();
        }
    }
}
