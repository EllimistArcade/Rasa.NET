namespace Rasa.Packets.Petition.Server
{
    using Data;
    using Memory;

    /// <summary>
    /// Recv_CancelPetitionAck(success, petitionId) - client/petitionmanager.py:83. The handler
    /// is an empty body in the shipped client, so nothing it carries is ever shown; it is sent
    /// because the contract says a cancel is answered, and a client that grows a petition list
    /// will expect it.
    /// </summary>
    public class CancelPetitionAckPacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.CancelPetitionAck;

        public bool Success { get; set; }
        public uint PetitionId { get; set; }

        public CancelPetitionAckPacket(bool success, uint petitionId)
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
