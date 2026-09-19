namespace Rasa.Packets.Crafting.Client
{
    using Data;
    using Memory;

    /// <summary>RequestCraftItem(kraftwerksId, recipeTemplateId) - the older window's form; kraftwerks.py still sends it when no page is given. See client/augmentations/kraftwerks.py.</summary>
    public class RequestCraftItemPacket : ClientPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.RequestCraftItem;

        public ulong KraftwerksId { get; set; }
        public uint RecipeTemplateId { get; set; }

        public override void Read(PythonReader pr)
        {
            pr.ReadTuple();
            KraftwerksId = pr.ReadULong();
            RecipeTemplateId = pr.ReadUInt();
        }
    }
}
