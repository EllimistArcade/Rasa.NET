namespace Rasa.Packets.Party.Client
{
    using Data;
    using Memory;

    /// <summary>
    /// client/party.py SendChangeLeader: SendWorldMsg('MakeUserPartyLeader', (name,)). Read used to
    /// only log the payload, which disconnected the player.
    /// </summary>
    public class MakeUserPartyLeaderPacket : ClientPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.MakeUserPartyLeader;

        internal string FamilyName { get; set; }

        public override void Read(PythonReader pr)
        {
            pr.ReadTuple();
            FamilyName = PartyArgs.ReadName(pr);
        }
    }
}
