namespace Rasa.Packets.Summon.Client
{
    using Data;
    using Memory;
    using Party;

    /// <summary>
    /// manifestation.py RespondToAddAndJoinFriend: (invitationId, friendName, response). Accepting
    /// means both "add them to my friends list" and "take me to them", so the name travels with it.
    /// </summary>
    public class RespondToAddAndJoinFriendPacket : ClientPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.RespondToAddAndJoinFriend;

        internal uint InvitationId { get; set; }
        internal string FriendName { get; set; }
        internal bool Accepted { get; set; }

        public override void Read(PythonReader pr)
        {
            pr.ReadTuple();
            InvitationId = SummonArgs.ReadId(pr);
            FriendName = PartyArgs.ReadName(pr);
            Accepted = SummonArgs.ReadResponse(pr);
        }
    }
}
