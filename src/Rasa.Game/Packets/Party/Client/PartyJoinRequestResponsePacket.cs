namespace Rasa.Packets.Party.Client
{
    using Data;
    using Memory;

    /// <summary>
    /// client/party.py SendJoinResponse: SendWorldMsg('PartyJoinRequestResponse', (accepted, senderUserId)).
    /// Join requests are not implemented; the arguments are read so the response no longer disconnects.
    /// </summary>
    public class PartyJoinRequestResponsePacket : ClientPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.PartyJoinRequestResponse;

        internal bool Accepted { get; set; }
        internal uint SenderUserId { get; set; }

        public override void Read(PythonReader pr)
        {
            pr.ReadTuple();
            Accepted = pr.ReadBool();
            SenderUserId = PartyArgs.ReadUserId(pr);
        }
    }
}
