namespace Rasa.Packets.Party.Client
{
    using Data;
    using Memory;

    /// <summary>
    /// client/party.py SendInviteSquadRequest: SendWorldMsg('InviteSquad', (targetName,)), sent by
    /// OnAcceptInviteSquadConfirmation - the Accept button on the dialog the server puts up when an
    /// invited player turns out to already be in a squad. It carries the same name the player
    /// typed, and means "invite their whole squad".
    /// </summary>
    public class InviteSquadPacket : ClientPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.InviteSquad;

        internal string FamilyName { get; set; }

        public override void Read(PythonReader pr)
        {
            pr.ReadTuple();
            FamilyName = PartyArgs.ReadName(pr);
        }
    }
}
