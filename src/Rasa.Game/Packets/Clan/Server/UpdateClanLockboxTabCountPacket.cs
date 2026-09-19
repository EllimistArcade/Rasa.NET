namespace Rasa.Packets.Clan.Server
{
    using Data;
    using Memory;

    /// <summary>
    /// How many clan lockbox tabs are unlocked. A plain count, unlike the personal lockbox's
    /// LockboxTabPermissions, which sends a dictionary of five booleans - the client builds the
    /// dictionary itself here, from 1 up to the count.
    ///
    /// A count above NUM_CLAN_LOCKBOX_TABS makes it return without changing anything, so a clan
    /// row holding something impossible would leave every tab as the window last had it rather
    /// than failing visibly. Clamped before it goes out.
    /// </summary>
    public class UpdateClanLockboxTabCountPacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.UpdateClanLockboxTabCount;

        public uint Count { get; }

        public UpdateClanLockboxTabCountPacket(uint count)
        {
            Count = count > ClanLockboxTab.LastTab ? ClanLockboxTab.LastTab : count;
        }

        public override void Write(PythonWriter pw)
        {
            pw.WriteTuple(1);
            pw.WriteUInt(Count);
        }
    }
}
