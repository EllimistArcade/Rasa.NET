using System.Collections.Generic;

namespace Rasa.Packets.Party.Server
{
    using Data;
    using Memory;
    using Structures;

    /// <summary>
    /// client/party.py Recv_JoinSquadRequestReceived(senderName, senderSquadInfo, senderUserId):
    /// puts the squad merge window up for the leader being asked. It lists senderSquadInfo and
    /// marks the member whose name matches senderName as its leader (ui/squadmergewindow.py), and
    /// its Accept button answers with PartyJoinRequestResponse(True, senderUserId) - which is why
    /// the request has to be found again by the requester's account id.
    /// </summary>
    public class JoinSquadRequestReceivedPacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.JoinSquadRequestReceived;

        internal string SenderName { get; set; }
        internal List<PartyMember> SenderSquadInfo { get; set; }
        internal uint SenderUserId { get; set; }

        internal JoinSquadRequestReceivedPacket(string senderName, List<PartyMember> senderSquadInfo, uint senderUserId)
        {
            SenderName = senderName;
            SenderSquadInfo = senderSquadInfo;
            SenderUserId = senderUserId;
        }

        public override void Write(PythonWriter pw)
        {
            pw.WriteTuple(3);
            pw.WriteUnicodeString(SenderName);
            pw.WriteList(SenderSquadInfo.Count);

            foreach (var member in SenderSquadInfo)
                pw.WriteStruct(member);

            pw.WriteUInt(SenderUserId);
        }
    }
}
