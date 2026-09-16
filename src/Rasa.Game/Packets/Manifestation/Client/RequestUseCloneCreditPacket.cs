namespace Rasa.Packets.Manifestation.Client
{
    using Data;
    using Memory;

    /// <summary>
    /// A clone-credit item being used from the inventory: <c>(entityId,)</c>, the item's own id.
    ///
    /// <c>client/augmentations/clonecredit.py</c> sends this from <c>InventoryUse</c>, which is
    /// the right-click handler, and the item is real - template 111219 carries entity class 26491
    /// <c>ItemCloneCredit</c>, whose augmentation list includes 70, <see cref="AugmentationType.CloneCredit"/>,
    /// "items that give a cloning credit to the player upon use".
    ///
    /// Until this existed the opcode had no handler, and an unhandled opcode fails the packet
    /// terminator check and closes the connection - so right-clicking one of these disconnected
    /// the player.
    /// </summary>
    public class RequestUseCloneCreditPacket : ClientPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.RequestUseCloneCredit;

        public ulong EntityId { get; set; }

        public override void Read(PythonReader pr)
        {
            pr.ReadTuple();
            EntityId = pr.ReadULong();
        }
    }
}
