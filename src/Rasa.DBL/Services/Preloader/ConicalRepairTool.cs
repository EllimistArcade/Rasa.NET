namespace Rasa.Services.Preloader
{
    /// <summary>
    /// Too Quiet's Conical Repair Tool (CavesOfDonnContent), item template 121310, is of entity
    /// class 28699, DELETEME_BROKEN: the client's entityclass has it with no augmentation at
    /// all, so the entity it makes for one is not an Item. The backpack draws it from the item
    /// class's icon and then fails on it - the slot keeps its "000" placeholder, the tooltip has
    /// no name, the window can no longer be shown again until the client restarts, and the
    /// vendor's repair tab, which lists the pack, the equipment and the drawer in one go, lists
    /// nothing. The template's own row puts it in the Miscellaneous tab.
    ///
    /// The reward is the Direct Repair Tool of the same band instead, 3869
    /// (Tool_Avatar_Field_Repair_Direct_CMN_05_to_09): the Pools' missions read a "Conical"
    /// repair tool as the Direct one already, and its item class, quality and prices are
    /// 121310's. The items already given are made 3869 too; the loader moves one in the
    /// Miscellaneous tab to the first free slot of the Equipment tab
    /// (InventoryManager.InitCharacterInventory).
    ///
    /// Down gives Too Quiet 121310 back. The items stay 3869: nothing tells one made from a
    /// 121310 from any other.
    /// </summary>
    public static class ConicalRepairTool
    {
        public const uint Broken = 121310;
        public const uint Replacement = 3869;

        public static readonly string[] WorldUp = Reward(Broken, Replacement);

        public static readonly string[] WorldDown = Reward(Replacement, Broken);

        public static readonly string[] CharUp =
        {
            $"update items set item_template_id = {Replacement} where item_template_id = {Broken};"
        };

        private static string[] Reward(uint from, uint to) => new[]
        {
            $"update mission_reward_item set item_template_id = {to} where mission_id = {CavesOfDonnContent.TooQuiet} " +
            $"and content_revision = '{CavesOfDonnContent.Revision}' and item_template_id = {from};"
        };
    }
}
