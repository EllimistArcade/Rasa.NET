namespace Rasa.Packets.Petition.Client
{
    using Data;
    using Memory;

    /// <summary>
    /// client/petitionmanager.py:55 - SendRetrievePetition(petitionId). Unreachable in the
    /// shipped client for the same reasons as CancelPetition: no caller, no slash command, and
    /// no petition id is ever kept client-side to pass. Groundwork.
    /// </summary>
    public class RetrievePetitionPacket : ClientPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.RetrievePetition;

        internal uint PetitionId { get; set; }

        public override void Read(PythonReader pr)
        {
            pr.ReadTuple();
            PetitionId = pr.ReadUInt();
        }
    }
}
