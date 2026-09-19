namespace Rasa.Packets.Summon.Client
{
    using Data;
    using Memory;
    using Party;

    /// <summary>
    /// client/communicator.py GotoFriend -> gameui.OnRequestFriendToInviteMeToJoin:
    /// SendCallActorMethod('RequestInvitationToJoin', (playerName,)). "Request to join your
    /// friend's position" - the other direction of the same feature.
    /// </summary>
    public class RequestInvitationToJoinPacket : ClientPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.RequestInvitationToJoin;

        internal string PlayerName { get; set; }

        public override void Read(PythonReader pr)
        {
            pr.ReadTuple();
            PlayerName = PartyArgs.ReadName(pr);
        }
    }
}
