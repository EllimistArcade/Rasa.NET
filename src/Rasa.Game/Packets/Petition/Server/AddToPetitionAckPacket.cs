namespace Rasa.Packets.Petition.Server
{
    using Data;
    using Memory;

    /// <summary>
    /// Recv_AddToPetitionAck(success, petitionId) - client/petitionmanager.py:83, empty body.
    /// </summary>
    public class AddToPetitionAckPacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.AddToPetitionAck;

        public bool Success { get; set; }
        public uint PetitionId { get; set; }

        public AddToPetitionAckPacket(bool success, uint petitionId)
        {
            Success = success;
            PetitionId = petitionId;
        }

        public override void Write(PythonWriter pw)
        {
            pw.WriteTuple(2);
            pw.WriteBool(Success);
            pw.WriteUInt(PetitionId);
        }
    }
}
