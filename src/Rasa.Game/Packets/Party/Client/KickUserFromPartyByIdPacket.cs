namespace Rasa.Packets.Party.Client
{
    using Data;
    using Memory;

    /// <summary>
    /// client/party.py SendKickUserById: SendWorldMsg('KickUserFromPartyById', (id,)), the userId
    /// (account id) of a party window row.
    /// </summary>
    public class KickUserFromPartyByIdPacket : ClientPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.KickUserFromPartyById;

        internal uint UserId { get; set; }

        public override void Read(PythonReader pr)
        {
            pr.ReadTuple();
            UserId = PartyArgs.ReadUserId(pr);
        }
    }
}
