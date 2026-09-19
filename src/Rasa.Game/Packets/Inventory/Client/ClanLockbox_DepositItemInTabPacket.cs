namespace Rasa.Packets.Inventory.Client
{
    using Data;
    using Memory;

    public class ClanLockbox_DepositItemInTabPacket : ClientPythonPacket
    {
        public override GameOpcode Opcode => GameOpcode.ClanLockbox_DepositItemInTab;

        public int SrcSlot { get; set; }

        /// <summary>
        /// The tab the item is being dropped on - 1 to 5, not a slot. The client sends
        /// g_currentClanLockboxTabId here (inventory.py _SendServerRequest), the tab its lockbox
        /// window is showing, and leaves the slot inside it to the server. Deposits onto a
        /// particular slot are ClanLockbox_DepositItemInSlot instead.
        /// </summary>
        public int DestTab { get; set; }

        public long Quantity { get; set; }

        /// <summary>
        /// The tab arrived as None: the window has not had a tab selected yet
        /// (g_currentClanLockboxTabId starts None), so the player named no tab.
        /// </summary>
        public bool NoTabNamed { get; set; }

        public override void Read(PythonReader pr)
        {
            pr.ReadTuple();
            SrcSlot = pr.ReadInt();

            if (pr.PeekType() == PythonType.Int)
                DestTab = pr.ReadInt();
            else
            {
                NoTabNamed = true;
                pr.ReadNoneStruct();
            }

            if (pr.PeekType() == PythonType.Long)
                Quantity = pr.ReadLong();
            else
                Quantity = pr.ReadInt();
        }
    }
}
