namespace Rasa.Packets.Party.Server
{
    using Data;
    using Memory;

    /// <summary>
    /// client/party.py Recv_RequestToJoinLeaderConfirmationRequest(inviteeName): asks the player
    /// who sent a join request to someone who does not lead their squad whether to send it on to
    /// the leader instead ("%(invitee)s is not the squad leader. Would you like to send a join
    /// request to the squad leader?"). Accepting sends SendJoinRequestToSquadLeader with the same
    /// name, so this carries the player who was asked, not the leader.
    /// </summary>
    public class RequestToJoinLeaderConfirmationRequestPacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.RequestToJoinLeaderConfirmationRequest;

        internal string InviteeName { get; set; }

        internal RequestToJoinLeaderConfirmationRequestPacket(string inviteeName)
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
