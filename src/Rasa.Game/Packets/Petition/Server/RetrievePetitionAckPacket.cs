namespace Rasa.Packets.Petition.Server
{
    using Data;
    using Memory;
    using Structures;

    /// <summary>
    /// Recv_RetrievePetitionAck(success, petitionId, petitionInfo) -
    /// client/petitionmanager.py:91, whose body is empty.
    ///
    /// petitionInfo is None on a refusal, and a PetitionInfo dictionary on success - a shape
    /// this server defines, because the client does not. See PetitionInfo.
    /// </summary>
    public class RetrievePetitionAckPacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.RetrievePetitionAck;

        public bool Success { get; set; }
        public uint PetitionId { get; set; }
        public PetitionInfo Info { get; set; }

        public RetrievePetitionAckPacket(bool success, uint petitionId, PetitionInfo info = null)
        {
            Success = success;
            PetitionId = petitionId;
            Info = info;
        }

        public override void Write(PythonWriter pw)
        {
            pw.WriteTuple(3);
            pw.WriteBool(Success);
            pw.WriteUInt(PetitionId);

            if (Info == null)
                pw.WriteNoneStruct();
            else
                Info.Write(pw);
        }
    }
}
