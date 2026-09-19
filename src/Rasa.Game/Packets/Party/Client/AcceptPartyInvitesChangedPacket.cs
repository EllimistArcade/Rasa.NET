namespace Rasa.Packets.Party.Client
{
    using Data;
    using Memory;

    /// <summary>
    /// client/communicator.py: SendWorldMsg('AcceptPartyInvitesChanged', (value.lower() == 'true',)).
    /// Read used to require the True struct, so turning invites off (None) disconnected the player.
    /// </summary>
    public class AcceptPartyInvitesChangedPacket : ClientPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.AcceptPartyInvitesChanged;

        internal bool Accept { get; set; }

        public override void Read(PythonReader pr)
        {
            pr.ReadTuple();
            Accept = pr.ReadBool();
        }
    }
}
