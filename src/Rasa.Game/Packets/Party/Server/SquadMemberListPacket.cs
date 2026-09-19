namespace Rasa.Packets.Party.Server
{
    using System.Collections.Generic;
    using Data;
    using Memory;

    /// <summary>
    /// client/party.py Recv_SquadMemberList(squadMembers, partyExclusiveMap): the (userId, entityId)
    /// pairs of members who are in the world. partyExclusiveMap is always false; there are no
    /// squad-exclusive map instances.
    /// </summary>
    public class SquadMemberListPacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.SquadMemberList;

        internal List<(uint UserId, ulong EntityId)> Members { get; }

        internal SquadMemberListPacket(List<(uint UserId, ulong EntityId)> members)
        {
            Members = members;
        }

        public override void Write(PythonWriter pw)
        {
            pw.WriteTuple(2);
            pw.WriteList(Members.Count);

            foreach (var (userId, entityId) in Members)
            {
                pw.WriteTuple(2);
                pw.WriteUInt(userId);
                pw.WriteULong(entityId);
            }

            pw.WriteBool(false);
        }
    }
}
