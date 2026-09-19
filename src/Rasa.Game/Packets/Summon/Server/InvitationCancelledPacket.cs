namespace Rasa.Packets.Summon.Server
{
    using Data;
    using Memory;

    /// <summary>
    /// Recv_InvitationCancelled(inviterName): PM_INVITATION_TO_JOIN_CANCELLED - the summon is off,
    /// told to the player who was being summoned.
    /// </summary>
    public class InvitationCancelledPacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.InvitationCancelled;

        internal string InviterName { get; set; }

        internal InvitationCancelledPacket(string inviterName)
        {
            InviterName = inviterName;
        }

        public override void Write(PythonWriter pw)
        {
            pw.WriteTuple(1);
            pw.WriteUnicodeString(InviterName);
        }
    }
}
