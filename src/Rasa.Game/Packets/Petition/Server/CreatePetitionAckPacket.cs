namespace Rasa.Packets.Petition.Server
{
    using Data;
    using Memory;

    /// <summary>
    /// Recv_CreatePetitionAck(success, petitionId) - client/petitionmanager.py:75. The client
    /// branches on success to print PM_PETITION_REQUEST_SUCCESS or PM_PETITION_REQUEST_FAILURE
    /// and throws the id away; it is sent anyway so the player has something to quote.
    /// </summary>
    public class CreatePetitionAckPacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.CreatePetitionAck;

        public bool Success { get; set; }
        public uint PetitionId { get; set; }

        public CreatePetitionAckPacket(bool success, uint petitionId)
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
