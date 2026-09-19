namespace Rasa.Packets.Party.Server
{
    using Data;
    using Memory;

    /// <summary>
    /// client/party.py Recv_PartyDisbanded(): clears the squad, the leader and the party id and
    /// posts PM_PARTY_DISBANDED.
    /// </summary>
    public class PartyDisbandedPacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.PartyDisbanded;

        public override void Write(PythonWriter pw)
        {
            pw.WriteTuple(0);
        }
    }
}
