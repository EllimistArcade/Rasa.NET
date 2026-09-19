namespace Rasa.Packets.Party.Client
{
    using Data;
    using Memory;

    /// <summary>
    /// client/party.py SendKickUser: SendWorldMsg('KickUserFromParty', (name,)) from /kick. Read used
    /// to only log the payload, leaving the 0x66 terminator check to fail and drop the connection.
    /// </summary>
    public class KickUserFromPartyPacket : ClientPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.KickUserFromParty;

        internal string FamilyName { get; set; }

        public override void Read(PythonReader pr)
        {
            pr.ReadTuple();
            FamilyName = PartyArgs.ReadName(pr);
        }
    }
}
