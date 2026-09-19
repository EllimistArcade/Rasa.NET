namespace Rasa.Packets.Party.Server
{
    using Data;
    using Memory;

    /// <summary>
    /// client/party.py Recv_AddSquadMember(userId, entityId): links a party member to their
    /// manifestation, which lights up their health bar in the party window and their overhead name.
    /// </summary>
    public class AddSquadMemberPacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.AddSquadMember;

        internal uint UserId { get; set; }
        internal ulong EntityId { get; set; }

        internal AddSquadMemberPacket(uint userId, ulong entityId)
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
