namespace Rasa.Packets.Petition.Client
{
    using Data;
    using Memory;

    /// <summary>
    /// Help -> Customer Services -> Report Bug -> Send, and the /bug slash command, which opens
    /// the same page with its argument prefilled (client/petitionmanager.py:36).
    /// </summary>
    public class CreateBugReportPacket : ClientPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.CreateBugReport;

        internal string Summary { get; set; }
        internal string Body { get; set; }

        public override void Read(PythonReader pr)
        {
            pr.ReadTuple();
            Summary = PetitionArgs.ReadText(pr);
            Body = PetitionArgs.ReadText(pr);
        }
    }
}
