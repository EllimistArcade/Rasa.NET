namespace Rasa.Packets.Summon.Server
{
    using Data;
    using Memory;

    /// <summary>
    /// Recv_InvitationDeclined(inviteeName): PM_INVITATION_TO_JOIN_DECLINED, told to the summoner.
    /// </summary>
    public class InvitationDeclinedPacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.InvitationDeclined;

        internal string InviteeName { get; set; }

        internal InvitationDeclinedPacket(string inviteeName)
        {
            InviteeName = inviteeName;
        }

        public override void Write(PythonWriter pw)
        {
            pw.WriteTuple(1);
            pw.WriteUnicodeString(InviteeName);
        }
    }
}
