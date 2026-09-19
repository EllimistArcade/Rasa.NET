namespace Rasa.Packets.Crafting.Client
{
    using Data;
    using Memory;

    /// <summary>RequestCraftItemNew(kraftwerksId, recipeItemEntityId, craftingPage) - the 1.16.5 crafting window's fabrication request. See client/augmentations/kraftwerks.py.</summary>
    public class RequestCraftItemNewPacket : ClientPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.RequestCraftItemNew;

        public ulong KraftwerksId { get; set; }
        /// <summary>The schematic item in the player's inventory (an entity id, not the recipe template).</summary>
        public ulong RecipeItemId { get; set; }
        public uint CraftingPage { get; set; }

        public override void Read(PythonReader pr)
        {
            pr.ReadTuple();
            KraftwerksId = pr.ReadULong();
            RecipeItemId = pr.ReadULong();
            CraftingPage = pr.ReadUInt();
        }
    }
}
