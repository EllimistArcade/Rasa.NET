namespace Rasa.Packets.MapChannel.Client
{
    using Data;
    using Memory;

    /// <summary>
    /// Repair one item at one vendor. `client/actions/repair.py` sends it as
    /// RequestRepair(itemId, vendorId) - note the order, which is the opposite way round from
    /// RequestVendorRepair(vendorId, itemList).
    ///
    /// No UI in this client reaches it: the vendor window's Repair and Repair All buttons both
    /// go through Vendor.RepairItems, which sends RequestVendorRepair, and the only thing that
    /// builds a RepairAction is Manifestation.RequestRepairWeapon, which nothing calls. It is
    /// handled anyway because an opcode with no packet class fails the terminator check and
    /// closes the connection (1.11), and because the work is the same validation either way.
    /// </summary>
    public class RequestRepairPacket : ClientPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.RequestRepair;

        public ulong ItemEntityId { get; set; }
        public ulong VendorEntityId { get; set; }

        public override void Read(PythonReader pr)
        {
            pr.ReadTuple();
            ItemEntityId = pr.ReadULong();
            VendorEntityId = pr.ReadULong();
        }
    }
}
