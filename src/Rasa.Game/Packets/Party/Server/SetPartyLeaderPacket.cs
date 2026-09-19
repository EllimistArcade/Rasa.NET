namespace Rasa.Packets.Party.Server
{
    using Data;
    using Memory;

    /// <summary>
    /// client/party.py Recv_SetPartyLeader(userId). A userId found in the recipient's member list
    /// names that member leader; anything else - their own id, or None - makes the recipient the
    /// leader (IsPartyLeader is g_partyLeaderId is None). None is also what clears a former member's
    /// stale leader id, which otherwise stops them inviting after they leave.
    /// </summary>
    public class SetPartyLeaderPacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.SetPartyLeader;

        internal uint UserId { get; set; }

        internal SetPartyLeaderPacket(uint userId)
        {
            UserId = userId;
        }

        public override void Write(PythonWriter pw)
        {
            pw.WriteTuple(1);

            if (UserId != 0)
                pw.WriteUInt(UserId);
            else
                pw.WriteNoneStruct();
        }
    }
}
