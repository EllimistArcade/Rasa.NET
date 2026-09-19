namespace Rasa.Packets.Summon.Client
{
    using Data;
    using Memory;
    using Party;

    /// <summary>
    /// client/communicator.py SummonFriend -> gameui.OnInviteFriendToJoin:
    /// SendCallActorMethod('InviteFriendToJoin', (playerName,)). "Invite the given friend to be
    /// teleported to your position." No slash command or UI in the 1.16.5.0 client reaches it.
    /// </summary>
    public class InviteFriendToJoinPacket : ClientPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.InviteFriendToJoin;

        internal string PlayerName { get; set; }

        public override void Read(PythonReader pr)
        {
            pr.ReadTuple();
            PlayerName = PartyArgs.ReadName(pr);
        }
    }
}
