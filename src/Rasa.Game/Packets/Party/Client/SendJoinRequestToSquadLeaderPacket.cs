namespace Rasa.Packets.Party.Client
{
    using Data;
    using Memory;

    /// <summary>
    /// client/party.py SendJoinRequestToSquadLeader: SendWorldMsg('SendJoinRequestToSquadLeader', (targetName,)).
    /// Join requests are not implemented; the argument is read so the request no longer disconnects.
    /// </summary>
    public class SendJoinRequestToSquadLeaderPacket : ClientPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.SendJoinRequestToSquadLeader;

        internal string FamilyName { get; set; }

        public override void Read(PythonReader pr)
        {
            pr.ReadTuple();
            FamilyName = PartyArgs.ReadName(pr);
        }
    }
}
