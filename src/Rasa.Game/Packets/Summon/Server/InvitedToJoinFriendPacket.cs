namespace Rasa.Packets.Summon.Server
{
    using Data;
    using Memory;

    /// <summary>
    /// Recv_InvitedToJoinFriend(invitationId, inviterName): the summon prompt for someone already
    /// on the target's friends list.
    ///
    /// NOTE: broken in the 1.16.5.0 client. It stores the id, raises a pending indicator, and the
    /// indicator's only action is ActivateFriendRequestDialog, which returns unless the
    /// manifestation has tmp_invitationName - a field nothing in the client ever assigns (the
    /// original devs left a TR_LOG_WARNING there saying as much). The target is left with an
    /// indicator that opens nothing and no way to answer. SummonManager sends
    /// InvitedToAddAndJoinFriend instead, whose dialog works; this packet is here for a client
    /// that fixes the field.
    /// </summary>
    public class InvitedToJoinFriendPacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.InvitedToJoinFriend;

        internal uint InvitationId { get; set; }
        internal string InviterName { get; set; }

        internal InvitedToJoinFriendPacket(uint invitationId, string inviterName)
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
