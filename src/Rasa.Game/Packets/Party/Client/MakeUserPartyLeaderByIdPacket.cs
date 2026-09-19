namespace Rasa.Packets.Party.Client
{
    using Data;
    using Memory;

    /// <summary>
    /// client/party.py SendChangeLeaderById: SendWorldMsg('MakeUserPartyLeaderById', (id,)) from the
    /// party window's pass-leadership option. Read used to only log the payload, which disconnected
    /// the player.
    /// </summary>
    public class MakeUserPartyLeaderByIdPacket : ClientPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.MakeUserPartyLeaderById;

        internal uint UserId { get; set; }

        public override void Read(PythonReader pr)
        {
            pr.ReadTuple();
            UserId = PartyArgs.ReadUserId(pr);
        }
    }
}
