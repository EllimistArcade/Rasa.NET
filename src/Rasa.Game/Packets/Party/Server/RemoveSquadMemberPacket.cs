namespace Rasa.Packets.Party.Server
{
    using Data;
    using Memory;

    /// <summary>
    /// client/party.py Recv_RemoveSquadMember(userId, entityId): unlinks the member's manifestation,
    /// greying their row, without removing them from the party.
    /// </summary>
    public class RemoveSquadMemberPacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.RemoveSquadMember;

        internal uint UserId { get; set; }
        internal ulong EntityId { get; set; }

        internal RemoveSquadMemberPacket(uint userId, ulong entityId)
        {
            UserId = userId;
            EntityId = entityId;
        }

        public override void Write(PythonWriter pw)
        {
            pw.WriteTuple(2);
            pw.WriteUInt(UserId);
            pw.WriteULong(EntityId);
        }
    }
}
