namespace Rasa.Packets.Petition.Client
{
    using Data;
    using Memory;

    /// <summary>
    /// client/petitionmanager.py:45 - SendAddToPetition(petitionId, text). No caller, no slash
    /// command, and no petition id is kept client-side to pass. Groundwork.
    /// </summary>
    public class AddToPetitionPacket : ClientPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.AddToPetition;

        internal uint PetitionId { get; set; }
        internal string Text { get; set; }

        public override void Read(PythonReader pr)
        {
            pr.ReadTuple();
            PetitionId = pr.ReadUInt();
            Text = PetitionArgs.ReadText(pr);
        }
    }
}
