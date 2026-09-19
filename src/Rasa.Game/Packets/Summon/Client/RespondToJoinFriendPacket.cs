namespace Rasa.Packets.Summon.Client
{
    using Data;
    using Memory;
    using Party;

    /// <summary>
    /// manifestation.py RespondToJoinFriend: (invitationId, response). response is the int 1 or 0,
    /// not a bool.
    /// </summary>
    public class RespondToJoinFriendPacket : ClientPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.RespondToJoinFriend;

        internal uint InvitationId { get; set; }
        internal bool Accepted { get; set; }

        public override void Read(PythonReader pr)
        {
            pr.ReadTuple();
            InvitationId = SummonArgs.ReadId(pr);
            Accepted = SummonArgs.ReadResponse(pr);
        }
    }
}
