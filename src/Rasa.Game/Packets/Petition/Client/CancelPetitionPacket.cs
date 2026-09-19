namespace Rasa.Packets.Petition.Client
{
    using Data;
    using Memory;

    /// <summary>
    /// client/petitionmanager.py:50 - SendCancelPetition(petitionId).
    ///
    /// Nothing in the shipped client calls it. There is no petition list window, `/petition`
    /// opens the Customer Services page rather than reaching this, and the Cancel buttons on
    /// that page are wired to OnCloseButton, which hides the window. The client also never
    /// retains a petition id to pass: Recv_CreatePetitionAck takes one and throws it away.
    /// Wired as groundwork; see PetitionManager.CancelPetition.
    /// </summary>
    public class CancelPetitionPacket : ClientPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.CancelPetition;

        internal uint PetitionId { get; set; }

        public override void Read(PythonReader pr)
        {
            pr.ReadTuple();
            PetitionId = pr.ReadUInt();
        }
    }
}
