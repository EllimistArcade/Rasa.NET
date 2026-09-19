namespace Rasa.Packets.Petition.Client
{
    using Data;
    using Memory;

    /// <summary>
    /// Help -> Customer Services -> Petition -> Send, and the /petition slash command, which
    /// opens the same page with its argument prefilled (client/petitionmanager.py:28).
    /// </summary>
    public class CreateHelpRequestPacket : ClientPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.CreateHelpRequest;

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
