namespace Rasa.Packets.Summon.Server
{
    using Data;
    using Memory;

    /// <summary>
    /// Recv_InvitedToAddAndJoinFriend(invitationId, inviterName): PM_INVITED_TO_JOIN_NON_FRIEND,
    /// "<name> is not on your friends list but wishes to summon you to their location. Do you want
    /// to add them to your friends list and go to them?", with Accept and Decline. Unlike
    /// InvitedToJoinFriend this one builds its dialog from the arguments it is given, so it works.
    /// </summary>
    public class InvitedToAddAndJoinFriendPacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.InvitedToAddAndJoinFriend;

        internal uint InvitationId { get; set; }
        internal string InviterName { get; set; }

        internal InvitedToAddAndJoinFriendPacket(uint invitationId, string inviterName)
        {
            InvitationId = invitationId;
            InviterName = inviterName;
        }

        public override void Write(PythonWriter pw)
        {
            pw.WriteTuple(2);
            pw.WriteUInt(InvitationId);
            pw.WriteUnicodeString(InviterName);
        }
    }
}
